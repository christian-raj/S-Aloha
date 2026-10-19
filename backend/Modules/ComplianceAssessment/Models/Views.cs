namespace SAloha.Api.Modules.ComplianceAssessment;

// Réponses de l'API (A2, ADR-0016) : DTO explicites, noms de champs inchangés pour le frontend.
// Un champ ajouté à une entité n'est exposé qu'en l'ajoutant ici.

public record AssessmentView(
    string? EntityCategory,
    string? ValidatedBy,
    DateTime? ValidatedAt,
    DateTime? NextAssessmentDate,
    string? ReferentialVersion,
    IReadOnlyList<AssessmentResponseView> Responses,
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
    public static AssessmentView From(Assessment e) => new(
        e.EntityCategory,
        e.ValidatedBy,
        e.ValidatedAt,
        e.NextAssessmentDate,
        e.ReferentialVersion,
        e.Responses.Select(AssessmentResponseView.From).ToList(),
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

public record AssessmentResponseView(
    int Id,
    int AssessmentId,
    int RequirementId,
    int? Score,
    bool NotApplicable,
    string? Justification,
    string? UpdatedBy,
    DateTime UpdatedAt)
{
    public static AssessmentResponseView From(AssessmentResponse e) => new(
        e.Id,
        e.AssessmentId,
        e.RequirementId,
        e.Score,
        e.NotApplicable,
        e.Justification,
        e.UpdatedBy,
        e.UpdatedAt);
}

public record AssessmentEvidenceView(
    int Id,
    int AssessmentId,
    int RequirementId,
    string? TargetType,
    int TargetId,
    string? Reference,
    string? Title,
    string? CreatedBy,
    DateTime CreatedAt)
{
    public static AssessmentEvidenceView From(AssessmentEvidence e) => new(
        e.Id,
        e.AssessmentId,
        e.RequirementId,
        e.TargetType,
        e.TargetId,
        e.Reference,
        e.Title,
        e.CreatedBy,
        e.CreatedAt);
}
