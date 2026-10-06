using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Core.Links;

public record LinkDto(string FromType, int FromId, string ToReference);

/// <summary>Liens inter-processus, lus dans les deux sens.</summary>
[ApiController]
[Route("api/links")]
[Authorize(Policy = "User")]
public class LinksController(AppDbContext db) : ControllerBase
{
    /// <summary>Enregistrements reliés à (type, id), quel que soit le sens du lien.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string type, [FromQuery] int id)
    {
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
                return new { o.LinkId, o.Type, t.Id, t.Reference, t.Title, t.Status };
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
        if (!await from.Query(db).AnyAsync(t => t.Id == dto.FromId)) return NotFound();

        var reference = dto.ToReference.Trim().ToUpper();
        var to = ItemLinks.ByReference(reference);
        var target = to is null ? null : await to.Query(db).FirstOrDefaultAsync(t => t.Reference == reference);
        if (to is null || target is null)
            return BadRequest(new { message = $"Aucun enregistrement de référence « {dto.ToReference} »." });
        if (to.Type == from.Type && target.Id == dto.FromId)
            return BadRequest(new { message = "Un enregistrement ne peut pas être relié à lui-même." });
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
        return Ok(new { LinkId = link.Id, to.Type, target.Id, target.Reference, target.Title, target.Status });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var link = await db.ItemLinks.FindAsync(id);
        if (link is null) return NotFound();
        db.ItemLinks.Remove(link);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
