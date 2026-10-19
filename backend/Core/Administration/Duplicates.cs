using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Audit;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Links;
using SAloha.Api.Core.Records;
using SAloha.Api.Modules.ChangeEnablement;
using SAloha.Api.Modules.ContinualImprovement;
using SAloha.Api.Modules.IncidentManagement;
using SAloha.Api.Modules.KnowledgeManagement;
using SAloha.Api.Modules.ProblemManagement;
using SAloha.Api.Modules.ServiceRequestManagement;

namespace SAloha.Api.Core.Administration;

/// <summary>ADM-42 : paire écartée par l'Admin (« pas un doublon »), identifiants dans l'ordre croissant.</summary>
public class DuplicateDismissal
{
    public int Id { get; set; }
    [MaxLength(20)] public string Type { get; set; } = "";
    public int FirstId { get; set; }
    public int SecondId { get; set; }
    [MaxLength(100)] public string DismissedBy { get; set; } = "";
    public DateTime DismissedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// ADM-40, ADM-41 : doublons probables (même pratique, même périmètre, titre normalisé
/// identique, créés à 7 jours au plus) et fusion d'un doublon dans l'enregistrement conservé.
/// </summary>
public class Duplicates(AppDbContext db)
{
    public static readonly TimeSpan Window = TimeSpan.FromDays(7);

    /// <summary>Pratiques examinées : les CI, services, accords, évaluations et projets en sont exclus (registres tenus à la main).</summary>
    private static readonly Dictionary<string, (Func<AppDbContext, IQueryable<Record>> Set, Func<AppDbContext, int, Task<List<Deletion.Impact>>> Cascade)> Practices = new()
    {
        ["incident"] = (d => d.Incidents, (d, id) => Deletion.CascadeAsync<Incident>(d, id)),
        ["request"] = (d => d.ServiceRequests, (d, id) => Deletion.CascadeAsync<ServiceRequest>(d, id)),
        ["problem"] = (d => d.Problems, (d, id) => Deletion.CascadeAsync<Problem>(d, id)),
        ["change"] = (d => d.Changes, (d, id) => Deletion.CascadeAsync<Change>(d, id)),
        ["article"] = (d => d.KnowledgeArticles, (d, id) => Deletion.CascadeAsync<KnowledgeArticle>(d, id)),
        ["improvement"] = (d => d.Improvements, (d, id) => Deletion.CascadeAsync<Improvement>(d, id)),
    };

    public static bool Supports(string type) => Practices.ContainsKey(type);

    /// <summary>Titre comparé : minuscules, sans accents, lettres et chiffres séparés d'une seule espace.</summary>
    public static string Normalize(string title)
    {
        var decomposed = title.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public record Side(int Id, string Reference, string Title, string Status, DateTime CreatedAt, int PerimeterId);
    public record Pair(string Type, Side First, Side Second);

    /// <summary>Paires probables, toutes pratiques examinées, les plus récentes d'abord, 200 au plus.</summary>
    public async Task<List<Pair>> FindAsync()
    {
        var dismissed = (await db.DuplicateDismissals.AsNoTracking().Select(d => new { d.Type, d.FirstId, d.SecondId }).ToListAsync())
            .Select(d => (d.Type, d.FirstId, d.SecondId)).ToHashSet();
        var pairs = new List<Pair>();
        foreach (var (type, practice) in Practices)
        {
            var rows = await practice.Set(db).AsNoTracking()
                .Select(r => new Side(r.Id, r.Reference, r.Title, r.Status, r.CreatedAt, r.PerimeterId)).ToListAsync();
            foreach (var group in rows.GroupBy(r => (r.PerimeterId, Key: Normalize(r.Title))).Where(g => g.Key.Key != "" && g.Count() > 1))
            {
                var ordered = group.OrderBy(r => r.CreatedAt).ToList();
                for (var i = 0; i < ordered.Count; i++)
                    for (var j = i + 1; j < ordered.Count && ordered[j].CreatedAt - ordered[i].CreatedAt <= Window; j++)
                    {
                        var (a, b) = ordered[i].Id < ordered[j].Id ? (ordered[i], ordered[j]) : (ordered[j], ordered[i]);
                        if (!dismissed.Contains((type, a.Id, b.Id))) pairs.Add(new Pair(type, a, b));
                    }
            }
        }
        return pairs.OrderByDescending(p => p.Second.CreatedAt).Take(200).ToList();
    }

    public async Task DismissAsync(string type, int a, int b, string by)
    {
        var (first, second) = a < b ? (a, b) : (b, a);
        if (!await db.DuplicateDismissals.AnyAsync(d => d.Type == type && d.FirstId == first && d.SecondId == second))
        {
            db.DuplicateDismissals.Add(new DuplicateDismissal { Type = type, FirstId = first, SecondId = second, DismissedBy = by });
            await db.SaveChangesAsync();
        }
    }

    public record MergeResult(string? Error, int LinksMoved, int CommentsMoved);

    /// <summary>
    /// ADM-41 : reporte les liens et commentaires du doublon sur l'enregistrement conservé, puis
    /// supprime le doublon ; refusé si le doublon porte des éléments propres à sa pratique.
    /// </summary>
    public async Task<MergeResult> MergeAsync(string type, int keepId, int removeId, string author, string authorDisplay)
    {
        var practice = Practices[type];
        if (keepId == removeId) return new("Choisir deux enregistrements différents.", 0, 0);
        var keep = await practice.Set(db).FirstOrDefaultAsync(r => r.Id == keepId);
        var remove = await practice.Set(db).FirstOrDefaultAsync(r => r.Id == removeId);
        if (keep is null || remove is null) return new("Enregistrement introuvable.", 0, 0);
        if (keep.PerimeterId != remove.PerimeterId) return new("Les deux enregistrements ne sont pas dans le même périmètre.", 0, 0);
        var own = await practice.Cascade(db, removeId);
        if (own.Count > 0)
            return new($"{remove.Reference} porte {string.Join(", ", own.Select(i => $"{i.Count} {i.Label}"))} : à traiter à la main avant la fusion.", 0, 0);

        await using var tx = await db.Database.BeginTransactionAsync();
        var keepLinks = await db.ItemLinks.Where(l => (l.FromType == type && l.FromId == keepId) || (l.ToType == type && l.ToId == keepId))
            .Select(l => l.FromType == type && l.FromId == keepId ? new { T = l.ToType, I = l.ToId } : new { T = l.FromType, I = l.FromId }).ToListAsync();
        var linked = keepLinks.Select(x => (x.T, x.I)).ToHashSet();
        var moved = 0;
        foreach (var link in await db.ItemLinks.Where(l => (l.FromType == type && l.FromId == removeId) || (l.ToType == type && l.ToId == removeId)).ToListAsync())
        {
            var fromRemoved = link.FromType == type && link.FromId == removeId;
            var other = fromRemoved ? (link.ToType, link.ToId) : (link.FromType, link.FromId);
            // Sans lien vers lui-même ni lien en double (SOC-12).
            if (other == (type, keepId) || !linked.Add(other)) { db.ItemLinks.Remove(link); continue; }
            if (fromRemoved) link.FromId = keepId; else link.ToId = keepId;
            moved++;
        }
        var comments = await db.Comments.Where(c => c.TargetType == type && c.TargetId == removeId).ToListAsync();
        foreach (var c in comments) c.TargetId = keepId;
        // Notifications du doublon : elles mèneraient à une fiche disparue (comme SOC-14).
        var notifications = db.Notifications.Where(n => n.TargetType == type && n.TargetId == removeId);
        db.NotificationReads.RemoveRange(db.NotificationReads.Where(r => notifications.Any(n => n.Id == r.NotificationId)));
        db.Notifications.RemoveRange(notifications);
        var now = DateTime.UtcNow;
        AuditEntry Trace(Record r, string text) => new()
        {
            EntityType = type, EntityId = r.Id, Reference = r.Reference, Action = "Fusion", NewValue = text,
            Author = author, AuthorDisplayName = authorDisplay, At = now
        };
        db.AuditEntries.Add(Trace(keep, $"{remove.Reference} « {remove.Title} » fusionné ici : {moved} lien(s), {comments.Count} commentaire(s) reportés."));
        db.AuditEntries.Add(Trace(remove, $"Fusionné dans {keep.Reference} puis supprimé."));
        db.Remove(remove);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(null, moved, comments.Count);
    }
}
