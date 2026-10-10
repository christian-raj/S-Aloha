using System.ComponentModel.DataAnnotations;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.IncidentManagement;

/// <summary>
/// Incident (ITIL 4, gestion des incidents) : interruption ou dégradation
/// d'un service, à rétablir au plus vite.
/// </summary>
public class Incident : Record
{
    public static readonly string[] Statuses = ["Nouveau", "En cours", "En attente", "Résolu", "Clos"];
    /// <summary>Transitions permises (SOC-05, § 4 de la pratique) ; liste vide : statut final.</summary>
    public static readonly Dictionary<string, string[]> Transitions = new()
    {
        ["Nouveau"] = ["En cours", "Résolu"],
        ["En cours"] = ["En attente", "Résolu"],
        ["En attente"] = ["En cours"],
        ["Résolu"] = ["En cours", "Clos"],
        ["Clos"] = [],   // un incident clos ne se rouvre pas : on en crée un nouveau, relié
    };
    public static readonly string[] Done = ["Résolu", "Clos"];

    [MaxLength(10)] public string Impact { get; set; } = "Moyen";    // Faible, Moyen, Élevé
    [MaxLength(10)] public string Urgency { get; set; } = "Moyenne"; // Faible, Moyenne, Élevée
    [MaxLength(10)] public string Priority { get; set; } = "P3";     // P1..P4 (dérivée impact x urgence)
    [MaxLength(100)] public string Category { get; set; } = "";
    [MaxLength(150)] public string AffectedService { get; set; } = "";
    public bool IsMajor { get; set; }                                // incident majeur
    public string? Resolution { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public record IncidentDto(string Title, string? Description, string? Status,
    string? OwnerType, string? OwnerId, string? OwnerDisplayName,
    string? Impact, string? Urgency, string? Category, string? AffectedService,
    bool IsMajor, string? Resolution) : IRecordDto;
