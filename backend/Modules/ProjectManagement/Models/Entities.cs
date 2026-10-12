using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ProjectManagement;

/// <summary>
/// Méthodes de gestion de projet et composants qu'elles activent (PRJ-02,
/// ADR-0015). Le composant, pas la méthode, décide d'un onglet et de ses
/// règles : un projet hybride combine librement les composants.
/// </summary>
public static class Methods
{
    public const string Scrum = "Scrum", Kanban = "Kanban", VModel = "Cycle en V", Prince2 = "PRINCE2", Hybrid = "Hybride";
    public static readonly string[] All = [Scrum, Kanban, VModel, Prince2, Hybrid];

    // Composants
    public const string Backlog = "backlog", Sprints = "sprints", Board = "board", Burndown = "burndown",
        Phases = "phases", Milestones = "milestones", Deliverables = "deliverables",
        Stages = "stages", BusinessCase = "business-case", Risks = "risks", Issues = "issues", Tolerances = "tolerances";
    public static readonly string[] Components =
        [Backlog, Sprints, Board, Burndown, Phases, Milestones, Deliverables, Stages, BusinessCase, Risks, Issues, Tolerances];

    /// <summary>Composants d'une méthode (PRJ-03) ; pour l'hybride, la proposition par défaut.</summary>
    public static string[] DefaultComponents(string method) => method switch
    {
        Scrum => [Backlog, Sprints, Board, Burndown],
        Kanban => [Backlog, Board],
        VModel => [Phases, Milestones, Deliverables],
        Prince2 => [Stages, BusinessCase, Risks, Issues, Tolerances],
        _ => [Phases, Milestones, Deliverables, Backlog, Sprints, Board, Burndown, Risks]
    };

    /// <summary>Phases proposées à la création d'un projet en V (PRJ-10), modifiables ensuite.</summary>
    public static readonly string[] VPhases =
    [
        "Spécifications", "Conception générale", "Conception détaillée", "Développement",
        "Tests unitaires", "Tests d'intégration", "Recette", "Mise en production"
    ];

    /// <summary>Étapes de gestion PRINCE2 proposées (PRJ-20).</summary>
    public static readonly string[] Prince2Stages = ["Démarrage", "Initialisation", "Livraison 1", "Clôture"];
}

/// <summary>
/// Projet (PRJ-AAAA-NNNN) : le responsable (Owner) est le chef de projet.
/// Business case et tolérances : PRINCE2, ou hybride si activés.
/// </summary>
public class Project : Record
{
    public static readonly string[] Statuses = ["Cadrage", "En cours", "Suspendu", "Clôturé", "Abandonné"];
    public static readonly string[] Done = ["Clôturé", "Abandonné"];

    [MaxLength(20)] public string Method { get; set; } = Methods.Scrum;
    /// <summary>Composants activés, séparés par des virgules (voir <see cref="Methods.Components"/>).</summary>
    [MaxLength(200)] public string Components { get; set; } = "";
    public DateTime? StartDate { get; set; }
    public DateTime? TargetEndDate { get; set; }
    public decimal? Budget { get; set; }
    public decimal? ActualCost { get; set; }
    // Business case (PRINCE2)
    public string? Justification { get; set; }
    public string? ExpectedBenefits { get; set; }
    public string? ExpectedCosts { get; set; }
    public string? MajorRisks { get; set; }
    // Tolérances (PRINCE2) : au-delà, le chef de projet remonte une exception.
    public int? ToleranceDays { get; set; }
    public decimal? ToleranceCostPercent { get; set; }
    [MaxLength(500)] public string? ToleranceScope { get; set; }
    [MaxLength(500)] public string? ToleranceQuality { get; set; }
    public DateTime? ClosedAt { get; set; }
    /// <summary>Validation du cadrage par un gestionnaire (PRJ-08).</summary>
    [MaxLength(200)] public string? ValidatedBy { get; set; }
    public DateTime? ValidatedAt { get; set; }
    /// <summary>Motif d'abandon, obligatoire pour abandonner (PRJ-09).</summary>
    public string? AbandonReason { get; set; }

    public bool Has(string component) => Components.Split(',').Contains(component);
}

public record ProjectDto(string Title, string? Description, string? Status,
    string? OwnerType, string? OwnerId, string? OwnerDisplayName,
    string? Method, string[]? Components, DateTime? StartDate, DateTime? TargetEndDate,
    decimal? Budget, decimal? ActualCost,
    string? Justification, string? ExpectedBenefits, string? ExpectedCosts, string? MajorRisks,
    int? ToleranceDays, decimal? ToleranceCostPercent, string? ToleranceScope, string? ToleranceQuality,
    string? AbandonReason = null) : IRecordDto;

/// <summary>Élément rattaché à un projet (phase, sprint, élément de travail…).</summary>
public abstract class ProjectPart
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    [JsonIgnore] public Project? Project { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(200)] public string CreatedBy { get; set; } = "";
}

/// <summary>Phase (cycle en V) ou étape de gestion (PRINCE2), séquentielles.</summary>
public class ProjectPhase : ProjectPart
{
    public static readonly string[] Kinds = ["Phase", "Étape"];
    public static readonly string[] Statuses = ["À venir", "En cours", "Terminée"];
    [MaxLength(10)] public string Kind { get; set; } = "Phase";
    [MaxLength(150)] public string Name { get; set; } = "";
    public int Order { get; set; }
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "À venir";
    /// <summary>Validation de fin d'étape (PRINCE2, PRJ-21).</summary>
    [MaxLength(200)] public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

public class Milestone : ProjectPart
{
    [MaxLength(150)] public string Name { get; set; } = "";
    public int? PhaseId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? AchievedAt { get; set; }
}

public class Deliverable : ProjectPart
{
    public static readonly string[] Statuses = ["À produire", "En cours", "Livré", "Validé"];
    [MaxLength(150)] public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int? PhaseId { get; set; }
    public DateTime? DueDate { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "À produire";
    [MaxLength(200)] public string? ValidatedBy { get; set; }
    public DateTime? ValidatedAt { get; set; }
}

public class Sprint : ProjectPart
{
    public static readonly string[] Statuses = ["Planifié", "Actif", "Clos"];
    [MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(500)] public string Goal { get; set; } = "";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "Planifié";
}

/// <summary>User story, tâche ou bogue : backlog, sprint, tableau.</summary>
public class WorkItem : ProjectPart
{
    public static readonly string[] Types = ["User story", "Tâche", "Bogue"];
    public static readonly string[] Statuses = ["À faire", "En cours", "En revue", "Terminé"];
    [MaxLength(20)] public string Type { get; set; } = "User story";
    [MaxLength(200)] public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string AcceptanceCriteria { get; set; } = "";
    public int? Points { get; set; }
    /// <summary>Ordre de priorité au backlog : le plus petit en tête.</summary>
    public int Rank { get; set; }
    public int? SprintId { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "À faire";
    [MaxLength(200)] public string? AssigneeId { get; set; }
    [MaxLength(250)] public string? AssigneeDisplayName { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ProjectRisk : ProjectPart
{
    public static readonly string[] Responses = ["Éviter", "Réduire", "Transférer", "Partager", "Accepter"];
    public static readonly string[] Statuses = ["Ouvert", "Clos"];
    [MaxLength(200)] public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int Probability { get; set; } = 3;   // 1 à 5
    public int Impact { get; set; } = 3;        // 1 à 5
    public int Score => Probability * Impact;
    [MaxLength(20)] public string Response { get; set; } = "Réduire";
    [MaxLength(250)] public string? OwnerDisplayName { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "Ouvert";
}

/// <summary>Incident projet (PRINCE2 « issue ») : distinct d'un incident ITIL.</summary>
public class ProjectIssue : ProjectPart
{
    public static readonly string[] Types = ["Demande de changement", "Hors spécification", "Problème ou préoccupation"];
    public static readonly string[] Severities = ["Faible", "Moyenne", "Élevée"];
    public static readonly string[] Statuses = ["Ouvert", "En traitement", "Clos"];
    [MaxLength(30)] public string Type { get; set; } = "Problème ou préoccupation";
    [MaxLength(200)] public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    [MaxLength(10)] public string Severity { get; set; } = "Moyenne";
    [MaxLength(20)] public string Status { get; set; } = "Ouvert";
    public string? Resolution { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public record PhaseDto(string? Kind, string Name, int? Order, DateTime? PlannedStart, DateTime? PlannedEnd, string? Status);
public record MilestoneDto(string Name, int? PhaseId, DateTime? DueDate, bool Achieved);
public record DeliverableDto(string Name, string? Description, int? PhaseId, DateTime? DueDate, string? Status);
public record SprintDto(string Name, string? Goal, DateTime? StartDate, DateTime? EndDate, string? Status);
public record WorkItemDto(string? Type, string Title, string? Description, string? AcceptanceCriteria, int? Points,
    int? Rank, int? SprintId, string? Status, string? AssigneeId, string? AssigneeDisplayName);
public record RiskDto(string Title, string? Description, int Probability, int Impact, string? Response, string? OwnerDisplayName, string? Status);
public record IssueDto(string? Type, string Title, string? Description, string? Severity, string? Status, string? Resolution);
