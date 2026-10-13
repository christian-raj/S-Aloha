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

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var me = User.MeKey();
        var mine = await MineAsync();
        var items = await mine.OrderByDescending(n => n.CreatedAt).Take(50)
            .Select(n => new
            {
                n.Id, n.TargetType, n.TargetId, n.Reference, n.Title, n.Event, n.ActorDisplayName, n.CreatedAt,
                Read = db.NotificationReads.Any(r => r.NotificationId == n.Id && r.User == me)
            }).ToListAsync();
        var unread = await mine.CountAsync(n => !db.NotificationReads.Any(r => r.NotificationId == n.Id && r.User == me));
        return Ok(new { Unread = unread, Items = items });
    }

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> Read(long id)
    {
        var me = User.MeKey();
        if (!await (await MineAsync()).AnyAsync(n => n.Id == id)) return NotFound();
        if (!await db.NotificationReads.AnyAsync(r => r.NotificationId == id && r.User == me))
        {
            db.NotificationReads.Add(new NotificationRead { NotificationId = id, User = me });
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll()
    {
        var me = User.MeKey();
        var ids = await (await MineAsync()).Where(n => !db.NotificationReads.Any(r => r.NotificationId == n.Id && r.User == me))
            .Select(n => n.Id).ToListAsync();
        db.NotificationReads.AddRange(ids.Select(id => new NotificationRead { NotificationId = id, User = me }));
        await db.SaveChangesAsync();
        return NoContent();
    }
}
