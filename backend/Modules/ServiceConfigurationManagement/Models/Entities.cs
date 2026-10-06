using System.ComponentModel.DataAnnotations;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ServiceConfigurationManagement;

/// <summary>
/// Élément de configuration (ITIL 4, gestion de la configuration des
/// services) : composant à gérer pour délivrer un service. Le titre porte le nom du CI.
/// </summary>
public class ConfigurationItem : Record
{
    public static readonly string[] Statuses = ["Planifié", "En service", "Hors service", "Retiré"];
    public static readonly string[] Types =
        ["Application", "Serveur", "Base de données", "Réseau", "Stockage", "Poste de travail", "Logiciel", "Autre"];
    public static readonly string[] Environments = ["Production", "Recette", "Développement", "Autre"];

    [MaxLength(30)] public string CiType { get; set; } = "Autre";
    [MaxLength(20)] public string Environment { get; set; } = "Production";
    [MaxLength(150)] public string Location { get; set; } = "";
    public List<CiRelationship> Outgoing { get; set; } = new();   // ce CI → autres
    public List<CiRelationship> Incoming { get; set; } = new();   // autres → ce CI
}

/// <summary>Relation orientée entre deux CI : « source » dépend de / héberge… « cible ».</summary>
public class CiRelationship
{
    public static readonly string[] Types = ["Dépend de", "Héberge", "Fait partie de", "Se connecte à"];

    /// <summary>
    /// Sens de propagation d'une panne. « Source dépend de / se connecte à
    /// cible » : la panne de la cible touche la source. « Source héberge /
    /// fait partie de cible » : la panne de la source touche la cible.
    /// </summary>
    public static bool FailureFlowsFromTarget(string type) => type is "Dépend de" or "Se connecte à";

    public int Id { get; set; }
    public int SourceId { get; set; }
    public ConfigurationItem? Source { get; set; }
    public int TargetId { get; set; }
    public ConfigurationItem? Target { get; set; }
    [MaxLength(30)] public string Type { get; set; } = "Dépend de";
}

public record ConfigurationItemDto(string Title, string? Description, string? Status,
    string? OwnerType, string? OwnerId, string? OwnerDisplayName,
    string? CiType, string? Environment, string? Location) : IRecordDto;

public record CiRelationshipDto(string TargetReference, string Type);
