using System.Security.Claims;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Core.Notifications;

/// <summary>
/// Émission des notifications (SOC-21). Les événements s'accumulent pendant la requête et
/// s'enregistrent avec <see cref="SaveAsync"/> ; l'auteur de l'événement n'est jamais
/// notifié, et deux notifications identiques pour un même destinataire n'en font qu'une.
/// </summary>
public class Notifier(AppDbContext db, IHttpContextAccessor http)
{
    private readonly List<Notification> _pending = [];
    private ClaimsPrincipal? User => http.HttpContext?.User;
    private string Actor => (User?.Identity?.Name ?? "système").ToLowerInvariant();

    private void Add(string type, string id, int perimeterId, string targetType, int targetId, string reference, string title, string ev)
    {
        id = id.Trim().ToLowerInvariant();
        if (type != Notification.ToManagers && (id == "" || (type == Notification.ToUser && id == Actor))) return;
        if (_pending.Any(n => n.RecipientType == type && n.RecipientId == id && n.TargetType == targetType && n.TargetId == targetId && n.Event == ev))
            return;
        _pending.Add(new Notification
        {
            RecipientType = type, RecipientId = id, PerimeterId = perimeterId, TargetType = targetType, TargetId = targetId,
            Reference = reference, Title = title.Length > 200 ? title[..200] : title, Event = ev,
            Actor = Actor, ActorDisplayName = User?.FindFirst("displayName")?.Value ?? User?.Identity?.Name ?? "système"
        });
    }

    /// <summary>Un compte (sAMAccountName).</summary>
    public void ToUser(Record r, string type, string? account, string ev)
    {
        if (!string.IsNullOrWhiteSpace(account)) Add(Notification.ToUser, account, r.PerimeterId, type, r.Id, r.Reference, r.Title, ev);
    }

    /// <summary>Un utilisateur ou un groupe AD désigné (responsable, RACI).</summary>
    public void ToPrincipal(Record r, string type, string? principalType, string? principalId, string ev)
    {
        if (string.IsNullOrWhiteSpace(principalId)) return;
        Add(principalType == "Group" ? Notification.ToGroup : Notification.ToUser, principalId, r.PerimeterId, type, r.Id, r.Reference, r.Title, ev);
    }

    /// <summary>Le responsable de l'enregistrement (utilisateur ou groupe).</summary>
    public void ToOwner(Record r, string type, string ev) => ToPrincipal(r, type, r.OwnerType, r.OwnerId, ev);

    /// <summary>Le déclarant (créateur) de l'enregistrement.</summary>
    public void ToCreator(Record r, string type, string ev) => ToUser(r, type, r.CreatedBy, ev);

    /// <summary>Les gestionnaires du périmètre de l'enregistrement (et les Admin).</summary>
    public void ToManagers(Record r, string type, string ev) =>
        Add(Notification.ToManagers, "", r.PerimeterId, type, r.Id, r.Reference, r.Title, ev);

    public async Task SaveAsync()
    {
        if (_pending.Count == 0) return;
        db.Notifications.AddRange(_pending);
        _pending.Clear();
        await db.SaveChangesAsync();
    }
}
