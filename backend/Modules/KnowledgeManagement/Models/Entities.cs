using System.ComponentModel.DataAnnotations;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.KnowledgeManagement;

/// <summary>
/// Article de connaissance (ITIL 4, gestion des connaissances) : solution,
/// procédure, erreur connue documentée ou FAQ. La description porte le résumé.
/// </summary>
public class KnowledgeArticle : Record
{
    public static readonly string[] Statuses = ["Brouillon", "Publié", "Archivé"];
    public static readonly string[] Types = ["Solution", "Procédure", "Erreur connue", "FAQ"];

    [MaxLength(20)] public string ArticleType { get; set; } = "Solution";
    public string Content { get; set; } = "";
    [MaxLength(300)] public string Keywords { get; set; } = "";
    public DateTime? ReviewDate { get; set; }
    [MaxLength(200)] public string? PublishedBy { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public record KnowledgeArticleDto(string Title, string? Description, string? Status,
    string? OwnerType, string? OwnerId, string? OwnerDisplayName,
    string? ArticleType, string? Content, string? Keywords, DateTime? ReviewDate) : IRecordDto;
