using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SAloha.Api.Core.Perimeters;

/// <summary>
/// Périmètre : une partie de l'organisation (direction, site, entité) à laquelle
/// se rattachent les enregistrements et les droits des gestionnaires et des
/// intervenants (docs/reference/perimetres.md, ADR-0014). Suppression douce (PER-01).
/// </summary>
public class Perimeter
{
    /// <summary>Périmètre de reprise : il reçoit tous les enregistrements existants (PER-10).</summary>
    public const int DefaultId = 1;
    public const string DefaultName = "Organisation";

    public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(500)] public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string CreatedBy { get; set; } = "";
    public DateTime? DeletedAt { get; set; }
    [MaxLength(100)] public string? DeletedBy { get; set; }
}

/// <summary>
/// Affectation d'un utilisateur ou d'un groupe AD à un périmètre, avec le rôle
/// Manager ou User (PER-02). Jamais d'Admin : sa portée est globale.
/// </summary>
public class PerimeterAssignment
{
    public static readonly string[] Roles = ["Manager", "User"];
    public static readonly string[] PrincipalTypes = ["User", "Group"];

    public int Id { get; set; }
    public int PerimeterId { get; set; }
    [JsonIgnore] public Perimeter? Perimeter { get; set; }
    [MaxLength(10)] public string PrincipalType { get; set; } = "User";     // User | Group
    [MaxLength(200)] public string PrincipalId { get; set; } = "";          // sAMAccountName ou CN du groupe
    [MaxLength(250)] public string PrincipalDisplayName { get; set; } = "";
    [MaxLength(10)] public string Role { get; set; } = "User";              // Manager | User
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string CreatedBy { get; set; } = "";
}

/// <summary>Enregistrement rattaché à un périmètre (PER-04).</summary>
public interface IHasPerimeter
{
    int PerimeterId { get; set; }
}

public record PerimeterDto(string Name, string? Description);
public record AssignmentDto(string PrincipalType, string PrincipalId, string? PrincipalDisplayName, string Role);
