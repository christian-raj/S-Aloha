using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Auth;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.KnowledgeManagement;

[Route("api/knowledge")]
public class KnowledgeController(AppDbContext db) : RecordController<KnowledgeArticle, KnowledgeArticleDto>(db)
{
    protected override string Code => "KB";
    protected override string LinkType => "article";
    protected override string[] Statuses => KnowledgeArticle.Statuses;
    protected override IReadOnlyDictionary<string, string[]> Transitions => KnowledgeArticle.Transitions;

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

    // Publier engage l'organisation ; archiver un article ou le réactiver aussi (KB-18).
    protected override bool RequiresManager(string from, string to) =>
        to is "Publié" or "Archivé" || from == "Archivé";

    protected override string? CheckStatus(KnowledgeArticle e, string status) =>
        status == "Publié" && string.IsNullOrWhiteSpace(e.Content) ? "Un article publié doit avoir un contenu." : null;

    // Un article publié retouché par un non-gestionnaire repasse en brouillon :
    // la nouvelle version attend une nouvelle publication.
    protected override string? StatusAfterEdit(KnowledgeArticle e, KnowledgeArticleDto dto) =>
        e.Status == "Publié" && (dto.Title.Trim() != e.Title || (dto.Description ?? "") != e.Description
                                 || (dto.Content ?? "") != e.Content || dto.ArticleType != e.ArticleType)
            ? "Brouillon" : null;

    protected override void OnStatusChanged(KnowledgeArticle e, string from)
    {
        if (e.Status == "Publié")
        {
            e.PublishedAt = DateTime.UtcNow; e.PublishedBy = MyDisplay;
            // KB-11 : sans date de revue, l'article se revoit un an après sa publication.
            e.ReviewDate ??= DateTime.UtcNow.Date.AddMonths(12);
        }
        else if (e.Status == "Brouillon") { e.PublishedAt = null; e.PublishedBy = null; }
        // Une décision est prise : la soumission (KB-21) et la demande d'archivage (KB-17) sont soldées.
        if (e.Status != "Brouillon") { e.SubmittedAt = null; e.SubmittedBy = null; }
        if (e.Status != "Publié") { e.ArchiveRequestedAt = null; e.ArchiveRequestedBy = null; e.ArchiveRequestReason = null; }
    }

    /// <summary>Responsable de l'article (en personne ou par un groupe AD) ou gestionnaire.</summary>
    private bool OwnsOrManages(KnowledgeArticle e) => IsManager || User.Designates(e.OwnerType, e.OwnerId);

    /// <summary>KB-21 : un brouillon est déclaré prêt ; il remonte dans « Décisions » des gestionnaires.</summary>
    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id)
    {
        var e = await Db.KnowledgeArticles.FindAsync(id);
        if (e is null) return NotFound();
        if (e.Status != "Brouillon") return BadRequest(new { message = "Seul un brouillon se soumet à publication." });
        if (string.IsNullOrWhiteSpace(e.Content)) return BadRequest(new { message = "Rédiger le contenu avant de soumettre l'article." });
        e.SubmittedAt = DateTime.UtcNow; e.SubmittedBy = MyDisplay;
        await Db.SaveChangesAsync();
        return Ok(e);
    }

    /// <summary>KB-17 : le responsable confirme l'article à sa revue ; il reste publié.</summary>
    [HttpPost("{id:int}/review")]
    public async Task<IActionResult> Review(int id, ReviewDto dto)
    {
        var e = await Db.KnowledgeArticles.FindAsync(id);
        if (e is null) return NotFound();
        if (e.Status != "Publié") return BadRequest(new { message = "Seul un article publié se revoit." });
        if (!OwnsOrManages(e)) return Forbid();
        var next = Dates.Utc(dto.NextReviewDate) ?? DateTime.UtcNow.Date.AddMonths(12);
        if (next <= DateTime.UtcNow) return BadRequest(new { message = "La prochaine revue doit être dans le futur." });
        e.ReviewDate = next;
        e.LastReviewedAt = DateTime.UtcNow; e.LastReviewedBy = MyDisplay;
        e.ArchiveRequestedAt = null; e.ArchiveRequestedBy = null; e.ArchiveRequestReason = null;
        await Db.SaveChangesAsync();
        return Ok(e);
    }

    /// <summary>KB-17 : à la revue, le responsable demande l'archivage ; un gestionnaire décide.</summary>
    [HttpPost("{id:int}/archive-request")]
    public async Task<IActionResult> RequestArchive(int id, ArchiveRequestDto dto)
    {
        var e = await Db.KnowledgeArticles.FindAsync(id);
        if (e is null) return NotFound();
        if (e.Status != "Publié") return BadRequest(new { message = "Seul un article publié se propose à l'archivage." });
        if (!OwnsOrManages(e)) return Forbid();
        if (string.IsNullOrWhiteSpace(dto.Reason)) return BadRequest(new { message = "Motiver la demande d'archivage." });
        e.ArchiveRequestedAt = DateTime.UtcNow; e.ArchiveRequestedBy = MyDisplay;
        e.ArchiveRequestReason = dto.Reason.Trim();
        var error = Lengths.Check(e);
        if (error is not null) return BadRequest(new { message = error });
        await Db.SaveChangesAsync();
        return Ok(e);
    }

    /// <summary>KB-14 : incidents et demandes reliés à l'article (résolus ou traités grâce à lui).</summary>
    [HttpGet("{id:int}/usage")]
    public async Task<IActionResult> Usage(int id)
    {
        if (!await Db.KnowledgeArticles.AnyAsync(a => a.Id == id)) return NotFound();
        var others = await Db.ItemLinks.AsNoTracking()
            .Where(l => (l.FromType == LinkType && l.FromId == id) || (l.ToType == LinkType && l.ToId == id))
            .Select(l => l.FromType == LinkType && l.FromId == id ? l.ToType : l.FromType).ToListAsync();
        return Ok(new { Incidents = others.Count(t => t == "incident"), Requests = others.Count(t => t == "request") });
    }

    // Le registre n'affiche pas le contenu, potentiellement long : il reste dans la fiche.
    protected override async Task<IEnumerable<object>> ListItems(IQueryable<KnowledgeArticle> q) =>
        await q.Select(a => new
        {
            a.Id, a.Reference, a.Title, a.Status, a.ArticleType, a.Keywords, a.ReviewDate,
            a.OwnerId, a.OwnerDisplayName, a.CreatedAt, a.SubmittedAt, a.ArchiveRequestedAt
        }).ToListAsync();

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
