using System.ComponentModel.DataAnnotations;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ComplianceAssessment;

/// <summary>
/// Objectif de sécurité du Référentiel Cyber France (ReCyF) de l'ANSSI, qui
/// décline les mesures de l'article 21 de la directive NIS 2. Structure seule
/// (numéro, titre, pilier) : ADR-0012.
/// </summary>
public class SecurityObjective
{
    public int Id { get; set; }                                   // numéro 1 à 20
    [MaxLength(200)] public string Title { get; set; } = "";
    [MaxLength(20)] public string Pillar { get; set; } = "";      // Gouvernance, Protection, Défense, Résilience
    public List<SecurityRequirement> Requirements { get; set; } = new();
}

/// <summary>
/// Exigence du ReCyF. Le dépôt ne porte que sa structure (code, thématique,
/// cibles, n° de mesures ISO 27002) ; son <see cref="Text"/> est importé par
/// un administrateur depuis le document de l'ANSSI (ADR-0012).
/// </summary>
public class SecurityRequirement
{
    public int Id { get; set; }
    [MaxLength(20)] public string Code { get; set; } = "";        // ex. 5.B.4-EI/EE
    public int ObjectiveId { get; set; }
    public SecurityObjective? Objective { get; set; }
    [MaxLength(150)] public string Theme { get; set; } = "";
    public bool ForImportant { get; set; }                         // applicable aux entités importantes
    public bool ForEssential { get; set; }                         // applicable aux entités essentielles
    [MaxLength(300)] public string IsoControls { get; set; } = ""; // n° séparés par des espaces, sans intitulés
    public int Order { get; set; }
    public string? Text { get; set; }
    public DateTime? TextImportedAt { get; set; }
}

/// <summary>
/// Évaluation de conformité NIS 2 d'un périmètre (description) au regard du
/// ReCyF : une réponse notée 0 à 3 (ou « non applicable ») par exigence
/// applicable à la catégorie d'entité.
/// </summary>
public class Assessment : Record
{
    public static readonly string[] Statuses = ["Brouillon", "En cours", "Validée"];
    public const string Important = "Entité importante";
    public const string Essential = "Entité essentielle";
    public static readonly string[] Categories = [Important, Essential];

    [MaxLength(30)] public string EntityCategory { get; set; } = Important;
    [MaxLength(200)] public string? ValidatedBy { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public List<AssessmentResponse> Responses { get; set; } = new();

    public bool Applies(SecurityRequirement r) => EntityCategory == Essential ? r.ForEssential : r.ForImportant;
}

/// <summary>Réponse à une exigence : score 0 (absent) à 3 (maîtrisé), ou non applicable.</summary>
public class AssessmentResponse
{
    public int Id { get; set; }
    public int AssessmentId { get; set; }
    public Assessment? Assessment { get; set; }
    public int RequirementId { get; set; }
    public SecurityRequirement? Requirement { get; set; }
    public int? Score { get; set; }
    public bool NotApplicable { get; set; }
    public string Justification { get; set; } = "";
    [MaxLength(200)] public string UpdatedBy { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public record AssessmentDto(string Title, string? Description, string? Status,
    string? OwnerType, string? OwnerId, string? OwnerDisplayName,
    string? EntityCategory) : IRecordDto;

public record ResponseDto(int? Score, bool NotApplicable, string? Justification);
public record GapActionDto(string Code);
public record ReferentialImportDto(string Csv);
