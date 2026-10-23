namespace SAloha.Api.Modules.KnowledgeManagement;

// Réponses de l'API (A2, ADR-0016) : DTO explicites, noms de champs inchangés pour le frontend.
// Un champ ajouté à une entité n'est exposé qu'en l'ajoutant ici.

public record KnowledgeArticleView(
    string? ArticleType,
    string? Content,
    string? Keywords,
    DateTime? ReviewDate,
    string? PublishedBy,
    DateTime? PublishedAt,
    DateTime? SubmittedAt,
    string? SubmittedBy,
    DateTime? LastReviewedAt,
    string? LastReviewedBy,
    DateTime? ArchiveRequestedAt,
    string? ArchiveRequestedBy,
    string? ArchiveRequestReason,
    int Version,
    int HelpfulCount,
    int NotHelpfulCount,
    bool FlaggedUnhelpful,
    int Id,
    int PerimeterId,
    string? Reference,
    string? Title,
    string? Description,
    string? Status,
    string? OwnerType,
    string? OwnerId,
    string? OwnerDisplayName,
    string? CreatedBy,
    string? CreatedByDisplayName,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static KnowledgeArticleView From(KnowledgeArticle e) => new(
        e.ArticleType,
        e.Content,
        e.Keywords,
        e.ReviewDate,
        e.PublishedBy,
        e.PublishedAt,
        e.SubmittedAt,
        e.SubmittedBy,
        e.LastReviewedAt,
        e.LastReviewedBy,
        e.ArchiveRequestedAt,
        e.ArchiveRequestedBy,
        e.ArchiveRequestReason,
        e.Version,
        e.HelpfulCount,
        e.NotHelpfulCount,
        e.FlaggedUnhelpful,
        e.Id,
        e.PerimeterId,
        e.Reference,
        e.Title,
        e.Description,
        e.Status,
        e.OwnerType,
        e.OwnerId,
        e.OwnerDisplayName,
        e.CreatedBy,
        e.CreatedByDisplayName,
        e.CreatedAt,
        e.UpdatedAt);
}

/// <summary>KB-15 : version publiée, telle que la fiche la liste.</summary>
public record KnowledgeArticleVersionView(int Version, string Title, string Description, string ArticleType, string Content,
    string Authors, string PublishedBy, DateTime PublishedAt)
{
    public static KnowledgeArticleVersionView From(KnowledgeArticleVersion v) =>
        new(v.Version, v.Title, v.Description, v.ArticleType, v.Content, v.Authors, v.PublishedBy, v.PublishedAt);
}
