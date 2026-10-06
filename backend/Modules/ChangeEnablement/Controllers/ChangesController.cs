using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ChangeEnablement;

[Route("api/changes")]
public class ChangesController(AppDbContext db) : RecordController<Change, ChangeDto>(db)
{
    private const string StandardModel = "Modèle standard (pré-autorisé)";

    protected override string Code => "CHG";
    protected override string LinkType => "change";
    protected override string[] Statuses => Change.Statuses;

    protected override string? Apply(Change e, ChangeDto dto, bool creating)
    {
        var error = Allowed.Check("Type de changement", dto.ChangeType, Change.Types)
                    ?? Allowed.Check("Risque", dto.Risk, Change.Risks)
                    ?? Allowed.Check("Résultat", dto.Outcome, Change.Outcomes, optional: true);
        if (error is not null) return error;
        if (dto.PlannedStart is not null && dto.PlannedEnd is not null && dto.PlannedEnd < dto.PlannedStart)
            return "La fin planifiée précède le début planifié.";
        e.ChangeType = dto.ChangeType!; e.Risk = dto.Risk!;
        e.PlannedStart = Dates.Utc(dto.PlannedStart); e.PlannedEnd = Dates.Utc(dto.PlannedEnd);
        e.ImplementationPlan = dto.ImplementationPlan ?? ""; e.BackoutPlan = dto.BackoutPlan ?? "";
        e.Outcome = string.IsNullOrEmpty(dto.Outcome) ? null : dto.Outcome;
        return null;
    }

    // Un changement standard suit un modèle déjà évalué : il est pré-autorisé.
    protected override string InitialStatus(Change e) => e.ChangeType == "Standard" ? "Autorisé" : "Demandé";

    protected override bool RequiresManager(string status) => status is "Autorisé" or "Rejeté";

    protected override string? CheckTransition(Change e, string to)
    {
        if (to is "Planifié" or "Mis en œuvre" or "Clos" && e.AuthorizedAt is null && e.Status != "Rejeté")
            return "Le changement doit d'abord être autorisé par un gestionnaire.";
        if (to == "Planifié" && e.PlannedStart is null)
            return "Renseigner le début planifié pour inscrire le changement au calendrier.";
        if (to == "Clos" && e.Status != "Rejeté" && e.Outcome is null)
            return "Renseigner le résultat (Réussi ou Échoué) avant de clore le changement.";
        return null;
    }

    protected override void OnStatusChanged(Change e, string from)
    {
        if (e.Status == "Autorisé" && e.AuthorizedAt is null)
        {
            e.AuthorizedAt = DateTime.UtcNow;
            e.AuthorizedBy = from == "" ? StandardModel : MyDisplay;
        }
        else if (e.Status is "Demandé" or "Évalué" or "Rejeté") { e.AuthorizedAt = null; e.AuthorizedBy = null; }
        e.ClosedAt = e.Status == "Clos" ? DateTime.UtcNow : null;
    }

    /// <summary>Calendrier des changements : changements planifiés non rejetés, à partir d'une date.</summary>
    [HttpGet("schedule")]
    public async Task<IActionResult> Schedule([FromQuery] DateTime? from)
    {
        var start = Dates.Utc(from) ?? DateTime.UtcNow.Date.AddDays(-7);
        var items = await Db.Changes.AsNoTracking()
            .Where(c => c.PlannedStart != null && c.Status != "Rejeté"
                        && (c.PlannedEnd ?? c.PlannedStart) >= start)
            .OrderBy(c => c.PlannedStart)
            .Select(c => new { c.Id, c.Reference, c.Title, c.Status, c.ChangeType, c.Risk,
                               c.PlannedStart, c.PlannedEnd, c.OwnerDisplayName })
            .ToListAsync();
        return Ok(items);
    }
}
