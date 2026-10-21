using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Itil;
using Microsoft.AspNetCore.Mvc;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Records;
using SAloha.Api.Core.Notifications;
using SAloha.Api.Core.Links;

namespace SAloha.Api.Modules.ServiceRequestManagement;

[Route("api/requests")]
public class ServiceRequestsController(AppDbContext db) : RecordController<ServiceRequest, ServiceRequestDto, ServiceRequestView>(db)
{
    protected override ServiceRequestView View(ServiceRequest e) => ServiceRequestView.From(e);
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
        // REQ-07 : le bénéficiaire vaut le demandeur par défaut ; une demande pour autrui garde les deux.
        if (!string.IsNullOrWhiteSpace(dto.RequestedFor))
            (e.RequestedFor, e.RequestedForDisplayName) = (dto.RequestedFor.Trim(), dto.RequestedForDisplayName ?? dto.RequestedFor.Trim());
        else if (creating) (e.RequestedFor, e.RequestedForDisplayName) = (Me, MyDisplay);
        e.ContestReason = string.IsNullOrWhiteSpace(dto.ContestReason) ? null : dto.ContestReason.Trim();
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
        // REQ-10 : contestation d'une demande satisfaite — demandeur, bénéficiaire ou gestionnaire,
        // motivée, dans les 5 jours ouvrés.
        if (e.Status == "Satisfaite" && status == "En cours")
        {
            if (!IsManager && !string.Equals(e.CreatedBy, Me, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(e.RequestedFor, Me, StringComparison.OrdinalIgnoreCase))
                return "Seul le demandeur, le bénéficiaire ou un gestionnaire peut contester une demande satisfaite.";
            if (e.ContestReason is null) return "Motiver la contestation : ce qui n'est pas conforme à la demande.";
            if (e.FulfilledAt is { } at && DateTime.UtcNow > Dates.AddBusinessDays(at, ServiceRequest.ContestBusinessDays))
                return $"Délai de contestation dépassé ({ServiceRequest.ContestBusinessDays} jours ouvrés) : soumettre une nouvelle demande.";
        }
        return null;
    }

    // REQ-31 : soumission → approbateurs ; approbation, rejet, satisfaction → demandeur et bénéficiaire.
    protected override void OnNotify(ServiceRequest e, string? from, Notifier n)
    {
        // REQ-21 : une demande pré-approuvée n'attend aucun approbateur.
        if (from == "") { if (e.Status == "Soumise") n.ToManagers(e, LinkType, "Demande à approuver"); return; }
        if (from is null) return;
        // REQ-10 : une contestation → responsable.
        if (from == "Satisfaite" && e.Status == "En cours") { n.ToOwner(e, LinkType, "Demande contestée"); return; }
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
        // REQ-10 : le motif d'une contestation vaut jusqu'à la satisfaction suivante (l'historique le garde).
        if (e.Status == "Satisfaite") e.ContestReason = null;
    }

    /// <summary>
    /// REQ-12 : une demande qui révèle un besoin non standard se requalifie — un changement (ou
    /// un incident) est créé, relié à la demande, puis la demande est annulée avec ce motif.
    /// Mêmes droits et statuts que l'annulation (REQ-09) : demandeur ou gestionnaire, avant En cours.
    /// </summary>
    [HttpPost("{id:int}/requalify")]
    public async Task<IActionResult> Requalify(int id, RequalifyDto dto)
    {
        var e = await Db.ServiceRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (e is null || !await VisibleAsync(e)) return NotFound();
        await ScopeAsync();
        ActOn(e.PerimeterId);
        if (!IsManager && !string.Equals(e.CreatedBy, Me, StringComparison.OrdinalIgnoreCase)) return Forbid();
        if (dto.Target is not ("change" or "incident")) return BadRequest(new { message = "Requalifier en changement (« change ») ou en incident (« incident »)." });
        if (e.Status is not ("Soumise" or "Approuvée"))
            return BadRequest(new { message = $"Demande {e.Status.ToLowerInvariant()} : seule une demande Soumise ou Approuvée se requalifie." });
        var reason = dto.Reason?.Trim();
        if (string.IsNullOrEmpty(reason)) return BadRequest(new { message = "Motiver la requalification : ce qui rend le besoin non standard." });

        Core.Records.Record target = dto.Target == "change"
            ? await References.CreateAsync(Db, "CHG", () => new ChangeEnablement.Change
            {
                PerimeterId = e.PerimeterId, Title = e.Title, Description = $"{e.Description}\n\nRequalifié depuis {e.Reference} : {reason}".Trim(),
                Status = "Demandé", ChangeType = "Normal", Risk = "Moyen", CreatedBy = Me, CreatedByDisplayName = MyDisplay
            })
            : await References.CreateAsync(Db, "INC", () => new IncidentManagement.Incident
            {
                PerimeterId = e.PerimeterId, Title = e.Title, Description = $"{e.Description}\n\nRequalifié depuis {e.Reference} : {reason}".Trim(),
                Status = "Nouveau", Impact = "Moyen", Urgency = "Moyenne", Priority = Priority.Compute("Moyen", "Moyenne"),
                ServiceId = e.ServiceId, AffectedService = e.AffectedService, CreatedBy = Me, CreatedByDisplayName = MyDisplay
            });
        Db.ItemLinks.Add(new ItemLink { FromType = LinkType, FromId = e.Id, ToType = dto.Target, ToId = target.Id, CreatedBy = Me });
        var from = e.Status;
        e.Status = "Annulée";
        e.CancelReason = $"Requalifiée en {(dto.Target == "change" ? "changement" : "incident")} {target.Reference} : {reason}";
        if (e.CancelReason.Length > 2000) e.CancelReason = e.CancelReason[..2000];
        OnStatusChanged(e, from);
        e.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        var n = Notify;
        n.ToCreator(e, LinkType, "Demande requalifiée");
        if (dto.Target == "change") n.ToManagers(target, "change", "Changement demandé");
        await n.SaveAsync();
        return Ok(new { Request = View(e), Target = new { Type = dto.Target, target.Id, target.Reference } });
    }
}
