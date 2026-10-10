using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ServiceLevelManagement;

/// <summary>Catalogue des services : référentiel tenu par les gestionnaires.</summary>
[Route("api/services")]
public class ServicesController(AppDbContext db) : RecordController<ItService, ItServiceDto>(db)
{
    protected override string Code => "SVC";
    protected override string LinkType => "service";
    protected override string[] Statuses => ItService.Statuses;
    protected override IReadOnlyDictionary<string, string[]> Transitions => ItService.Transitions;
    protected override bool ManagerOnly => true;
    protected override bool StatusChosenAtCreation => true;

    // SLM-11 : un service en service a un propriétaire (responsable du service).
    protected override string? CheckStatus(ItService e, string status) =>
        status == "En service" && string.IsNullOrWhiteSpace(e.OwnerId)
            ? "Désigner le responsable du service : il est obligatoire pour un service en service." : null;

    protected override string? Apply(ItService e, ItServiceDto dto, bool creating)
    {
        var error = Allowed.Check("Criticité", dto.Criticality, ItService.Criticalities);
        if (error is not null) return error;
        e.Criticality = dto.Criticality!;
        e.ServiceHours = dto.ServiceHours ?? "";
        return null;
    }

    protected override IQueryable<ItService> WithDetails(IQueryable<ItService> q) => q.Include(s => s.Agreements);
}

/// <summary>Accords de niveau de service (SLA), rattachés à un service du catalogue.</summary>
[Route("api/agreements")]
public class AgreementsController(AppDbContext db) : RecordController<ServiceLevelAgreement, ServiceLevelAgreementDto>(db)
{
    protected override string Code => "SLA";
    protected override string LinkType => "agreement";
    protected override string[] Statuses => ServiceLevelAgreement.Statuses;
    protected override IReadOnlyDictionary<string, string[]> Transitions => ServiceLevelAgreement.Transitions;
    protected override bool ManagerOnly => true;

    protected override string? Apply(ServiceLevelAgreement e, ServiceLevelAgreementDto dto, bool creating)
    {
        if (dto.AvailabilityTarget is < 0 or > 100) return "La disponibilité cible est un pourcentage (0 à 100).";
        int?[] hours = [dto.ResolutionHoursP1, dto.ResolutionHoursP2, dto.ResolutionHoursP3, dto.ResolutionHoursP4];
        if (hours.Any(h => h is <= 0)) return "Les délais de résolution sont des nombres d'heures positifs.";
        if (dto.ValidFrom is not null && dto.ValidTo is not null && dto.ValidTo < dto.ValidFrom)
            return "La fin de validité précède son début.";
        e.ServiceId = dto.ServiceId;
        e.Customer = dto.Customer ?? "";
        e.AvailabilityTarget = dto.AvailabilityTarget;
        e.ResolutionHoursP1 = dto.ResolutionHoursP1; e.ResolutionHoursP2 = dto.ResolutionHoursP2;
        e.ResolutionHoursP3 = dto.ResolutionHoursP3; e.ResolutionHoursP4 = dto.ResolutionHoursP4;
        e.ValidFrom = Dates.Utc(dto.ValidFrom); e.ValidTo = Dates.Utc(dto.ValidTo);
        e.ReviewDate = Dates.Utc(dto.ReviewDate);
        return null;
    }

    protected override async Task<string?> ValidateAsync(ServiceLevelAgreement e) =>
        await Db.Services.AnyAsync(s => s.Id == e.ServiceId) ? null : "Choisir un service du catalogue.";

    /// <summary>
    /// SLM-03 : un accord en vigueur a des cibles complètes et croissantes
    /// (P1 ≤ P2 ≤ P3 ≤ P4), une date de début, un service en service, et il est
    /// le seul en vigueur pour ce service et ce client.
    /// </summary>
    protected override async Task<string?> CheckStatusAsync(ServiceLevelAgreement e, string status)
    {
        if (status != "En vigueur") return null;
        int?[] hours = [e.ResolutionHoursP1, e.ResolutionHoursP2, e.ResolutionHoursP3, e.ResolutionHoursP4];
        if (hours.Any(h => h is null))
            return "Renseigner les délais de résolution P1 à P4 pour mettre l'accord en vigueur.";
        for (var i = 1; i < hours.Length; i++)
            if (hours[i] < hours[i - 1])
                return $"Les délais de résolution doivent croître de P1 à P4 (P{i} : {hours[i - 1]} h, P{i + 1} : {hours[i]} h).";
        if (e.ValidFrom is null) return "Renseigner la date de début de validité.";
        if (!await Db.Services.AnyAsync(s => s.Id == e.ServiceId && s.Status == "En service"))
            return "Le service de l'accord doit être en service.";
        var customer = e.Customer.ToLower();
        var other = await Db.Agreements.AsNoTracking()
            .Where(a => a.Id != e.Id && a.Status == "En vigueur" && a.ServiceId == e.ServiceId && a.Customer.ToLower() == customer)
            .Select(a => a.Reference).FirstOrDefaultAsync();
        return other is null ? null : $"Un accord est déjà en vigueur pour ce service et ce client ({other}).";
    }

    protected override IQueryable<ServiceLevelAgreement> Filter(IQueryable<ServiceLevelAgreement> q) =>
        q.Include(a => a.Service);

    protected override IQueryable<ServiceLevelAgreement> WithDetails(IQueryable<ServiceLevelAgreement> q) =>
        q.Include(a => a.Service);
}
