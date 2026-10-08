using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Modules.IncidentManagement;
using SAloha.Api.Modules.KnowledgeManagement;
using SAloha.Api.Modules.ProblemManagement;

namespace SAloha.Api.Core.Search;

/// <summary>
/// Ce qui entre dans l'index (RAG-01) : uniquement du contenu validé par le
/// processus — un article publié, un problème dont la cause ou le contournement
/// est établi, un incident résolu ou clos avec sa résolution. Un brouillon ou
/// un incident en cours n'y entre pas : la recherche citerait des pistes non
/// vérifiées comme des solutions.
/// </summary>
public static class SearchSources
{
    public static readonly string[] Types = ["article", "problem", "incident"];
    private static readonly string[] SolvedProblem = ["Erreur connue", "Résolu", "Clos"];

    public record Document(string Type, int Id, string Reference, string Title, string Status, string Text);

    public static string? TypeOf(object entity) => entity switch
    {
        KnowledgeArticle => "article",
        Problem => "problem",
        Incident => "incident",
        _ => null
    };

    /// <summary>Document à indexer, ou null si l'enregistrement n'existe plus ou n'est pas éligible.</summary>
    public static async Task<Document?> IndexableAsync(AppDbContext db, string type, int id)
    {
        var d = await LoadAsync(db, type, id);
        return d is not null && Eligible(d) ? d : null;
    }

    /// <summary>Texte d'un enregistrement, éligible ou non (requête « cas similaires »).</summary>
    public static async Task<Document?> LoadAsync(AppDbContext db, string type, int id) => type switch
    {
        "article" => await db.KnowledgeArticles.AsNoTracking().Where(a => a.Id == id).Select(a => new Document(type, a.Id, a.Reference, a.Title, a.Status,
            Join(a.Description, a.Content, a.Keywords.Length > 0 ? "Mots-clés : " + a.Keywords : null))).FirstOrDefaultAsync(),
        "problem" => await db.Problems.AsNoTracking().Where(p => p.Id == id).Select(p => new Document(type, p.Id, p.Reference, p.Title, p.Status,
            Join(p.Description, p.RootCause != null ? "Cause racine : " + p.RootCause : null,
                p.KnownErrorWorkaround != null ? "Contournement : " + p.KnownErrorWorkaround : null))).FirstOrDefaultAsync(),
        "incident" => await db.Incidents.AsNoTracking().Where(i => i.Id == id).Select(i => new Document(type, i.Id, i.Reference, i.Title, i.Status,
            Join(i.Description, i.Resolution != null ? "Résolution : " + i.Resolution : null))).FirstOrDefaultAsync(),
        _ => null
    };

    private static bool Eligible(Document d) => d.Type switch
    {
        "article" => d.Status == "Publié",
        "problem" => SolvedProblem.Contains(d.Status),
        "incident" => Incident.Done.Contains(d.Status) && d.Text.Contains("Résolution : "),
        _ => false
    };

    /// <summary>Identifiants candidats d'un type (réindexation complète).</summary>
    public static Task<List<int>> IdsAsync(AppDbContext db, string type) => type switch
    {
        "article" => db.KnowledgeArticles.Where(a => a.Status == "Publié").Select(a => a.Id).ToListAsync(),
        "problem" => db.Problems.Where(p => SolvedProblem.Contains(p.Status)).Select(p => p.Id).ToListAsync(),
        "incident" => db.Incidents.Where(i => Incident.Done.Contains(i.Status)).Select(i => i.Id).ToListAsync(),
        _ => Task.FromResult(new List<int>())
    };

    private static string Join(params string?[] parts) =>
        string.Join("\n\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}
