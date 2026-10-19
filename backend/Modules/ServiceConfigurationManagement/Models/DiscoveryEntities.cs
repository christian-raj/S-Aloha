using System.ComponentModel.DataAnnotations;
using SAloha.Api.Core.Perimeters;

namespace SAloha.Api.Modules.ServiceConfigurationManagement;

/// <summary>
/// Actif découvert (CFG-40) : attend dans le sas de staging d'être rapproché d'un CI, ou
/// exclu. Un même équipement vu par plusieurs sources (balayage IP, annuaire, SNMP) n'y
/// figure qu'une fois : ses observations sont fusionnées.
/// </summary>
public class DiscoveredAsset : IHasPerimeter
{
    public const string ToReconcile = "À rapprocher";
    public const string Reconciled = "Rapproché";
    public const string Ignored = "Ignoré";
    public const string InConflict = "En conflit";
    public static readonly string[] Statuses = [ToReconcile, InConflict, Reconciled, Ignored];
    /// <summary>Statuts qui attendent une décision du gestionnaire.</summary>
    public static readonly string[] Open = [ToReconcile, InConflict];

    public int Id { get; set; }
    public int PerimeterId { get; set; } = Perimeter.DefaultId;

    [MaxLength(45)] public string? IpAddress { get; set; }
    [MaxLength(17)] public string? MacAddress { get; set; }
    [MaxLength(200)] public string? Hostname { get; set; }
    [MaxLength(100)] public string? SerialNumber { get; set; }
    [MaxLength(100)] public string? Vendor { get; set; }
    [MaxLength(100)] public string? Model { get; set; }
    [MaxLength(150)] public string? DetectedOs { get; set; }
    /// <summary>Type de CI déduit (valeurs de <see cref="ConfigurationItem.Types"/>).</summary>
    [MaxLength(30)] public string DetectedType { get; set; } = "Autre";
    [MaxLength(150)] public string? Location { get; set; }
    [MaxLength(250)] public string? OpenPorts { get; set; }
    /// <summary>CFG-49 : bannières lues sur les ports ouverts, une par ligne (« 22 SSH-2.0-OpenSSH_9.6 »).</summary>
    [MaxLength(500)] public string? Banners { get; set; }
    /// <summary>Sources qui l'ont vu, séparées par des virgules (IpSweep, Snmp, ActiveDirectory).</summary>
    [MaxLength(100)] public string Sources { get; set; } = "";
    /// <summary>Compte d'ordinateur présent dans l'annuaire (CFG-42).</summary>
    public bool InDirectory { get; set; }
    public string? RawData { get; set; }

    [MaxLength(30)] public string Status { get; set; } = ToReconcile;
    /// <summary>Score du meilleur rapprochement (0 à 100, CFG-41).</summary>
    public int ConfidenceScore { get; set; }
    /// <summary>Critère du rapprochement : SerialNumber, MacAddress, Hostname, IpAddress ou None.</summary>
    [MaxLength(20)] public string MatchRule { get; set; } = "None";
    /// <summary>Action suggérée : UpdateExisting, Conflict ou CreateNew (CFG-41).</summary>
    [MaxLength(20)] public string SuggestedAction { get; set; } = "CreateNew";
    [MaxLength(300)] public string? ConflictReason { get; set; }

    /// <summary>CI rapproché (statut Rapproché) ou proposé (à rapprocher, en conflit).</summary>
    public int? MatchedCiId { get; set; }
    public ConfigurationItem? MatchedCi { get; set; }
    public int? JobId { get; set; }

    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReconciledAt { get; set; }
    [MaxLength(200)] public string? ReconciledBy { get; set; }

    /// <summary>
    /// CFG-42 : actif inconnu (Shadow IT) — vu sur le réseau, sans compte d'ordinateur dans
    /// l'annuaire ni CI rapproché, et pas encore traité.
    /// </summary>
    public bool Unknown => !InDirectory && MatchedCiId is null && Open.Contains(Status);
}

/// <summary>Tâche de découverte : cible, fréquence et fenêtre de scan, identifiant du coffre (CFG-46).</summary>
public class DiscoveryJob : IHasPerimeter
{
    /// <summary>Types de scan disponibles ; SSH, WinRM, hyperviseurs et agent viendront en phases 3 et 4 du chantier F13.</summary>
    public static readonly string[] ScanTypes = ["IpSweep", "Snmp", "ActiveDirectory"];
    public const string NeverRun = "Jamais lancée";
    public const string Queued = "En file";
    public const string Running = "En cours";
    public const string Succeeded = "Réussie";
    public const string Failed = "En échec";
    public const string DefaultPorts = "22,80,443,135,445,3389,5985,9100";

    public int Id { get; set; }
    public int PerimeterId { get; set; } = Perimeter.DefaultId;

    [MaxLength(150)] public string Name { get; set; } = "";
    /// <summary>Plage CIDR ou adresse IP (balayage, SNMP) ; BaseDN facultatif (annuaire).</summary>
    [MaxLength(250)] public string TargetRange { get; set; } = "";
    [MaxLength(30)] public string ScanType { get; set; } = "IpSweep";
    [MaxLength(250)] public string PortList { get; set; } = DefaultPorts;
    /// <summary>Lancement automatique toutes les N heures ; 0 : lancement manuel seulement.</summary>
    public int FrequencyHours { get; set; } = 24;
    /// <summary>Fenêtre de scan, en heure locale de l'organisation ; sans fenêtre, à toute heure.</summary>
    public TimeOnly? WindowStart { get; set; } = new(20, 0);
    public TimeOnly? WindowEnd { get; set; } = new(6, 0);

    public int? CredentialId { get; set; }
    public DiscoveryCredential? Credential { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? LastRunAt { get; set; }
    [MaxLength(30)] public string LastStatus { get; set; } = NeverRun;
    [MaxLength(500)] public string? LastError { get; set; }
    public int? LastFoundCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string CreatedBy { get; set; } = "";
}

/// <summary>
/// Identifiant du coffre (CFG-45) : communauté SNMP, compte de domaine… Le secret est chiffré
/// au repos et n'est jamais renvoyé par l'API.
/// </summary>
public class DiscoveryCredential : IHasPerimeter
{
    /// <summary>Types utilisables par les collecteurs actuels ; SNMP v3, clés SSH et API cloud viendront avec leurs collecteurs.</summary>
    public static readonly string[] Types = ["SnmpV2c", "WindowsDomain"];

    public int Id { get; set; }
    public int PerimeterId { get; set; } = Perimeter.DefaultId;

    [MaxLength(150)] public string Name { get; set; } = "";
    [MaxLength(30)] public string CredentialType { get; set; } = "SnmpV2c";
    [MaxLength(150)] public string? Username { get; set; }
    [MaxLength(150)] public string? Domain { get; set; }
    public string EncryptedSecret { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string CreatedBy { get; set; } = "";
    public DateTime? SecretChangedAt { get; set; }
}

/// <summary>
/// Fin de support d'un produit (CFG-47) : un CI dont le système (ou constructeur et modèle)
/// contient <see cref="Product"/> est en fin de support à partir de <see cref="EndOfSupport"/>.
/// Référentiel partagé, tenu par les gestionnaires.
/// </summary>
public class EndOfLifeRule
{
    public int Id { get; set; }
    [MaxLength(150)] public string Product { get; set; } = "";
    public DateTime EndOfSupport { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string CreatedBy { get; set; } = "";
}

public record ReconcileAssetDto(string Action, int? TargetCiId = null, ConfigurationItemDto? Ci = null);

public record DiscoveryJobDto(
    string Name, string ScanType, string? TargetRange, string? PortList, int? CredentialId,
    int FrequencyHours = 24, string? WindowStart = "20:00", string? WindowEnd = "06:00",
    bool IsActive = true, int? PerimeterId = null);

public record DiscoveryCredentialDto(
    string Name, string CredentialType, string? Username, string? Domain, string? Secret, int? PerimeterId = null);

public record EndOfLifeRuleDto(string Product, DateTime? EndOfSupport, string? Notes);
