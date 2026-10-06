using System.ComponentModel.DataAnnotations;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Modules.ProblemManagement;

public class Problem : IHasReference
{
    public int Id { get; set; }
    [MaxLength(20)] public string Reference { get; set; } = "";   // PRB-2026-0001
    [MaxLength(200)] public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    [MaxLength(30)] public string Status { get; set; } = "Nouveau"; // Nouveau, En analyse, Erreur connue, Résolu, Clos
    [MaxLength(10)] public string Impact { get; set; } = "Moyen";   // Faible, Moyen, Élevé
    [MaxLength(10)] public string Urgency { get; set; } = "Moyenne";// Faible, Moyenne, Élevée
    [MaxLength(10)] public string Priority { get; set; } = "P3";    // P1..P4 (dérivée impact x urgence)
    [MaxLength(100)] public string Category { get; set; } = "";     // Réseau, Serveur, Application...
    [MaxLength(150)] public string AffectedService { get; set; } = "";
    [MaxLength(100)] public string CreatedBy { get; set; } = "";    // sAMAccountName
    [MaxLength(200)] public string CreatedByDisplayName { get; set; } = "";
    public string? KnownErrorWorkaround { get; set; }               // contournement (Known Error)
    public string? RootCause { get; set; }                          // cause racine validée
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public List<RcaAnalysis> Analyses { get; set; } = new();
    public List<CorrectiveAction> Actions { get; set; } = new();
}

public class RcaAnalysis
{
    public int Id { get; set; }
    public int ProblemId { get; set; }
    public Problem? Problem { get; set; }
    [MaxLength(20)] public string Method { get; set; } = "FIVE_WHYS"; // FIVE_WHYS, ISHIKAWA, FTA
    public string DataJson { get; set; } = "{}";                      // structure propre à la méthode
    public string? Conclusion { get; set; }
    [MaxLength(100)] public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class CorrectiveAction
{
    public int Id { get; set; }
    public int ProblemId { get; set; }
    public Problem? Problem { get; set; }
    [MaxLength(200)] public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    [MaxLength(20)] public string Status { get; set; } = "À faire";  // À faire, En cours, Terminée, Annulée
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    [MaxLength(100)] public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<RaciAssignment> Raci { get; set; } = new();
}

public class RaciAssignment
{
    public int Id { get; set; }
    public int CorrectiveActionId { get; set; }
    public CorrectiveAction? Action { get; set; }
    [MaxLength(1)] public string Role { get; set; } = "R";           // R, A, C, I
    [MaxLength(10)] public string AssigneeType { get; set; } = "User"; // User | Group
    [MaxLength(200)] public string AssigneeId { get; set; } = "";    // sAMAccountName ou nom du groupe AD
    [MaxLength(250)] public string AssigneeDisplayName { get; set; } = "";
}
