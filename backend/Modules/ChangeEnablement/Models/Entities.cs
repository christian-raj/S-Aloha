using System.ComponentModel.DataAnnotations;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ChangeEnablement;

/// <summary>
/// Changement (ITIL 4, habilitation des changements) : évalué, autorisé puis
/// planifié au calendrier des changements.
/// </summary>
public class Change : Record
{
    public static readonly string[] Statuses = ["Demandé", "Évalué", "Autorisé", "Rejeté", "Planifié", "Mis en œuvre", "Clos"];
    /// <summary>Transitions permises (SOC-05, § 4 de la pratique) ; liste vide : statut final.</summary>
    public static readonly Dictionary<string, string[]> Transitions = new()
    {
        ["Demandé"] = ["Évalué"],
        ["Évalué"] = ["Autorisé", "Rejeté"],
        ["Autorisé"] = ["Planifié"],
        ["Planifié"] = ["Mis en œuvre"],
        ["Mis en œuvre"] = ["Clos"],
        ["Rejeté"] = [],
        ["Clos"] = [],
    };
    public static readonly string[] Done = ["Rejeté", "Clos"];
    public static readonly string[] Types = ["Standard", "Normal", "Urgent"];
    public static readonly string[] Risks = ["Faible", "Moyen", "Élevé"];
    public static readonly string[] Outcomes = ["Réussi", "Échoué"];

    [MaxLength(10)] public string ChangeType { get; set; } = "Normal"; // Standard (pré-autorisé), Normal, Urgent
    [MaxLength(10)] public string Risk { get; set; } = "Moyen";
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public string ImplementationPlan { get; set; } = "";
    public string BackoutPlan { get; set; } = "";                     // retour arrière
    [MaxLength(10)] public string? Outcome { get; set; }              // Réussi, Échoué
    public string? RejectionReason { get; set; }                      // motif communiqué au demandeur (CHG-17)
    [MaxLength(200)] public string? AuthorizedBy { get; set; }
    public DateTime? AuthorizedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public record ChangeDto(string Title, string? Description, string? Status,
    string? OwnerType, string? OwnerId, string? OwnerDisplayName,
    string? ChangeType, string? Risk, DateTime? PlannedStart, DateTime? PlannedEnd,
    string? ImplementationPlan, string? BackoutPlan, string? Outcome, string? RejectionReason = null) : IRecordDto;
