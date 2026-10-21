namespace SAloha.Api.Modules.ServiceRequestManagement;

// Réponses de l'API (A2, ADR-0016) : DTO explicites, noms de champs inchangés pour le frontend.
// Un champ ajouté à une entité n'est exposé qu'en l'ajoutant ici.

public record ServiceRequestView(
    string? RequestedItem,
    string? AffectedService,
    int? ServiceId,
    string? RequestedFor,
    string? RequestedForDisplayName,
    DateTime? DueDate,
    string? RejectionReason,
    string? ContestReason,
    string? CancelReason,
    string? ApprovedBy,
    DateTime? ApprovedAt,
    DateTime? FulfilledAt,
    DateTime? ClosedAt,
    DateTime? CancelledAt,
    int? CatalogItemId,
    bool Overdue,
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
    public static ServiceRequestView From(ServiceRequest e) => new(
        e.RequestedItem,
        e.AffectedService,
        e.ServiceId,
        e.RequestedFor,
        e.RequestedForDisplayName,
        e.DueDate,
        e.RejectionReason,
        e.ContestReason,
        e.CancelReason,
        e.ApprovedBy,
        e.ApprovedAt,
        e.FulfilledAt,
        e.ClosedAt,
        e.CancelledAt,
        e.CatalogItemId,
        e.Overdue,
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

public record RequestCatalogItemView(
    int Id,
    string? Name,
    string? Description,
    int? ServiceId,
    bool RequiresApproval,
    int? TargetHours,
    string? FulfillmentGroup,
    bool Active,
    string? CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static RequestCatalogItemView From(RequestCatalogItem e) => new(
        e.Id,
        e.Name,
        e.Description,
        e.ServiceId,
        e.RequiresApproval,
        e.TargetHours,
        e.FulfillmentGroup,
        e.Active,
        e.CreatedBy,
        e.CreatedAt,
        e.UpdatedAt);
}
