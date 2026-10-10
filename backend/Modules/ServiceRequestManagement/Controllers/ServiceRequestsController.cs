using Microsoft.AspNetCore.Mvc;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ServiceRequestManagement;

[Route("api/requests")]
public class ServiceRequestsController(AppDbContext db) : RecordController<ServiceRequest, ServiceRequestDto>(db)
{
    protected override string Code => "REQ";
    protected override string LinkType => "request";
    protected override string[] Statuses => ServiceRequest.Statuses;
    protected override IReadOnlyDictionary<string, string[]> Transitions => ServiceRequest.Transitions;

    protected override string? Apply(ServiceRequest e, ServiceRequestDto dto, bool creating)
    {
        if (string.IsNullOrWhiteSpace(dto.RequestedItem)) return "Préciser l'objet demandé.";
        e.RequestedItem = dto.RequestedItem.Trim();
        var hasBeneficiary = !string.IsNullOrWhiteSpace(dto.RequestedFor);
        e.RequestedFor = hasBeneficiary ? dto.RequestedFor : null;
        e.RequestedForDisplayName = hasBeneficiary ? dto.RequestedForDisplayName ?? dto.RequestedFor : null;
        e.DueDate = Dates.Utc(dto.DueDate);
        e.RejectionReason = string.IsNullOrWhiteSpace(dto.RejectionReason) ? null : dto.RejectionReason.Trim();
        return null;
    }

    protected override bool RequiresManager(string status) => status is "Approuvée" or "Rejetée";

    // Une demande rejetée est terminée : elle n'est ni traitée ni close.
    protected override string? CheckStatus(ServiceRequest e, string status) =>
        e.ApprovedAt is null && status is "En cours" or "Satisfaite" or "Close"
            ? "La demande doit d'abord être approuvée par un gestionnaire."
            // REQ-06 : un rejet se motive, le motif est communiqué au demandeur.
            : status == "Rejetée" && e.RejectionReason is null ? "Motiver le rejet : le motif est communiqué au demandeur."
            : null;

    protected override void OnStatusChanged(ServiceRequest e, string from)
    {
        if (e.Status == "Approuvée") { e.ApprovedAt = DateTime.UtcNow; e.ApprovedBy = MyDisplay; }
        else if (e.Status is "Soumise" or "Rejetée") { e.ApprovedAt = null; e.ApprovedBy = null; }
        e.FulfilledAt = e.Status is "Satisfaite" or "Close" && e.ApprovedAt is not null
            ? e.FulfilledAt ?? DateTime.UtcNow : null;
        e.ClosedAt = e.Status == "Close" ? DateTime.UtcNow : null;
    }
}
