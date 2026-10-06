using System.ComponentModel.DataAnnotations;
using System.Reflection;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Core.Records;

/// <summary>
/// Socle commun des enregistrements de processus (incident, demande,
/// changement, CI…) : référence, titre, statut, responsable AD et traçabilité.
/// Classe non mappée : chaque processus a sa propre table.
/// </summary>
public abstract class Record : IHasReference
{
    public int Id { get; set; }
    [MaxLength(20)] public string Reference { get; set; } = "";
    [MaxLength(200)] public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    [MaxLength(30)] public string Status { get; set; } = "";
    // Responsable (assigné, propriétaire…) : utilisateur ou groupe AD.
    [MaxLength(10)] public string? OwnerType { get; set; }          // User | Group
    [MaxLength(200)] public string? OwnerId { get; set; }           // sAMAccountName ou nom du groupe
    [MaxLength(250)] public string? OwnerDisplayName { get; set; }
    [MaxLength(100)] public string CreatedBy { get; set; } = "";
    [MaxLength(200)] public string CreatedByDisplayName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Champs communs des DTO de création et de modification.</summary>
public interface IRecordDto
{
    string Title { get; }
    string? Description { get; }
    string? Status { get; }
    string? OwnerType { get; }
    string? OwnerId { get; }
    string? OwnerDisplayName { get; }
}

/// <summary>Contrôle des valeurs fermées (statuts, types…) : message d'erreur ou null.</summary>
public static class Allowed
{
    public static string? Check(string label, string? value, IReadOnlyCollection<string> allowed, bool optional = false) =>
        (optional && string.IsNullOrEmpty(value)) || (value is not null && allowed.Contains(value))
            ? null
            : $"{label} invalide : « {value} ». Valeurs admises : {string.Join(", ", allowed)}.";
}

/// <summary>
/// Les colonnes de date sont en <c>timestamptz</c> : Npgsql refuse une date
/// sans fuseau. Une date reçue sans fuseau (« 2026-10-12 ») est lue en UTC.
/// </summary>
public static class Dates
{
    public static DateTime? Utc(DateTime? d) => d?.Kind switch
    {
        null => null,
        DateTimeKind.Utc => d,
        DateTimeKind.Local => d.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(d.Value, DateTimeKind.Utc)
    };
}

/// <summary>
/// Longueurs maximales ([MaxLength]) contrôlées avant l'enregistrement : sans
/// cela, PostgreSQL refuse la ligne et le client reçoit une 500 sans motif.
/// </summary>
public static class Lengths
{
    public static string? Check(object entity)
    {
        foreach (var prop in entity.GetType().GetProperties())
        {
            var max = prop.GetCustomAttribute<MaxLengthAttribute>()?.Length;
            if (max is null || prop.PropertyType != typeof(string)) continue;
            if (prop.GetValue(entity) is string v && v.Length > max)
                return $"Le champ « {char.ToLowerInvariant(prop.Name[0])}{prop.Name[1..]} » dépasse {max} caractères ({v.Length}).";
        }
        return null;
    }
}
