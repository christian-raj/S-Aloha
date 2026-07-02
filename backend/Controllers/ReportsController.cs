using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProblemManagement.Api.Data;

namespace ProblemManagement.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "User")]
public class ReportsController(AppDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var now = DateTime.UtcNow;
        var problems = await db.Problems.AsNoTracking().ToListAsync();
        var actions = await db.Actions.AsNoTracking().Include(a => a.Raci).ToListAsync();

        var closed = problems.Where(p => p.ClosedAt != null).ToList();
        double? mttrDays = closed.Count > 0
            ? Math.Round(closed.Average(p => (p.ClosedAt!.Value - p.CreatedAt).TotalDays), 1)
            : null;

        return Ok(new
        {
            ProblemsByStatus = problems.GroupBy(p => p.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() }),
            ProblemsByPriority = problems.GroupBy(p => p.Priority).OrderBy(g => g.Key)
                .Select(g => new { Priority = g.Key, Count = g.Count() }),
            ProblemsByCategory = problems.GroupBy(p => p.Category)
                .Select(g => new { Category = g.Key, Count = g.Count() }),
            ActionsByStatus = actions.GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() }),
            OverdueActions = actions
                .Where(a => a.Status != "Terminée" && a.Status != "Annulée"
                            && a.DueDate != null && a.DueDate < now)
                .Select(a => new { a.Id, a.Title, a.DueDate,
                    Responsibles = a.Raci.Where(r => r.Role == "R").Select(r => r.AssigneeDisplayName) }),
            MttrDays = mttrDays,
            OpenProblems = problems.Count(p => p.Status != "Clos"),
            KnownErrors = problems.Count(p => p.Status == "Erreur connue"),
            TotalProblems = problems.Count,
            TotalActions = actions.Count,
            TotalAnalyses = await db.Analyses.CountAsync(),
            Contributors = problems.Select(p => p.CreatedBy).Distinct().Count(),
            LastActivity = problems.Count > 0 ? problems.Max(p => p.UpdatedAt) : (DateTime?)null
        });
    }
}

// --- Console personnalisée par rôle ---
[ApiController]
[Route("api/console")]
[Authorize(Policy = "User")]
public class ConsoleController(AppDbContext db) : ControllerBase
{
    /// <summary>Console adaptée au rôle : User = mon activité, Manager/Admin = pilotage du processus.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var me = User.Identity?.Name ?? "";
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "User";
        var now = DateTime.UtcNow;

        // --- Bloc personnel (tous les rôles) ---
        var myProblems = await db.Problems.AsNoTracking()
            .Where(p => p.CreatedBy == me && p.Status != "Clos")
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new { p.Id, p.Reference, p.Title, p.Status, p.Priority, p.CreatedAt })
            .Take(10).ToListAsync();

        var myActions = await db.Actions.AsNoTracking()
            .Include(a => a.Raci).Include(a => a.Problem)
            .Where(a => a.Status != "Terminée" && a.Status != "Annulée"
                        && a.Raci.Any(r => r.AssigneeId == me))
            .OrderBy(a => a.DueDate)
            .Select(a => new
            {
                a.Id, a.Title, a.Status, a.DueDate,
                MyRoles = a.Raci.Where(r => r.AssigneeId == me).Select(r => r.Role),
                Problem = new { a.Problem!.Id, a.Problem.Reference, a.Problem.Title },
                Overdue = a.DueDate != null && a.DueDate < now
            }).ToListAsync();

        object? management = null;
        if (role is "Manager" or "Admin")
        {
            // --- Bloc pilotage (Manager / Admin) ---
            var toQualify = await db.Problems.AsNoTracking()
                .Where(p => p.Status == "Nouveau").OrderBy(p => p.CreatedAt)
                .Select(p => new { p.Id, p.Reference, p.Title, p.Priority, p.CreatedAt, p.CreatedByDisplayName })
                .ToListAsync();

            var inAnalysisNoRootCause = await db.Problems.AsNoTracking()
                .Where(p => p.Status == "En analyse" && (p.RootCause == null || p.RootCause == ""))
                .Select(p => new { p.Id, p.Reference, p.Title, p.Priority, AnalysesCount = p.Analyses.Count })
                .ToListAsync();

            var knownErrorsNoAction = await db.Problems.AsNoTracking()
                .Where(p => p.Status == "Erreur connue" && !p.Actions.Any(a => a.Status != "Annulée"))
                .Select(p => new { p.Id, p.Reference, p.Title, p.Priority })
                .ToListAsync();

            var overdue = await db.Actions.AsNoTracking()
                .Include(a => a.Raci).Include(a => a.Problem)
                .Where(a => a.Status != "Terminée" && a.Status != "Annulée"
                            && a.DueDate != null && a.DueDate < now)
                .Select(a => new
                {
                    a.Id, a.Title, a.DueDate,
                    Problem = new { a.Problem!.Id, a.Problem.Reference },
                    Responsibles = a.Raci.Where(r => r.Role == "R").Select(r => r.AssigneeDisplayName)
                }).ToListAsync();

            management = new { ToQualify = toQualify, InAnalysisNoRootCause = inAnalysisNoRootCause,
                               KnownErrorsNoAction = knownErrorsNoAction, OverdueActions = overdue };
        }

        return Ok(new { Role = role, Username = me, MyProblems = myProblems, MyActions = myActions,
                        Management = management });
    }
}
