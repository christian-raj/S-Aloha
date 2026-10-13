using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Modules.ChangeEnablement;
using SAloha.Api.Modules.ComplianceAssessment;
using SAloha.Api.Modules.ContinualImprovement;
using SAloha.Api.Modules.IncidentManagement;
using SAloha.Api.Modules.KnowledgeManagement;
using SAloha.Api.Modules.ProblemManagement;
using SAloha.Api.Modules.ProjectManagement;
using SAloha.Api.Modules.ServiceConfigurationManagement;
using SAloha.Api.Modules.ServiceLevelManagement;
using SAloha.Api.Modules.ServiceRequestManagement;

namespace SAloha.Api.Core.Links;

/// <summary>
/// Lien entre deux enregistrements de processus (incident → problème,
/// changement → CI…). Les modules ne se connaissent pas : le lien vit dans le
/// socle et désigne chaque extrémité par son type et son identifiant.
/// </summary>
public class ItemLink
{
    public int Id { get; set; }
    [MaxLength(20)] public string FromType { get; set; } = "";
    public int FromId { get; set; }
    [MaxLength(20)] public string ToType { get; set; } = "";
    public int ToId { get; set; }
    [MaxLength(100)] public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Extrémité d'un lien, telle qu'affichée.</summary>
public class LinkTarget
{
    public int Id { get; set; }
    public string Reference { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    /// <summary>Périmètre de l'enregistrement (PER-05).</summary>
    public int PerimeterId { get; set; }
    /// <summary>Référentiel partagé, lisible de tous (ADR-0014 D2 : services, articles publiés).</summary>
    public bool Shared { get; set; }
}

/// <summary>
/// Registre des types reliables : code de référence et accès aux
/// enregistrements. Avec Core/Pilotage, seul endroit du socle qui connaît les modules.
/// </summary>
public static class ItemLinks
{
    public record Kind(string Type, string Code, Func<AppDbContext, IQueryable<LinkTarget>> Query);

    private static IQueryable<LinkTarget> Of<T>(DbSet<T> set) where T : Records.Record =>
        set.Select(x => new LinkTarget { Id = x.Id, Reference = x.Reference, Title = x.Title, Status = x.Status, PerimeterId = x.PerimeterId });

    public static readonly Kind[] Kinds =
    [
        new("incident", "INC", db => Of(db.Incidents)),
        new("request", "REQ", db => Of(db.ServiceRequests)),
        new("problem", "PRB", db => Of(db.Problems)),
        new("change", "CHG", db => Of(db.Changes)),
        new("ci", "CI", db => Of(db.ConfigurationItems)),
        new("service", "SVC", db => db.Services.Select(x => new LinkTarget
            { Id = x.Id, Reference = x.Reference, Title = x.Title, Status = x.Status, PerimeterId = x.PerimeterId, Shared = true })),
        new("agreement", "SLA", db => Of(db.Agreements)),
        new("article", "KB", db => db.KnowledgeArticles.Select(x => new LinkTarget
            { Id = x.Id, Reference = x.Reference, Title = x.Title, Status = x.Status, PerimeterId = x.PerimeterId, Shared = x.Status == "Publié" })),
        new("improvement", "AMI", db => Of(db.Improvements)),
        new("assessment", "EVA", db => Of(db.Assessments)),
        new("project", "PRJ", db => Of(db.Projects)),
    ];

    public static Kind? ByType(string type) => Kinds.FirstOrDefault(k => k.Type == type);

    /// <summary>Enregistrements du socle d'un type (commentaires, SOC-24) ; null pour un type inconnu.</summary>
    public static IQueryable<Records.Record>? Records(AppDbContext db, string type) => type switch
    {
        "incident" => db.Incidents, "request" => db.ServiceRequests, "problem" => db.Problems,
        "change" => db.Changes, "ci" => db.ConfigurationItems, "service" => db.Services,
        "agreement" => db.Agreements, "article" => db.KnowledgeArticles, "improvement" => db.Improvements,
        "assessment" => db.Assessments, "project" => db.Projects,
        _ => null
    };

    /// <summary>Type désigné par une référence XXX-AAAA-NNNN.</summary>
    public static Kind? ByReference(string reference)
    {
        var code = reference.Split('-')[0];
        return Kinds.FirstOrDefault(k => k.Code == code);
    }

    /// <summary>
    /// Vrai si (type, id) est relié, dans un sens ou dans l'autre, à un autre
    /// enregistrement du type <paramref name="otherType"/> (ex. doublon relié à
    /// l'incident conservé, INC-08, PRB-13).
    /// </summary>
    public static Task<bool> IsLinkedToAsync(AppDbContext db, string type, int id, string otherType) =>
        db.ItemLinks.AnyAsync(l =>
            (l.FromType == type && l.FromId == id && l.ToType == otherType && !(l.ToType == type && l.ToId == id)) ||
            (l.ToType == type && l.ToId == id && l.FromType == otherType && !(l.FromType == type && l.FromId == id)));

    /// <summary>Retire les liens d'un enregistrement supprimé (à enregistrer par l'appelant).</summary>
    public static void RemoveFor(AppDbContext db, string type, int id) =>
        db.ItemLinks.RemoveRange(db.ItemLinks.Where(l =>
            (l.FromType == type && l.FromId == id) || (l.ToType == type && l.ToId == id)));
}
