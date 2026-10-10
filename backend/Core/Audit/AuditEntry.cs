using System.ComponentModel.DataAnnotations;

namespace SAloha.Api.Core.Audit;

/// <summary>
/// Ligne du journal d'audit (SOC-20) : qui a fait quoi, quand, sur quel
/// enregistrement. L'enregistrement est désigné comme dans les liens (type et
/// identifiant) ; sa référence est recopiée pour rester lisible après suppression.
/// </summary>
public class AuditEntry
{
    public const string Creation = "Création";
    public const string Modification = "Modification";
    public const string Transition = "Transition";
    public const string ForcedTransition = "Transition forcée";
    public const string Deletion = "Suppression";
    public const string LinkAdded = "Lien ajouté";
    public const string LinkRemoved = "Lien retiré";

    public long Id { get; set; }
    [MaxLength(20)] public string EntityType { get; set; } = "";
    public int EntityId { get; set; }
    [MaxLength(20)] public string Reference { get; set; } = "";
    [MaxLength(30)] public string Action { get; set; } = "";
    /// <summary>Champ modifié (nom de propriété en camelCase, comme dans l'API), ou élément concerné.</summary>
    [MaxLength(100)] public string? Field { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    /// <summary>Motif saisi (transition forcée, plus tard réouverture, rejet…).</summary>
    public string? Reason { get; set; }
    [MaxLength(100)] public string Author { get; set; } = "";
    [MaxLength(200)] public string AuthorDisplayName { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}
