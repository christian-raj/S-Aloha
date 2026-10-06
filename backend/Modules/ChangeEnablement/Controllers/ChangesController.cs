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
        // L'autorisation vaut pour un type donné : un standard pré-autorisé
        // passé en urgent garderait sinon une autorisation jamais donnée.
        if (!creating && e.AuthorizedAt is not null && dto.ChangeType != e.ChangeType)
            return "Le type d'un changement autorisé ne peut plus changer : créer un nouveau changement.";
        e.ChangeType = dto.ChangeType!; e.Risk = dto.Risk!;
        e.PlannedStart = Dates.Utc(dto.PlannedStart); e.PlannedEnd = Dates.Utc(dto.PlannedEnd);
        e.ImplementationPlan = dto.ImplementationPlan ?? ""; e.BackoutPlan = dto.BackoutPlan ?? "";
        e.Outcome = string.IsNullOrEmpty(dto.Outcome) ? null : dto.Outcome;
        return null;
    }

    // Un changement standard suit un modèle déjà évalué : il est pré-autorisé.
    protected override string InitialStatus(Change e) => e.ChangeType == "Standard" ? "Autorisé" : "Demandé";

    protected override bool RequiresManager(string status) => status is "Autorisé" or "Rejeté";

    // Un changement rejeté est terminé : il n'est ni planifié, ni mis en œuvre, ni clos.
    protected override string? CheckStatus(Change e, string status)
    {
        if (status is "Planifié" or "Mis en œuvre" or "Clos" && e.AuthorizedAt is null)
            return "Le changement doit d'abord être autorisé par un gestionnaire.";
        if (status == "Planifié" && e.PlannedStart is null)
            return "Renseigner le début planifié pour inscrire le changement au calendrier.";
        if (status == "Clos" && e.Outcome is null)
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

    // Le registre n'affiche pas les plans : ils restent dans la fiche.
    protected override async Task<IEnumerable<object>> ListItems(IQueryable<Change> q) =>
        await q.Select(c => new
        {
            c.Id, c.Reference, c.Title, c.Status, c.ChangeType, c.Risk, c.PlannedStart, c.PlannedEnd,
            c.OwnerId, c.OwnerDisplayName, c.CreatedAt
        }).ToListAsync();

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
