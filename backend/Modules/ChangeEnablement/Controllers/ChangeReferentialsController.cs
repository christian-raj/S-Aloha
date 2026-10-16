using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ChangeEnablement;

/// <summary>
/// CHG-25 : périodes de gel, communes à toute l'organisation. Consultées par tous (calendrier),
/// tenues par les gestionnaires.
/// </summary>
[ApiController]
[Authorize(Policy = "User")]
[Route("api/change-freezes")]
public class ChangeFreezesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateTime? from)
    {
        var start = Dates.Utc(from) ?? DateTime.UtcNow.Date.AddDays(-7);
        return Ok(await db.ChangeFreezes.AsNoTracking().Where(f => f.End >= start).OrderBy(f => f.Start).ToListAsync());
    }

    [HttpPost]
    [Authorize(Policy = "Manager")]
    public async Task<IActionResult> Create(FreezeDto dto)
    {
        var f = new ChangeFreeze
        {
            Start = Dates.Utc(dto.Start)!.Value, End = Dates.Utc(dto.End)!.Value, Reason = dto.Reason?.Trim() ?? "",
            CreatedBy = User.Identity?.Name ?? ""
        };
        if (f.End <= f.Start) return BadRequest(new { message = "La fin du gel doit suivre son début." });
        if (f.Reason == "") return BadRequest(new { message = "Motiver la période de gel (clôture comptable, fêtes…)." });
        var error = Lengths.Check(f);
        if (error is not null) return BadRequest(new { message = error });
        db.ChangeFreezes.Add(f);
        await db.SaveChangesAsync();
        return Ok(f);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var f = await db.ChangeFreezes.FirstOrDefaultAsync(x => x.Id == id);
        if (f is null) return NotFound();
        db.ChangeFreezes.Remove(f);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

/// <summary>
/// CHG-26 : modèles de changement standard. Consultés par tous (création d'un Standard), tenus
/// par les gestionnaires ; désactivés plutôt que supprimés tant qu'un changement en vient.
/// </summary>
[ApiController]
[Authorize(Policy = "User")]
[Route("api/standard-changes")]
public class StandardChangeModelsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool all = false) =>
        Ok(await db.StandardChangeModels.AsNoTracking().Where(m => all || m.Active).OrderBy(m => m.Name).ToListAsync());

    private static string? Fill(StandardChangeModel m, StandardChangeModelDto dto)
    {
        var error = Allowed.Check("Risque", dto.Risk, Change.Risks);
        if (error is not null) return error;
        if (string.IsNullOrWhiteSpace(dto.Name)) return "Nommer le modèle.";
        if (string.IsNullOrWhiteSpace(dto.ImplementationPlan) || string.IsNullOrWhiteSpace(dto.BackoutPlan))
            return "Un modèle standard porte un plan de mise en œuvre et un plan de retour arrière : il est évalué une fois pour toutes.";
        m.Name = dto.Name.Trim(); m.Description = dto.Description?.Trim() ?? "";
        m.ImplementationPlan = dto.ImplementationPlan.Trim(); m.BackoutPlan = dto.BackoutPlan.Trim();
        m.Risk = dto.Risk!; m.Active = dto.Active; m.UpdatedAt = DateTime.UtcNow;
        return Lengths.Check(m);
    }

    [HttpPost]
    [Authorize(Policy = "Manager")]
    public async Task<IActionResult> Create(StandardChangeModelDto dto)
    {
        var m = new StandardChangeModel { CreatedBy = User.Identity?.Name ?? "" };
        var error = Fill(m, dto);
        if (error is not null) return BadRequest(new { message = error });
        db.StandardChangeModels.Add(m);
        await db.SaveChangesAsync();
        return Ok(m);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Manager")]
    public async Task<IActionResult> Update(int id, StandardChangeModelDto dto)
    {
        var m = await db.StandardChangeModels.FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();
        var error = Fill(m, dto);
        if (error is not null) return BadRequest(new { message = error });
        await db.SaveChangesAsync();
        return Ok(m);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var m = await db.StandardChangeModels.FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();
        if (await db.Changes.AnyAsync(c => c.StandardModelId == id))
            return BadRequest(new { message = "Des changements viennent de ce modèle : le désactiver plutôt que le supprimer." });
        db.StandardChangeModels.Remove(m);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
