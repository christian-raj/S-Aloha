namespace SAloha.Api.Modules.ChangeEnablement;

// Réponses de l'API (A2, ADR-0016) : DTO explicites, noms de champs inchangés pour le frontend.
// Un champ ajouté à une entité n'est exposé qu'en l'ajoutant ici.

public record ChangeView(
    string? ChangeType,
    string? Risk,
    DateTime? PlannedStart,
    DateTime? PlannedEnd,
    string? ImplementationPlan,
    string? BackoutPlan,
    string? Outcome,
    string? RejectionReason,
    string? AuthorizedBy,
    DateTime? AuthorizedAt,
    DateTime? ClosedAt,
    DateTime? ActualStart,
    DateTime? ActualEnd,
    string? PirNotes,
    string? ConflictComment,
    DateTime? ImplementedAt,
    string? FollowUp,
    int? StandardModelId,
    bool RequiresCab,
    bool Overrun,
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
    public static ChangeView From(Change e) => new(
        e.ChangeType,
        e.Risk,
        e.PlannedStart,
        e.PlannedEnd,
        e.ImplementationPlan,
        e.BackoutPlan,
        e.Outcome,
        e.RejectionReason,
        e.AuthorizedBy,
        e.AuthorizedAt,
        e.ClosedAt,
        e.ActualStart,
        e.ActualEnd,
        e.PirNotes,
        e.ConflictComment,
        e.ImplementedAt,
        e.FollowUp(DateTime.UtcNow),
        e.StandardModelId,
        e.RequiresCab,
        e.Overrun,
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

public record ChangeApprovalView(
    int Id,
    int ChangeId,
    string? Approver,
    string? ApproverDisplayName,
    bool Approved,
    string? Comment,
    DateTime At)
{
    public static ChangeApprovalView From(ChangeApproval e) => new(
        e.Id,
        e.ChangeId,
        e.Approver,
        e.ApproverDisplayName,
        e.Approved,
        e.Comment,
        e.At);
}

public record ChangeFreezeView(
    int Id,
    DateTime Start,
    DateTime End,
    string? Reason,
    string? CreatedBy,
    DateTime CreatedAt)
{
    public static ChangeFreezeView From(ChangeFreeze e) => new(
        e.Id,
        e.Start,
        e.End,
        e.Reason,
        e.CreatedBy,
        e.CreatedAt);
}

public record StandardChangeModelView(
    int Id,
    string? Name,
    string? Description,
    string? ImplementationPlan,
    string? BackoutPlan,
    string? Risk,
    bool Active,
    string? CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static StandardChangeModelView From(StandardChangeModel e) => new(
        e.Id,
        e.Name,
        e.Description,
        e.ImplementationPlan,
        e.BackoutPlan,
        e.Risk,
        e.Active,
        e.CreatedBy,
        e.CreatedAt,
        e.UpdatedAt);
}
