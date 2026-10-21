namespace SAloha.Api.Modules.ProblemManagement;

// Réponses de l'API (A2, ADR-0016) : DTO explicites, noms de champs inchangés pour le frontend.
// Un champ ajouté à une entité n'est exposé qu'en l'ajoutant ici.

public record ProblemView(
    string? Impact,
    string? Urgency,
    string? Priority,
    string? Category,
    string? AffectedService,
    int? ServiceId,
    string? KnownErrorWorkaround,
    string? RootCause,
    string? Source,
    string? ClosureCode,
    DateTime? ResolvedAt,
    string? ReopenReason,
    DateTime? ClosedAt,
    IReadOnlyList<RcaAnalysisView> Analyses,
    IReadOnlyList<CorrectiveActionView> Actions,
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
    public static ProblemView From(Problem e) => new(
        e.Impact,
        e.Urgency,
        e.Priority,
        e.Category,
        e.AffectedService,
        e.ServiceId,
        e.KnownErrorWorkaround,
        e.RootCause,
        e.Source,
        e.ClosureCode,
        e.ResolvedAt,
        e.ReopenReason,
        e.ClosedAt,
        e.Analyses.Select(RcaAnalysisView.From).ToList(),
        e.Actions.Select(CorrectiveActionView.From).ToList(),
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

public record RcaAnalysisView(
    int Id,
    int ProblemId,
    string? Method,
    string? DataJson,
    string? Conclusion,
    string? CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ValidatedAt = null,
    string? ValidatedBy = null)
{
    public static RcaAnalysisView From(RcaAnalysis e) => new(
        e.Id,
        e.ProblemId,
        e.Method,
        e.DataJson,
        e.Conclusion,
        e.CreatedBy,
        e.CreatedAt,
        e.UpdatedAt,
        e.ValidatedAt,
        e.ValidatedBy);
}

public record CorrectiveActionView(
    int Id,
    int ProblemId,
    string? Title,
    string? Description,
    string? Status,
    DateTime? DueDate,
    DateTime? CompletedAt,
    DateTime? ApprovalRequestedAt,
    string? ApprovalRequestedBy,
    int? ChangeId,
    string? ChangeReference,
    string? CreatedBy,
    DateTime CreatedAt,
    IReadOnlyList<RaciAssignmentView> Raci)
{
    public static CorrectiveActionView From(CorrectiveAction e) => new(
        e.Id,
        e.ProblemId,
        e.Title,
        e.Description,
        e.Status,
        e.DueDate,
        e.CompletedAt,
        e.ApprovalRequestedAt,
        e.ApprovalRequestedBy,
        e.ChangeId,
        e.ChangeReference,
        e.CreatedBy,
        e.CreatedAt,
        e.Raci.Select(RaciAssignmentView.From).ToList());
}

public record RaciAssignmentView(
    int Id,
    int CorrectiveActionId,
    string? Role,
    string? AssigneeType,
    string? AssigneeId,
    string? AssigneeDisplayName)
{
    public static RaciAssignmentView From(RaciAssignment e) => new(
        e.Id,
        e.CorrectiveActionId,
        e.Role,
        e.AssigneeType,
        e.AssigneeId,
        e.AssigneeDisplayName);
}
