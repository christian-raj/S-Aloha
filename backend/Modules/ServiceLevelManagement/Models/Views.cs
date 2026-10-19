namespace SAloha.Api.Modules.ServiceLevelManagement;

// Réponses de l'API (A2, ADR-0016) : DTO explicites, noms de champs inchangés pour le frontend.
// Un champ ajouté à une entité n'est exposé qu'en l'ajoutant ici.

/// <summary>Service cité par un accord : de quoi l'afficher et y mener.</summary>
public record ServiceSummary(int Id, string Reference, string Title, string Status)
{
    public static ServiceSummary? Of(ItService? e) => e is null ? null : new(e.Id, e.Reference, e.Title, e.Status);
}

public record ItServiceView(
    string? Criticality,
    string? ServiceHours,
    string? WorkingDays,
    string? OpeningTime,
    string? ClosingTime,
    string? Holidays,
    string? TimeZone,
    IReadOnlyList<ServiceLevelAgreementView> Agreements,
    int Id,
    int PerimeterId,
    string? Reference,
    string? Title,
    string? Description,
    string? Status,
    string? OwnerType,
    string? OwnerId,
    string? OwnerDisplayName,
    string? CreatedBy,
    string? CreatedByDisplayName,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static ItServiceView From(ItService e) => new(
        e.Criticality,
        e.ServiceHours,
        e.WorkingDays,
        e.OpeningTime,
        e.ClosingTime,
        e.Holidays,
        e.TimeZone,
        e.Agreements.Select(ServiceLevelAgreementView.From).ToList(),
        e.Id,
        e.PerimeterId,
        e.Reference,
        e.Title,
        e.Description,
        e.Status,
        e.OwnerType,
        e.OwnerId,
        e.OwnerDisplayName,
        e.CreatedBy,
        e.CreatedByDisplayName,
        e.CreatedAt,
        e.UpdatedAt);
}

public record ServiceLevelAgreementView(
    int ServiceId,
    ServiceSummary? Service,
    string? Customer,
    decimal? AvailabilityTarget,
    decimal? ComplianceTarget,
    int? ResolutionHoursP1,
    int? ResolutionHoursP2,
    int? ResolutionHoursP3,
    int? ResolutionHoursP4,
    int? ResponseHoursP1,
    int? ResponseHoursP2,
    int? ResponseHoursP3,
    int? ResponseHoursP4,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    DateTime? ReviewDate,
    IReadOnlyList<AgreementReviewView> Reviews,
    int Id,
    int PerimeterId,
    string? Reference,
    string? Title,
    string? Description,
    string? Status,
    string? OwnerType,
    string? OwnerId,
    string? OwnerDisplayName,
    string? CreatedBy,
    string? CreatedByDisplayName,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static ServiceLevelAgreementView From(ServiceLevelAgreement e) => new(
        e.ServiceId,
        ServiceSummary.Of(e.Service),
        e.Customer,
        e.AvailabilityTarget,
        e.ComplianceTarget,
        e.ResolutionHoursP1,
        e.ResolutionHoursP2,
        e.ResolutionHoursP3,
        e.ResolutionHoursP4,
        e.ResponseHoursP1,
        e.ResponseHoursP2,
        e.ResponseHoursP3,
        e.ResponseHoursP4,
        e.ValidFrom,
        e.ValidTo,
        e.ReviewDate,
        e.Reviews.Select(AgreementReviewView.From).ToList(),
        e.Id,
        e.PerimeterId,
        e.Reference,
        e.Title,
        e.Description,
        e.Status,
        e.OwnerType,
        e.OwnerId,
        e.OwnerDisplayName,
        e.CreatedBy,
        e.CreatedByDisplayName,
        e.CreatedAt,
        e.UpdatedAt);
}

public record AgreementReviewView(
    int Id,
    int AgreementId,
    DateTime ReviewedAt,
    string? ReviewedBy,
    string? Findings,
    string? Decisions,
    DateTime? NextReviewDate)
{
    public static AgreementReviewView From(AgreementReview e) => new(
        e.Id,
        e.AgreementId,
        e.ReviewedAt,
        e.ReviewedBy,
        e.Findings,
        e.Decisions,
        e.NextReviewDate);
}
