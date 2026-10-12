using Microsoft.AspNetCore.Mvc;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Itil;
using SAloha.Api.Core.Links;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.IncidentManagement;

[Route("api/incidents")]
public class IncidentsController(AppDbContext db) : RecordController<Incident, IncidentDto>(db)
{
    protected override string Code => "INC";
    protected override string LinkType => "incident";
    protected override string[] Statuses => Incident.Statuses;
    protected override IReadOnlyDictionary<string, string[]> Transitions => Incident.Transitions;

    protected override string? Apply(Incident e, IncidentDto dto, bool creating)
    {
        var error = Allowed.Check("Impact", dto.Impact, Priority.Impacts)
                    ?? Allowed.Check("Urgence", dto.Urgency, Priority.Urgencies)
                    ?? Allowed.Check("Code de résolution", dto.ResolutionCode, Incident.ResolutionCodes, optional: true);
        if (error is not null) return error;

        // INC-13 : seul un gestionnaire déclare ou retire le caractère majeur.
        if (creating && dto.IsMajor && !IsManager)
            return "Seul un gestionnaire peut déclarer un incident majeur.";
        if (!creating && dto.IsMajor != e.IsMajor && !IsManager)
            return "Seul un gestionnaire peut déclarer ou retirer le caractère majeur d'un incident.";

        e.Impact = dto.Impact!; e.Urgency = dto.Urgency!;
        e.IsMajor = dto.IsMajor;
        // INC-13 : un incident majeur a toujours priorité P1.
        e.Priority = e.IsMajor ? "P1" : Priority.Compute(e.Impact, e.Urgency);

        // INC-13 : un incident majeur a une personne pour responsable (pas un groupe),
        // désignée par un gestionnaire ; les autres champs restent modifiables par tous.
        if (e.IsMajor && e.OwnerType == "Group")
            return "Un incident majeur doit avoir un gestionnaire pour responsable (pas un groupe).";
        if (e.IsMajor && !IsManager && e.OwnerId != OriginalOwner(e, creating))
            return "Seul un gestionnaire désigne le responsable d'un incident majeur.";

        e.Category = dto.Category ?? ""; e.AffectedService = dto.AffectedService ?? "";
        e.Resolution = dto.Resolution;
        e.ResolutionCode = string.IsNullOrEmpty(dto.ResolutionCode) ? null : dto.ResolutionCode;
        e.ReopenReason = string.IsNullOrWhiteSpace(dto.ReopenReason) ? null : dto.ReopenReason.Trim();
        return null;
    }

    /// <summary>Responsable avant la modification (aucun à la création).</summary>
    private string? OriginalOwner(Incident e, bool creating) =>
        creating ? null : Db.Entry(e).Property(x => x.OwnerId).OriginalValue;

    protected override async Task<string?> CheckStatusAsync(Incident e, string status)
    {
        // INC-06 : la prise en charge suppose un responsable, utilisateur ou groupe.
        if (status == "En cours" && string.IsNullOrWhiteSpace(e.OwnerId))
            return "Désigner un responsable (utilisateur ou groupe) pour prendre l'incident en charge.";

        // INC-11 : réouverture d'un incident Résolu dans les 10 jours ouvrés, avec motif.
        if (e.Status == "Résolu" && status == "En cours")
        {
            if (e.ResolvedAt is not null && DateTime.UtcNow > Dates.AddBusinessDays(e.ResolvedAt.Value, 10))
                return "Délai de réouverture dépassé (10 jours ouvrés). Créer un nouvel incident et le relier à celui-ci.";
            if (string.IsNullOrWhiteSpace(e.ReopenReason))
                return "Motiver la réouverture de l'incident.";
        }

        if (!Incident.Done.Contains(status)) return null;
        if (string.IsNullOrWhiteSpace(e.Resolution))
            return "Décrire la résolution avant de résoudre ou clore l'incident.";
        // INC-08 : code de résolution ; un doublon est relié à l'incident conservé.
        if (e.ResolutionCode is null)
            return $"Choisir un code de résolution : {string.Join(", ", Incident.ResolutionCodes)}.";
        if (e.ResolutionCode == "Doublon" && !await ItemLinks.IsLinkedToAsync(Db, "incident", e.Id, "incident"))
            return "Un doublon doit être relié à l'incident conservé (onglet Liens).";
        return null;
    }

    protected override void OnStatusChanged(Incident e, string from)
    {
        // INC-11 : réouverture d'un incident résolu incrémente ReopenCount ; le motif
        // est effacé à la résolution suivante (l'historique le garde), pour qu'une
        // nouvelle réouverture en exige un nouveau.
        if (from == "Résolu" && e.Status == "En cours")
            e.ReopenCount++;
        if (e.Status == "Résolu") e.ReopenReason = null;

        // Rouvert, un incident n'est plus résolu : ses dates sont effacées
        // pour ne pas fausser le MTTR (même règle que les problèmes, M4).
        e.ResolvedAt = Incident.Done.Contains(e.Status) ? e.ResolvedAt ?? DateTime.UtcNow : null;
        // Première prise en charge : posée une fois, jamais effacée (délai de prise en charge, INC-21).
        if (e.Status == "En cours") e.FirstResponseAt ??= DateTime.UtcNow;
        e.ClosedAt = e.Status == "Clos" ? DateTime.UtcNow : null;
    }
}
