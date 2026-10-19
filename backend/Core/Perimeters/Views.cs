namespace SAloha.Api.Core.Perimeters;

// Réponses de l'API (A2, ADR-0016) : DTO explicites, noms de champs inchangés pour le frontend.
// Un champ ajouté à une entité n'est exposé qu'en l'ajoutant ici.

public record PerimeterView(
    int Id,
    string? Name,
    string? Description,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? DeletedAt,
    string? DeletedBy)
{
    public static PerimeterView From(Perimeter e) => new(
        e.Id,
        e.Name,
        e.Description,
        e.CreatedAt,
        e.CreatedBy,
        e.DeletedAt,
        e.DeletedBy);
}

public record PerimeterAssignmentView(
    int Id,
    int PerimeterId,
    string? PrincipalType,
    string? PrincipalId,
    string? PrincipalDisplayName,
    string? Role,
    DateTime CreatedAt,
    string? CreatedBy)
{
    public static PerimeterAssignmentView From(PerimeterAssignment e) => new(
        e.Id,
        e.PerimeterId,
        e.PrincipalType,
        e.PrincipalId,
        e.PrincipalDisplayName,
        e.Role,
        e.CreatedAt,
        e.CreatedBy);
}
