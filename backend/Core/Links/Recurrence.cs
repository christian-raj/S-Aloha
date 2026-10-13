using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Notifications;
using SAloha.Api.Modules.ContinualImprovement;
using SAloha.Api.Modules.ProblemManagement;

namespace SAloha.Api.Core.Links;

/// <summary>
/// PRB-23 et CSI-20 : un problème Résolu ou Clos qui reçoit un nouvel incident relié est
/// une **récurrence après correctif**. Les gestionnaires du périmètre sont alertés, et une
/// amélioration est proposée (source « Problème », reliée au problème), sauf si une
/// amélioration ouverte lui est déjà reliée. Vit dans le socle des liens : les modules
/// Problèmes et Amélioration ne s'appellent pas.
/// </summary>
public static class Recurrence
{
    public static async Task OnLinkAsync(AppDbContext db, Notifier notify, ItemLink link, string actor, string actorDisplay)
    {
        var (problemId, incidentId) = link switch
        {
            { FromType: "problem", ToType: "incident" } => (link.FromId, link.ToId),
            { FromType: "incident", ToType: "problem" } => (link.ToId, link.FromId),
            _ => (0, 0)
        };
        if (problemId == 0) return;
        var problem = await db.Problems.AsNoTracking().FirstOrDefaultAsync(p => p.Id == problemId);
        if (problem is null || problem.Status is not ("Résolu" or "Clos")) return;
        var incident = await db.Incidents.AsNoTracking().Where(i => i.Id == incidentId).Select(i => i.Reference).FirstAsync();

        notify.ToManagers(problem, "problem", $"Récurrence après correctif : {incident}");

        // Une amélioration ouverte déjà reliée au problème suffit.
        var linked = await db.ItemLinks.Where(l => (l.FromType == "problem" && l.FromId == problemId && l.ToType == "improvement")
                                                   || (l.ToType == "problem" && l.ToId == problemId && l.FromType == "improvement"))
            .Select(l => l.FromType == "improvement" ? l.FromId : l.ToId).ToListAsync();
        if (await db.Improvements.AnyAsync(i => linked.Contains(i.Id) && !Improvement.Done.Contains(i.Status)))
        {
            await notify.SaveAsync();
            return;
        }

        var title = $"Récurrence de {problem.Reference} : {problem.Title}";
        var improvement = await References.CreateAsync(db, "AMI", () => new Improvement
        {
            Title = title.Length > 200 ? title[..200] : title,
            Description = $"Le problème {problem.Reference}, {problem.Status.ToLower()}, a reçu un nouvel incident relié ({incident}) : "
                          + "le correctif ne tient pas, ou la cause est plus large. Proposée automatiquement (CSI-20).",
            Status = "Proposée", Step = 2, Priority = "Moyenne", Source = "Problème", PerimeterId = problem.PerimeterId,
            CreatedBy = actor, CreatedByDisplayName = actorDisplay
        });
        db.ItemLinks.Add(new ItemLink { FromType = "improvement", FromId = improvement.Id, ToType = "problem", ToId = problemId, CreatedBy = actor });
        await db.SaveChangesAsync();
        notify.ToManagers(improvement, "improvement", "Amélioration proposée");
        await notify.SaveAsync();
    }
}
