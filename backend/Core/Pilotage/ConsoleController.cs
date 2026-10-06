using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Modules.ProblemManagement;

namespace SAloha.Api.Core.Pilotage;

/// <summary>Console personnalisée par rôle.</summary>
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
        // L'AD ignore la casse des identifiants : la comparaison aussi (M2,
        // revue du 2026-10-06). Couvre les données enregistrées avant que le
        // jeton ne porte le sAMAccountName de l'annuaire.
        var meKey = me.ToLowerInvariant();
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "User";
        // L'échéance est une date (stockée à minuit UTC) : une action n'est en
        // retard qu'à partir du LENDEMAIN de son échéance, pas dès le matin
        // du jour même (M6, revue du 2026-10-06).
        var today = DateTime.UtcNow.Date;

        // --- Bloc personnel (tous les rôles) ---
        var myProblems = await db.Problems.AsNoTracking()
            .Where(p => p.CreatedBy.ToLower() == meKey && p.Status != "Clos")
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new { p.Id, p.Reference, p.Title, p.Status, p.Priority, p.CreatedAt })
            .Take(10).ToListAsync();

        var myActions = await db.Actions.AsNoTracking()
            .Include(a => a.Raci).Include(a => a.Problem)
            .Where(a => a.Status != "Terminée" && a.Status != "Annulée"
                        && a.Raci.Any(r => r.AssigneeId.ToLower() == meKey))
            .OrderBy(a => a.DueDate)
            .Select(a => new
            {
                a.Id, a.Title, a.Status, a.DueDate,
                MyRoles = a.Raci.Where(r => r.AssigneeId.ToLower() == meKey).Select(r => r.Role),
                Problem = new { a.Problem!.Id, a.Problem.Reference, a.Problem.Title },
                Overdue = a.DueDate != null && a.DueDate < today
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
                            && a.DueDate != null && a.DueDate < today)
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
