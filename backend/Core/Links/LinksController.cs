using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Notifications;
using SAloha.Api.Core.Perimeters;

namespace SAloha.Api.Core.Links;

public record LinkDto(string FromType, int FromId, string ToReference);

/// <summary>Liens inter-processus, lus dans les deux sens.</summary>
[ApiController]
[Route("api/links")]
[Authorize(Policy = "User")]
public class LinksController(AppDbContext db, PerimeterAccess access, Notifier notify) : ControllerBase
{
    /// <summary>Enregistrements reliés à (type, id), quel que soit le sens du lien.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string type, [FromQuery] int id)
    {
        var scope = await access.GetAsync();
        if (!await SeesAsync(scope, type, id)) return NotFound();
        var links = await db.ItemLinks.AsNoTracking()
            .Where(l => (l.FromType == type && l.FromId == id) || (l.ToType == type && l.ToId == id))
            .ToListAsync();
        var others = links.Select(l => l.FromType == type && l.FromId == id
            ? (LinkId: l.Id, Type: l.ToType, Id: l.ToId)
            : (LinkId: l.Id, Type: l.FromType, Id: l.FromId)).ToList();

        var result = new List<object>();
        foreach (var group in others.GroupBy(o => o.Type))
        {
            var kind = ItemLinks.ByType(group.Key);
            if (kind is null) continue;
            var ids = group.Select(o => o.Id).ToList();
            var targets = await kind.Query(db).Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id);
            // Un lien vers un enregistrement disparu est ignoré.
            result.AddRange(group.Where(o => targets.ContainsKey(o.Id)).Select(o =>
            {
                var t = targets[o.Id];
                // PER-08 : le lien se voit, l'enregistrement d'un autre périmètre non.
                return scope.Sees(t)
                    ? new { o.LinkId, o.Type, t.Id, t.Reference, t.Title, t.Status, Hidden = false }
                    : new { o.LinkId, o.Type, Id = 0, Reference = "", Title = "Enregistrement d'un autre périmètre", Status = "", Hidden = true };
            }));
        }
        return Ok(result);
    }

    /// <summary>Relie (type, id) à l'enregistrement désigné par sa référence.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(LinkDto dto)
    {
        var from = ItemLinks.ByType(dto.FromType);
        if (from is null) return BadRequest(new { message = $"Type inconnu : « {dto.FromType} »." });
        var scope = await access.GetAsync();
        var source = await from.Query(db).FirstOrDefaultAsync(t => t.Id == dto.FromId);
        if (source is null || !scope.Sees(source)) return NotFound();

        var reference = dto.ToReference.Trim().ToUpper();
        var to = ItemLinks.ByReference(reference);
        var target = to is null ? null : await to.Query(db).FirstOrDefaultAsync(t => t.Reference == reference);
        // PER-08 : relier suppose de voir les deux ; hors de ses périmètres, la référence n'existe pas.
        if (to is null || target is null || !scope.Sees(target))
            return BadRequest(new { message = $"Aucun enregistrement de référence « {dto.ToReference} »." });
        if (to.Type == from.Type && target.Id == dto.FromId)
            return BadRequest(new { message = "Un enregistrement ne peut pas être relié à lui-même." });
        // CFG-14 : un CI retiré ne peut plus être relié.
        if (from.Type == "ci" && await db.ConfigurationItems.AnyAsync(c => c.Id == dto.FromId && c.Status == "Retiré"))
            return BadRequest(new { message = "Un CI retiré ne peut plus être relié." });
        if (to.Type == "ci" && target.Status == "Retiré")
            return BadRequest(new { message = "Un CI retiré ne peut plus être relié." });
        if (await db.ItemLinks.AnyAsync(l =>
                (l.FromType == from.Type && l.FromId == dto.FromId && l.ToType == to.Type && l.ToId == target.Id) ||
                (l.FromType == to.Type && l.FromId == target.Id && l.ToType == from.Type && l.ToId == dto.FromId)))
            return Conflict(new { message = "Ce lien existe déjà." });

        var link = new ItemLink
        {
            FromType = from.Type, FromId = dto.FromId, ToType = to.Type, ToId = target.Id,
            CreatedBy = User.Identity?.Name ?? ""
        };
        db.ItemLinks.Add(link);
        await db.SaveChangesAsync();
        // PRB-23, CSI-20 : nouvel incident relié à un problème résolu ou clos.
        await Recurrence.OnLinkAsync(db, notify, link, User.Identity?.Name ?? "", User.FindFirst("displayName")?.Value ?? User.Identity?.Name ?? "");
        return Ok(new { LinkId = link.Id, to.Type, target.Id, target.Reference, target.Title, target.Status });
    }

    /// <summary>L'enregistrement (type, id) existe et l'utilisateur le voit.</summary>
    private async Task<bool> SeesAsync(PerimeterScope scope, string type, int id)
    {
        var kind = ItemLinks.ByType(type);
        var t = kind is null ? null : await kind.Query(db).FirstOrDefaultAsync(x => x.Id == id);
        return t is not null && scope.Sees(t);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var link = await db.ItemLinks.FindAsync(id);
        if (link is null) return NotFound();
        var scope = await access.GetAsync();
        if (!await SeesAsync(scope, link.FromType, link.FromId) || !await SeesAsync(scope, link.ToType, link.ToId)) return NotFound();
        db.ItemLinks.Remove(link);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
