using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Auth;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Perimeters;

namespace SAloha.Api.Core.Notifications;

/// <summary>
/// Mes notifications (SOC-21) : celles adressées à mon compte, à l'un de mes groupes AD, ou
/// aux gestionnaires d'un périmètre que je gère ; jamais pour un enregistrement que je ne
/// vois pas (SOC-23). Les 30 derniers jours, 50 au plus.
/// </summary>
[ApiController]
[Route("api/notifications")]
[Authorize(Policy = "User")]
public class NotificationsController(AppDbContext db, PerimeterAccess access) : ControllerBase
{
    private async Task<IQueryable<Notification>> MineAsync()
    {
        var me = User.MeKey();
        var groups = User.GroupKeys();
        var scope = await access.GetAsync();
        var all = scope.Global;
        var seen = scope.Ids.ToList();
        var managed = scope.ManagedIds.ToList();
        var since = DateTime.UtcNow.AddDays(-30);
        return db.Notifications.AsNoTracking().Where(n => n.CreatedAt >= since && n.Actor != me
            && (all || seen.Contains(n.PerimeterId))
            && ((n.RecipientType == Notification.ToUser && n.RecipientId == me)
                || (n.RecipientType == Notification.ToGroup && groups.Contains(n.RecipientId))
                || (n.RecipientType == Notification.ToManagers && (all || managed.Contains(n.PerimeterId)))));
    }

    /// <summary>
    /// Mes notifications, une par événement : reçue à plusieurs titres (comme A d'une action
    /// et comme gestionnaire), elle ne s'affiche et ne se compte qu'une fois.
    /// </summary>
    private async Task<List<(Notification N, bool Read)>> DistinctAsync()
    {
        var me = User.MeKey();
        var rows = await (await MineAsync()).OrderByDescending(n => n.CreatedAt).Take(300)
            .Select(n => new { N = n, Read = db.NotificationReads.Any(r => r.NotificationId == n.Id && r.User == me) })
            .ToListAsync();
        return rows.GroupBy(x => (x.N.TargetType, x.N.TargetId, x.N.Event))
            .Select(g => (g.First().N, g.Any(x => x.Read))).ToList();
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var mine = await DistinctAsync();
        return Ok(new
        {
            Unread = mine.Count(x => !x.Read),
            Items = mine.Take(50).Select(x => new
            {
                x.N.Id, x.N.TargetType, x.N.TargetId, x.N.Reference, x.N.Title, x.N.Event, x.N.ActorDisplayName, x.N.CreatedAt, x.Read
            })
        });
    }

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> Read(long id)
    {
        var me = User.MeKey();
        var mine = await (await MineAsync()).ToListAsync();
        var n = mine.FirstOrDefault(x => x.Id == id);
        if (n is null) return NotFound();
        // Le même événement reçu à plusieurs titres se lit d'un coup.
        await MarkReadAsync(me, mine.Where(x => x.TargetType == n.TargetType && x.TargetId == n.TargetId && x.Event == n.Event).Select(x => x.Id));
        return NoContent();
    }

    private async Task MarkReadAsync(string me, IEnumerable<long> ids)
    {
        var list = ids.ToList();
        var already = await db.NotificationReads.Where(r => r.User == me && list.Contains(r.NotificationId)).Select(r => r.NotificationId).ToListAsync();
        db.NotificationReads.AddRange(list.Except(already).Select(id => new NotificationRead { NotificationId = id, User = me }));
        await db.SaveChangesAsync();
    }

    /// <summary>Relève tout de suite les événements datés (Admin ; la tâche périodique le fait seule).</summary>
    [HttpPost("run")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Run([FromServices] DatedEvents events) =>
        Ok(new { Events = await events.RunAsync(DateTime.UtcNow) });

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll()
    {
        var me = User.MeKey();
        await MarkReadAsync(me, await (await MineAsync()).Select(n => n.Id).ToListAsync());
        return NoContent();
    }
}
