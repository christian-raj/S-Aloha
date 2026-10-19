namespace SAloha.Api.Modules.ProjectManagement;

// Réponses de l'API (A2, ADR-0016) : DTO explicites, noms de champs inchangés pour le frontend.
// Un champ ajouté à une entité n'est exposé qu'en l'ajoutant ici.

public record ProjectView(
    string? Method,
    string? Components,
    DateTime? StartDate,
    DateTime? TargetEndDate,
    decimal? Budget,
    decimal? ActualCost,
    string? Justification,
    string? ExpectedBenefits,
    string? ExpectedCosts,
    string? MajorRisks,
    int? ToleranceDays,
    decimal? ToleranceCostPercent,
    string? ToleranceScope,
    string? ToleranceQuality,
    DateTime? ClosedAt,
    string? ValidatedBy,
    DateTime? ValidatedAt,
    string? AbandonReason,
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
    public static ProjectView From(Project e) => new(
        e.Method,
        e.Components,
        e.StartDate,
        e.TargetEndDate,
        e.Budget,
        e.ActualCost,
        e.Justification,
        e.ExpectedBenefits,
        e.ExpectedCosts,
        e.MajorRisks,
        e.ToleranceDays,
        e.ToleranceCostPercent,
        e.ToleranceScope,
        e.ToleranceQuality,
        e.ClosedAt,
        e.ValidatedBy,
        e.ValidatedAt,
        e.AbandonReason,
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

public record ProjectPhaseView(
    string? Kind,
    string? Name,
    int Order,
    DateTime? PlannedStart,
    DateTime? PlannedEnd,
    string? Status,
    string? ApprovedBy,
    DateTime? ApprovedAt,
    int Id,
    int ProjectId,
    DateTime CreatedAt,
    string? CreatedBy)
{
    public static ProjectPhaseView From(ProjectPhase e) => new(
        e.Kind,
        e.Name,
        e.Order,
        e.PlannedStart,
        e.PlannedEnd,
        e.Status,
        e.ApprovedBy,
        e.ApprovedAt,
        e.Id,
        e.ProjectId,
        e.CreatedAt,
        e.CreatedBy);
}

public record MilestoneView(
    string? Name,
    int? PhaseId,
    DateTime? DueDate,
    DateTime? AchievedAt,
    int Id,
    int ProjectId,
    DateTime CreatedAt,
    string? CreatedBy)
{
    public static MilestoneView From(Milestone e) => new(
        e.Name,
        e.PhaseId,
        e.DueDate,
        e.AchievedAt,
        e.Id,
        e.ProjectId,
        e.CreatedAt,
        e.CreatedBy);
}

public record DeliverableView(
    string? Name,
    string? Description,
    int? PhaseId,
    DateTime? DueDate,
    string? Status,
    string? ValidatedBy,
    DateTime? ValidatedAt,
    int Id,
    int ProjectId,
    DateTime CreatedAt,
    string? CreatedBy)
{
    public static DeliverableView From(Deliverable e) => new(
        e.Name,
        e.Description,
        e.PhaseId,
        e.DueDate,
        e.Status,
        e.ValidatedBy,
        e.ValidatedAt,
        e.Id,
        e.ProjectId,
        e.CreatedAt,
        e.CreatedBy);
}

public record SprintView(
    string? Name,
    string? Goal,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Status,
    int Id,
    int ProjectId,
    DateTime CreatedAt,
    string? CreatedBy)
{
    public static SprintView From(Sprint e) => new(
        e.Name,
        e.Goal,
        e.StartDate,
        e.EndDate,
        e.Status,
        e.Id,
        e.ProjectId,
        e.CreatedAt,
        e.CreatedBy);
}

public record WorkItemView(
    string? Type,
    string? Title,
    string? Description,
    string? AcceptanceCriteria,
    int? Points,
    int Rank,
    int? SprintId,
    string? Status,
    string? AssigneeId,
    string? AssigneeDisplayName,
    DateTime? CompletedAt,
    int Id,
    int ProjectId,
    DateTime CreatedAt,
    string? CreatedBy)
{
    public static WorkItemView From(WorkItem e) => new(
        e.Type,
        e.Title,
        e.Description,
        e.AcceptanceCriteria,
        e.Points,
        e.Rank,
        e.SprintId,
        e.Status,
        e.AssigneeId,
        e.AssigneeDisplayName,
        e.CompletedAt,
        e.Id,
        e.ProjectId,
        e.CreatedAt,
        e.CreatedBy);
}

public record ProjectRiskView(
    string? Title,
    string? Description,
    int Probability,
    int Impact,
    int Score,
    string? Response,
    string? OwnerDisplayName,
    string? Status,
    int Id,
    int ProjectId,
    DateTime CreatedAt,
    string? CreatedBy)
{
    public static ProjectRiskView From(ProjectRisk e) => new(
        e.Title,
        e.Description,
        e.Probability,
        e.Impact,
        e.Score,
        e.Response,
        e.OwnerDisplayName,
        e.Status,
        e.Id,
        e.ProjectId,
        e.CreatedAt,
        e.CreatedBy);
}

public record ProjectIssueView(
    string? Type,
    string? Title,
    string? Description,
    string? Severity,
    string? Status,
    string? Resolution,
    DateTime? ClosedAt,
    int Id,
    int ProjectId,
    DateTime CreatedAt,
    string? CreatedBy)
{
    public static ProjectIssueView From(ProjectIssue e) => new(
        e.Type,
        e.Title,
        e.Description,
        e.Severity,
        e.Status,
        e.Resolution,
        e.ClosedAt,
        e.Id,
        e.ProjectId,
        e.CreatedAt,
        e.CreatedBy);
}
