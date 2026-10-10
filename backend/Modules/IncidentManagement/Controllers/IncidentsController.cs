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
        e.Impact = dto.Impact!; e.Urgency = dto.Urgency!;
        e.Priority = Priority.Compute(e.Impact, e.Urgency);
        e.Category = dto.Category ?? ""; e.AffectedService = dto.AffectedService ?? "";
        e.IsMajor = dto.IsMajor;
        e.Resolution = dto.Resolution;
        e.ResolutionCode = string.IsNullOrEmpty(dto.ResolutionCode) ? null : dto.ResolutionCode;
        return null;
    }

    protected override async Task<string?> CheckStatusAsync(Incident e, string status)
    {
        // INC-06 : la prise en charge suppose un responsable, utilisateur ou groupe.
        if (status == "En cours" && string.IsNullOrWhiteSpace(e.OwnerId))
            return "Désigner un responsable (utilisateur ou groupe) pour prendre l'incident en charge.";
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
        // Rouvert, un incident n'est plus résolu : ses dates sont effacées
        // pour ne pas fausser le MTTR (même règle que les problèmes, M4).
        e.ResolvedAt = Incident.Done.Contains(e.Status) ? e.ResolvedAt ?? DateTime.UtcNow : null;
        // Première prise en charge : posée une fois, jamais effacée (délai de prise en charge, INC-21).
        if (e.Status == "En cours") e.FirstResponseAt ??= DateTime.UtcNow;
        e.ClosedAt = e.Status == "Clos" ? DateTime.UtcNow : null;
    }
}
