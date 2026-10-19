namespace SAloha.Api.Core.Comments;

/// <summary>Commentaire renvoyé par l'API (A2, ADR-0016).</summary>
public record CommentView(int Id, string TargetType, int TargetId, string Visibility, string Body, string Author,
    string AuthorDisplayName, DateTime CreatedAt)
{
    public static CommentView From(Comment e) => new(e.Id, e.TargetType, e.TargetId, e.Visibility, e.Body, e.Author,
        e.AuthorDisplayName, e.CreatedAt);
}
