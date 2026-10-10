using System.ComponentModel.DataAnnotations;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ServiceLevelManagement;

/// <summary>Service du catalogue (ITIL 4, gestion des niveaux de service). Le titre porte le nom du service.</summary>
public class ItService : Record
{
    public static readonly string[] Statuses = ["En conception", "En service", "Retiré"];
    /// <summary>Transitions permises (SOC-05, § 4 de la pratique) ; liste vide : statut final.</summary>
    public static readonly Dictionary<string, string[]> Transitions = new()
    {
        ["En conception"] = ["En service"],
        ["En service"] = ["Retiré"],
        ["Retiré"] = [],
    };
    public static readonly string[] Criticalities = ["Faible", "Moyenne", "Élevée"];

    [MaxLength(10)] public string Criticality { get; set; } = "Moyenne";
    [MaxLength(150)] public string ServiceHours { get; set; } = "";    // ex. Lun–Ven 8h–18h
    public List<ServiceLevelAgreement> Agreements { get; set; } = new();
}

/// <summary>
/// Accord de niveau de service : cibles convenues avec un client pour un
/// service (disponibilité, délais de résolution des incidents par priorité).
/// </summary>
public class ServiceLevelAgreement : Record
{
    public static readonly string[] Statuses = ["Brouillon", "En vigueur", "Expiré"];
    /// <summary>Transitions permises (SOC-05, § 4 de la pratique) ; liste vide : statut final.</summary>
    public static readonly Dictionary<string, string[]> Transitions = new()
    {
        ["Brouillon"] = ["En vigueur"],
        ["En vigueur"] = ["Expiré", "Brouillon"],
        ["Expiré"] = [],   // un nouvel accord se crée
    };

    public int ServiceId { get; set; }
    public ItService? Service { get; set; }
    [MaxLength(200)] public string Customer { get; set; } = "";
    public decimal? AvailabilityTarget { get; set; }                  // en %, ex. 99.5
    public int? ResolutionHoursP1 { get; set; }
    public int? ResolutionHoursP2 { get; set; }
    public int? ResolutionHoursP3 { get; set; }
    public int? ResolutionHoursP4 { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public DateTime? ReviewDate { get; set; }
}

public record ItServiceDto(string Title, string? Description, string? Status,
    string? OwnerType, string? OwnerId, string? OwnerDisplayName,
    string? Criticality, string? ServiceHours) : IRecordDto;

public record ServiceLevelAgreementDto(string Title, string? Description, string? Status,
    string? OwnerType, string? OwnerId, string? OwnerDisplayName,
    int ServiceId, string? Customer, decimal? AvailabilityTarget,
    int? ResolutionHoursP1, int? ResolutionHoursP2, int? ResolutionHoursP3, int? ResolutionHoursP4,
    DateTime? ValidFrom, DateTime? ValidTo, DateTime? ReviewDate) : IRecordDto;
