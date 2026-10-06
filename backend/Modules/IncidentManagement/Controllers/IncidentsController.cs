using Microsoft.AspNetCore.Mvc;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Itil;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.IncidentManagement;

[Route("api/incidents")]
public class IncidentsController(AppDbContext db) : RecordController<Incident, IncidentDto>(db)
{
    protected override string Code => "INC";
    protected override string LinkType => "incident";
    protected override string[] Statuses => Incident.Statuses;

    protected override string? Apply(Incident e, IncidentDto dto, bool creating)
    {
        var error = Allowed.Check("Impact", dto.Impact, Priority.Impacts)
                    ?? Allowed.Check("Urgence", dto.Urgency, Priority.Urgencies);
        if (error is not null) return error;
        e.Impact = dto.Impact!; e.Urgency = dto.Urgency!;
        e.Priority = Priority.Compute(e.Impact, e.Urgency);
        e.Category = dto.Category ?? ""; e.AffectedService = dto.AffectedService ?? "";
        e.IsMajor = dto.IsMajor;
        e.Resolution = dto.Resolution;
        return null;
    }

    protected override string? CheckStatus(Incident e, string status) =>
        Incident.Done.Contains(status) && string.IsNullOrWhiteSpace(e.Resolution)
            ? "Décrire la résolution avant de résoudre ou clore l'incident." : null;

    protected override void OnStatusChanged(Incident e, string from)
    {
        // Rouvert, un incident n'est plus résolu : ses dates sont effacées
        // pour ne pas fausser le MTTR (même règle que les problèmes, M4).
        e.ResolvedAt = Incident.Done.Contains(e.Status) ? e.ResolvedAt ?? DateTime.UtcNow : null;
        e.ClosedAt = e.Status == "Clos" ? DateTime.UtcNow : null;
    }
}
