using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Core.Pilotage;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "User")]
public class ReportsController(AppDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var today = DateTime.UtcNow.Date; // en retard à partir du lendemain de l'échéance (M6)
        var problems = await db.Problems.AsNoTracking().ToListAsync();
        var actions = await db.Actions.AsNoTracking().Include(a => a.Raci).ToListAsync();

        var closed = problems.Where(p => p.ClosedAt != null).ToList();
        double? mttrDays = closed.Count > 0
            ? Math.Round(closed.Average(p => (p.ClosedAt!.Value - p.CreatedAt).TotalDays), 1)
            : null;

        var resolved = await db.Incidents.AsNoTracking().Where(i => i.ResolvedAt != null)
            .Select(i => new { i.CreatedAt, i.ResolvedAt }).ToListAsync();
        double? incidentMttrHours = resolved.Count > 0
            ? Math.Round(resolved.Average(i => (i.ResolvedAt!.Value - i.CreatedAt).TotalHours), 1)
            : null;
        var outcomes = await db.Changes.AsNoTracking().Where(c => c.Outcome != null)
            .Select(c => c.Outcome).ToListAsync();
        double? changeSuccessRate = outcomes.Count > 0
            ? Math.Round(100.0 * outcomes.Count(o => o == "Réussi") / outcomes.Count)
            : null;

        // Volumétrie par processus, dans l'ordre du cycle de vie du service.
        var processes = new[]
        {
            await Breakdown(db.Incidents, "Incidents"),
            await Breakdown(db.ServiceRequests, "Demandes"),
            await Breakdown(db.Changes, "Changements"),
            await Breakdown(db.ConfigurationItems, "Configuration"),
            await Breakdown(db.Agreements, "Niveaux de service (SLA)"),
            await Breakdown(db.KnowledgeArticles, "Connaissances"),
            await Breakdown(db.Improvements, "Amélioration continue"),
        };

        return Ok(new
        {
            IncidentMttrHours = incidentMttrHours,
            ChangeSuccessRate = changeSuccessRate,
            MajorIncidentsOpen = await db.Incidents.CountAsync(i => i.IsMajor && i.Status != "Résolu" && i.Status != "Clos"),
            Processes = processes,
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
                            && a.DueDate != null && a.DueDate < today)
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

    private static async Task<object> Breakdown<T>(DbSet<T> set, string label) where T : Record
    {
        var byStatus = await set.AsNoTracking().GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync();
        return new { Process = label, Total = byStatus.Sum(s => s.Count), ByStatus = byStatus };
    }
}
