using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Core.Pilotage;

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
