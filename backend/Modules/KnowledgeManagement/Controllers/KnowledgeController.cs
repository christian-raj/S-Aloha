using Microsoft.AspNetCore.Mvc;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.KnowledgeManagement;

[Route("api/knowledge")]
public class KnowledgeController(AppDbContext db) : RecordController<KnowledgeArticle, KnowledgeArticleDto>(db)
{
    protected override string Code => "KB";
    protected override string LinkType => "article";
    protected override string[] Statuses => KnowledgeArticle.Statuses;

    protected override string? Apply(KnowledgeArticle e, KnowledgeArticleDto dto, bool creating)
    {
        var error = Allowed.Check("Type d'article", dto.ArticleType, KnowledgeArticle.Types);
        if (error is not null) return error;
        e.ArticleType = dto.ArticleType!;
        e.Content = dto.Content ?? "";
        e.Keywords = dto.Keywords ?? "";
        e.ReviewDate = Dates.Utc(dto.ReviewDate);
        return null;
    }

    // Publier engage l'organisation : réservé aux gestionnaires.
    protected override bool RequiresManager(string status) => status == "Publié";

    protected override string? CheckTransition(KnowledgeArticle e, string to) =>
        to == "Publié" && string.IsNullOrWhiteSpace(e.Content) ? "Un article publié doit avoir un contenu." : null;

    protected override void OnStatusChanged(KnowledgeArticle e, string from)
    {
        if (e.Status == "Publié") { e.PublishedAt = DateTime.UtcNow; e.PublishedBy = MyDisplay; }
        else if (e.Status == "Brouillon") { e.PublishedAt = null; e.PublishedBy = null; }
    }

    // La recherche porte aussi sur les mots-clés et le contenu.
    protected override IQueryable<KnowledgeArticle> Search(IQueryable<KnowledgeArticle> q, string key) =>
        q.Where(a => a.Title.ToLower().Contains(key) || a.Reference.ToLower().Contains(key)
                     || a.Keywords.ToLower().Contains(key) || a.Content.ToLower().Contains(key));

    protected override IQueryable<KnowledgeArticle> Filter(IQueryable<KnowledgeArticle> q)
    {
        var type = Request.Query["type"].ToString();
        return string.IsNullOrEmpty(type) ? q : q.Where(a => a.ArticleType == type);
    }
}
