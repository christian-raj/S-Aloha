using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Itil;
using SAloha.Api.Core.Links;
using SAloha.Api.Core.Records;
using SAloha.Api.Core.Notifications;

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
                    ?? Allowed.Check("Code de résolution", dto.ResolutionCode, Incident.ResolutionCodes, optional: true)
                    ?? Allowed.Check("Motif d'attente", dto.OnHoldReason, Incident.OnHoldReasons, optional: true);
        if (error is not null) return error;

        // INC-13 : seul un gestionnaire déclare ou retire le caractère majeur.
        if (creating && dto.IsMajor && !IsManager)
            return "Seul un gestionnaire peut déclarer un incident majeur.";
        if (!creating && dto.IsMajor != e.IsMajor && !IsManager)
            return "Seul un gestionnaire peut déclarer ou retirer le caractère majeur d'un incident.";

        e.Impact = dto.Impact!; e.Urgency = dto.Urgency!;
        _becameMajor = dto.IsMajor && (creating || !e.IsMajor);
        e.IsMajor = dto.IsMajor;
        // INC-13 : un incident majeur a toujours priorité P1.
        e.Priority = e.IsMajor ? "P1" : Priority.Compute(e.Impact, e.Urgency);

        // INC-13 : un incident majeur a une personne pour responsable (pas un groupe),
        // désignée par un gestionnaire ; les autres champs restent modifiables par tous.
        if (e.IsMajor && e.OwnerType == "Group")
            return "Un incident majeur doit avoir un gestionnaire pour responsable (pas un groupe).";
        if (e.IsMajor && !IsManager && e.OwnerId != OriginalOwner(e, creating))
            return "Seul un gestionnaire désigne le responsable d'un incident majeur.";

        e.Category = dto.Category ?? ""; e.AffectedService = dto.AffectedService ?? ""; e.ServiceId = dto.ServiceId;
        e.Resolution = dto.Resolution;
        e.ResolutionCode = string.IsNullOrEmpty(dto.ResolutionCode) ? null : dto.ResolutionCode;
        e.ReopenReason = string.IsNullOrWhiteSpace(dto.ReopenReason) ? null : dto.ReopenReason.Trim();
        e.OnHoldReason = string.IsNullOrEmpty(dto.OnHoldReason) ? null : dto.OnHoldReason;
        // INC-27 : début constaté, jamais postérieur à la déclaration.
        e.OccurredAt = Dates.Utc(dto.OccurredAt);
        if (e.OccurredAt > (creating ? DateTime.UtcNow : e.CreatedAt).AddMinutes(5))
            return "Le début constaté ne peut pas être postérieur à la déclaration de l'incident.";
        // INC-23 : un changement de responsable exclut la résolution au premier contact.
        if (!creating && e.OwnerId != OriginalOwner(e, creating)) e.OwnerChanged = true;
        return null;
    }

    // SLM-10, INC-20 : service du catalogue (vérifié ici, recopié avant l'écriture).
    protected override Task<string?> ValidateAsync(Incident e) => ServiceCatalog.ResolveAsync(Db, e);

    /// <summary>Responsable avant la modification (aucun à la création).</summary>
    private string? OriginalOwner(Incident e, bool creating) =>
        creating ? null : Db.Entry(e).Property(x => x.OwnerId).OriginalValue;

    protected override async Task<string?> CheckStatusAsync(Incident e, string status)
    {
        // INC-07 : l'attente est motivée (motif fermé) ; vérifié à l'entrée En attente, pour qu'un
        // incident déjà en attente avant la règle reste modifiable.
        if (status == "En attente" && e.Status != "En attente" && e.OnHoldReason is null)
            return $"Choisir le motif de l'attente : {string.Join(", ", Incident.OnHoldReasons)}.";

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

    /// <summary>INC-21 : échéance recalculée à la création et quand la priorité ou le service change.</summary>
    protected override async Task BeforeSaveAsync(Incident e, bool creating)
    {
        await ServiceCatalog.ResolveAsync(Db, e);   // SLM-10, INC-20
        var entry = Db.Entry(e);
        // INC-22 : l'horloge SLA s'arrête En attente ; à la reprise, l'attente (en heures de
        // service) prolonge les échéances. Le motif ne vaut que pendant l'attente (INC-07).
        var from = creating ? null : entry.Property(x => x.Status).OriginalValue;
        var resumed = false;
        if (e.Status == "En attente" && from != "En attente") e.OnHoldSince = DateTime.UtcNow;
        else if (from == "En attente" && e.Status != "En attente")
        {
            if (e.OnHoldSince is { } since)
            {
                var held = (await IncidentSla.CalendarAsync(Db, e.ServiceId)).Between(since, DateTime.UtcNow);
                e.HeldMinutes += (int)Math.Round(held.TotalMinutes);
            }
            e.OnHoldSince = null; resumed = true;
        }
        if (e.Status != "En attente") e.OnHoldReason = null;
        if (creating || resumed || entry.Property(x => x.Priority).IsModified || entry.Property(x => x.AffectedService).IsModified
            || entry.Property(x => x.ServiceId).IsModified || e.ResolutionDueAt is null)
        {
            e.ResolutionDueAt = await IncidentSla.DueAsync(Db, e);
            e.ResponseDueAt = await IncidentSla.ResponseDueAsync(Db, e);
        }
    }

    private bool _becameMajor;

    // INC-25 : incident majeur déclaré → gestionnaires ; résolution → déclarant.
    protected override void OnNotify(Incident e, string? from, Notifier n)
    {
        if (_becameMajor) n.ToManagers(e, LinkType, "Incident majeur déclaré");
        if (from is not null && e.Status == "Résolu") n.ToCreator(e, LinkType, "Incident résolu");
    }

    protected override void OnStatusChanged(Incident e, string from)
    {
        // INC-23 : fixé à la première résolution ; une réouverture l'annule.
        if (Incident.Done.Contains(e.Status) && !Incident.Done.Contains(from))
            e.FirstContactResolution = e.FirstContactResolution ?? (e.FirstResponseAt is null && !e.OwnerChanged && e.ReopenCount == 0);
        if (from == "Résolu" && e.Status == "En cours") e.FirstContactResolution = false;
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
