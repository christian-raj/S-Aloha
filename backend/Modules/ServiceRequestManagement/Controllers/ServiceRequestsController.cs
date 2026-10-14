using Microsoft.EntityFrameworkCore;
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
        // REQ-20 : le modèle se choisit à la création ; il donne l'objet demandé et le service.
        if (creating) e.CatalogItemId = dto.CatalogItemId;
        if (string.IsNullOrWhiteSpace(dto.RequestedItem) && e.CatalogItemId is null) return "Préciser l'objet demandé, ou choisir un modèle du catalogue.";
        e.RequestedItem = dto.RequestedItem?.Trim() ?? "";
        var hasBeneficiary = !string.IsNullOrWhiteSpace(dto.RequestedFor);
        e.RequestedFor = hasBeneficiary ? dto.RequestedFor : null;
        e.RequestedForDisplayName = hasBeneficiary ? dto.RequestedForDisplayName ?? dto.RequestedFor : null;
        // REQ-22 : l'échéance d'une demande issue d'un modèle à délai cible est calculée ; seul un
        // gestionnaire la modifie ensuite. Sans délai cible, l'échéance souhaitée reste libre.
        var due = Dates.Utc(dto.DueDate);
        if (creating || e.CatalogItemId is null) e.DueDate = due;
        else if (due != e.DueDate)
        {
            if (!IsManager) return "Seul un gestionnaire modifie l'échéance d'une demande du catalogue.";
            e.DueDate = due;
        }
        e.RejectionReason = string.IsNullOrWhiteSpace(dto.RejectionReason) ? null : dto.RejectionReason.Trim();
        e.ServiceId = dto.ServiceId; e.AffectedService = dto.AffectedService ?? "";
        return null;
    }

    // SLM-10 : service du catalogue (vérifié ici, recopié sur l'enregistrement juste avant l'écriture).
    protected override async Task<string?> ValidateAsync(ServiceRequest e)
    {
        var item = await ItemAsync(e);
        if (e.CatalogItemId is not null && item is null) return "Choisir un modèle du catalogue de demandes.";
        if (e.Id == 0 && item is { Active: false }) return $"Le modèle « {item.Name} » n'est plus proposé.";
        _preApproved = e.Id == 0 && item is { RequiresApproval: false };
        FromItem(e, item);
        return await ServiceCatalog.ResolveAsync(Db, e);
    }

    private bool _preApproved;

    private async Task<RequestCatalogItem?> ItemAsync(ServiceRequest e) =>
        e.CatalogItemId is int id ? await Db.RequestCatalogItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) : null;

    /// <summary>REQ-20 : objet demandé et service repris du modèle quand la demande ne les précise pas.</summary>
    private static void FromItem(ServiceRequest e, RequestCatalogItem? item)
    {
        if (item is null) return;
        if (string.IsNullOrWhiteSpace(e.RequestedItem)) e.RequestedItem = item.Name;
        if (e.ServiceId is null && string.IsNullOrWhiteSpace(e.AffectedService)) e.ServiceId = item.ServiceId;
    }

    // REQ-21 : un modèle sans approbation crée la demande directement Approuvée.
    protected override string InitialStatus(ServiceRequest e) => _preApproved ? "Approuvée" : base.InitialStatus(e);

    /// <summary>
    /// À l'approbation (ou à la création pré-approuvée) : échéance = maintenant + délai cible du
    /// modèle, en heures de service de son service (REQ-22, SLM-14) ; le groupe d'exécution du
    /// modèle devient responsable, s'il n'y en a pas (REQ-23).
    /// </summary>
    protected override async Task BeforeSaveAsync(ServiceRequest e, bool creating)
    {
        var item = await ItemAsync(e);
        FromItem(e, item);
        await ServiceCatalog.ResolveAsync(Db, e);
        var approvedNow = e.Status == "Approuvée" && (creating || Db.Entry(e).Property(x => x.Status).OriginalValue != "Approuvée");
        if (item is null || !approvedNow) return;
        if (item.TargetHours is int hours)
        {
            var calendar = item.ServiceId is int sid
                ? (await Db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid))?.Calendar() ?? ServiceCalendar.AlwaysOpen
                : ServiceCalendar.AlwaysOpen;
            e.DueDate = calendar.Add(DateTime.UtcNow, TimeSpan.FromHours(hours));
        }
        if (string.IsNullOrWhiteSpace(e.OwnerId) && item.FulfillmentGroup is { } group)
        {
            e.OwnerType = "Group"; e.OwnerId = group; e.OwnerDisplayName = group;
        }
    }

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
        // REQ-21 : une demande pré-approuvée n'attend aucun approbateur.
        if (from == "") { if (e.Status == "Soumise") n.ToManagers(e, LinkType, "Demande à approuver"); return; }
        if (from is null) return;
        var ev = e.Status switch { "Approuvée" => "Demande approuvée", "Rejetée" => "Demande rejetée", "Satisfaite" => "Demande satisfaite", _ => null };
        if (ev is null) return;
        n.ToCreator(e, LinkType, ev);
        n.ToUser(e, LinkType, e.RequestedFor, ev);
    }

    protected override void OnStatusChanged(ServiceRequest e, string from)
    {
        if (e.Status == "Approuvée") { e.ApprovedAt = DateTime.UtcNow; e.ApprovedBy = from == "" && _preApproved ? "Modèle pré-approuvé" : MyDisplay; }
        else if (e.Status is "Soumise" or "Rejetée") { e.ApprovedAt = null; e.ApprovedBy = null; }
        e.FulfilledAt = e.Status is "Satisfaite" or "Close" && e.ApprovedAt is not null
            ? e.FulfilledAt ?? DateTime.UtcNow : null;
        e.ClosedAt = e.Status == "Close" ? DateTime.UtcNow : null;
        e.CancelledAt = e.Status == "Annulée" ? DateTime.UtcNow : null;
    }
}
