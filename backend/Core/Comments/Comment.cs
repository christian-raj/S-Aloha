using System.ComponentModel.DataAnnotations;

namespace SAloha.Api.Core.Comments;

/// <summary>
/// Commentaire sur un enregistrement de pratique (SOC-24) : public, ou note de travail
/// réservée à ceux qui traitent l'enregistrement. Désigne son enregistrement comme un
/// lien : type et identifiant (les modules ne se connaissent pas).
/// </summary>
public class Comment
{
    public const string Public = "Public";
    public const string WorkNote = "Note";
    public static readonly string[] Visibilities = [Public, WorkNote];
    public const int MaxBody = 4000;

    public int Id { get; set; }
    [MaxLength(20)] public string TargetType { get; set; } = "";
    public int TargetId { get; set; }
    [MaxLength(10)] public string Visibility { get; set; } = Public;
    [MaxLength(MaxBody)] public string Body { get; set; } = "";
    [MaxLength(100)] public string Author { get; set; } = "";
    [MaxLength(200)] public string AuthorDisplayName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public record CommentDto(string Type, int Id, string? Visibility, string Body);
