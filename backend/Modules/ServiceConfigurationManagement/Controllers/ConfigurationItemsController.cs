using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ServiceConfigurationManagement;

[Route("api/configuration-items")]
public class ConfigurationItemsController(AppDbContext db) : RecordController<ConfigurationItem, ConfigurationItemDto>(db)
{
    protected override string Code => "CI";
    protected override string LinkType => "ci";
    protected override string[] Statuses => ConfigurationItem.Statuses;
    protected override bool StatusChosenAtCreation => true;
    protected override string InitialStatus(ConfigurationItem e) => "En service";

    protected override string? Apply(ConfigurationItem e, ConfigurationItemDto dto, bool creating)
    {
        var error = Allowed.Check("Type de CI", dto.CiType, ConfigurationItem.Types)
                    ?? Allowed.Check("Environnement", dto.Environment, ConfigurationItem.Environments);
        if (error is not null) return error;
        e.CiType = dto.CiType!; e.Environment = dto.Environment!;
        e.Location = dto.Location ?? "";
        return null;
    }

    protected override IQueryable<ConfigurationItem> Filter(IQueryable<ConfigurationItem> q)
    {
        var type = Request.Query["type"].ToString();
        return string.IsNullOrEmpty(type) ? q : q.Where(c => c.CiType == type);
    }

    protected override IQueryable<ConfigurationItem> WithDetails(IQueryable<ConfigurationItem> q) =>
        q.Include(c => c.Outgoing).ThenInclude(r => r.Target)
         .Include(c => c.Incoming).ThenInclude(r => r.Source);

    /// <summary>Relie ce CI à un autre, désigné par sa référence.</summary>
    [HttpPost("{id:int}/relations")]
    public async Task<IActionResult> AddRelation(int id, CiRelationshipDto dto)
    {
        var error = Allowed.Check("Type de relation", dto.Type, CiRelationship.Types);
        if (error is not null) return BadRequest(new { message = error });
        if (!await Db.ConfigurationItems.AnyAsync(c => c.Id == id)) return NotFound();
        var reference = dto.TargetReference.Trim().ToUpper();
        var target = await Db.ConfigurationItems.FirstOrDefaultAsync(c => c.Reference == reference);
        if (target is null) return BadRequest(new { message = $"Aucun CI de référence « {dto.TargetReference} »." });
        if (target.Id == id) return BadRequest(new { message = "Un CI ne peut pas être relié à lui-même." });
        if (await Db.CiRelationships.AnyAsync(r => r.SourceId == id && r.TargetId == target.Id && r.Type == dto.Type))
            return Conflict(new { message = "Cette relation existe déjà." });

        var rel = new CiRelationship { SourceId = id, TargetId = target.Id, Type = dto.Type };
        Db.CiRelationships.Add(rel);
        await Db.SaveChangesAsync();
        return Ok(new { rel.Id, rel.Type, Target = new { target.Id, target.Reference, target.Title } });
    }

    [HttpDelete("relations/{relationId:int}")]
    public async Task<IActionResult> RemoveRelation(int relationId)
    {
        var rel = await Db.CiRelationships.FindAsync(relationId);
        if (rel is null) return NotFound();
        Db.CiRelationships.Remove(rel);
        await Db.SaveChangesAsync();
        return NoContent();
    }
}
