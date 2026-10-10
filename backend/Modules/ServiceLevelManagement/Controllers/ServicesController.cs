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

    protected override IQueryable<ServiceLevelAgreement> Filter(IQueryable<ServiceLevelAgreement> q) =>
        q.Include(a => a.Service);

    protected override IQueryable<ServiceLevelAgreement> WithDetails(IQueryable<ServiceLevelAgreement> q) =>
        q.Include(a => a.Service);
}
