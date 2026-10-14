using SAloha.Api.Core.Itil;
using Microsoft.AspNetCore.Mvc;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Records;
using SAloha.Api.Core.Notifications;

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
        e.ServiceId = dto.ServiceId; e.AffectedService = dto.AffectedService ?? "";
        return null;
    }

    // SLM-10 : service du catalogue (vérifié ici, recopié sur l'enregistrement juste avant l'écriture).
    protected override Task<string?> ValidateAsync(ServiceRequest e) => ServiceCatalog.ResolveAsync(Db, e);
    protected override Task BeforeSaveAsync(ServiceRequest e, bool creating) => ServiceCatalog.ResolveAsync(Db, e);

    protected override bool RequiresManager(string status) => status is "Approuvée" or "Rejetée";

    // Une demande rejetée est terminée : elle n'est ni traitée ni close.
    protected override string? CheckStatus(ServiceRequest e, string status)
    {
        if (e.ApprovedAt is null && status is "En cours" or "Satisfaite" or "Close")
            return "La demande doit d'abord être approuvée par un gestionnaire.";
        // REQ-06 : un rejet se motive, le motif est communiqué au demandeur.
        if (status == "Rejetée" && e.RejectionReason is null)
            return "Motiver le rejet : le motif est communiqué au demandeur.";
        // REQ-09 : annulation par le demandeur ou un gestionnaire.
        if (status == "Annulée" && !IsManager && !string.Equals(e.CreatedBy, Me, StringComparison.OrdinalIgnoreCase))
            return "Seul le demandeur ou un gestionnaire peut annuler la demande.";
        return null;
    }

    // REQ-31 : soumission → approbateurs ; approbation, rejet, satisfaction → demandeur et bénéficiaire.
    protected override void OnNotify(ServiceRequest e, string? from, Notifier n)
    {
        if (from == "") { n.ToManagers(e, LinkType, "Demande à approuver"); return; }
        if (from is null) return;
        var ev = e.Status switch { "Approuvée" => "Demande approuvée", "Rejetée" => "Demande rejetée", "Satisfaite" => "Demande satisfaite", _ => null };
        if (ev is null) return;
        n.ToCreator(e, LinkType, ev);
        n.ToUser(e, LinkType, e.RequestedFor, ev);
    }

    protected override void OnStatusChanged(ServiceRequest e, string from)
    {
        if (e.Status == "Approuvée") { e.ApprovedAt = DateTime.UtcNow; e.ApprovedBy = MyDisplay; }
        else if (e.Status is "Soumise" or "Rejetée") { e.ApprovedAt = null; e.ApprovedBy = null; }
        e.FulfilledAt = e.Status is "Satisfaite" or "Close" && e.ApprovedAt is not null
            ? e.FulfilledAt ?? DateTime.UtcNow : null;
        e.ClosedAt = e.Status == "Close" ? DateTime.UtcNow : null;
        e.CancelledAt = e.Status == "Annulée" ? DateTime.UtcNow : null;
    }
}
