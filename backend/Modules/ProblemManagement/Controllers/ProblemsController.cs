using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Itil;
using SAloha.Api.Core.Links;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ProblemManagement;

[ApiController]
[Route("api/problems")]
[Authorize(Policy = "User")]
public class ProblemsController(AppDbContext db) : ControllerBase
{
    private string Me => User.Identity?.Name ?? "";
    private string MyDisplay => User.FindFirst("displayName")?.Value ?? Me;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? q)
    {
        var query = db.Problems.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(p => p.Status == status);
        if (!string.IsNullOrEmpty(q))
        {
            // Insensible à la casse : Contains devient un LIKE, sensible à la
            // casse sous PostgreSQL — « vpn » ne trouvait pas « VPN » (M5).
            var key = q.Trim().ToLower();
            query = query.Where(p => p.Title.ToLower().Contains(key) || p.Reference.ToLower().Contains(key));
        }
        var items = await query.OrderByDescending(p => p.CreatedAt)
            .Select(p => new { p.Id, p.Reference, p.Title, p.Status, p.Priority, p.Category,
                               p.AffectedService, p.CreatedByDisplayName, p.CreatedAt,
                               ActionsCount = p.Actions.Count }).ToListAsync();
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var p = await db.Problems.AsNoTracking()
            .Include(x => x.Analyses)
            .Include(x => x.Actions).ThenInclude(a => a.Raci)
            .FirstOrDefaultAsync(x => x.Id == id);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPost]
    public async Task<IActionResult> Create(ProblemDto dto)
    {
        Problem Build() => new()
        {
            Title = dto.Title, Description = dto.Description,
            Impact = dto.Impact, Urgency = dto.Urgency,
            Priority = Priority.Compute(dto.Impact, dto.Urgency),
            Category = dto.Category, AffectedService = dto.AffectedService,
            CreatedBy = Me, CreatedByDisplayName = MyDisplay
        };
        var error = Lengths.Check(Build());
        if (error is not null) return BadRequest(new { message = error });
        var p = await References.CreateAsync(db, "PRB", Build);
        return CreatedAtAction(nameof(Get), new { id = p.Id }, p);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Manager")]
    public async Task<IActionResult> Update(int id, ProblemDto dto)
    {
        var p = await db.Problems.FindAsync(id);
        if (p is null) return NotFound();
        p.Title = dto.Title; p.Description = dto.Description;
        p.Impact = dto.Impact; p.Urgency = dto.Urgency;
        p.Priority = Priority.Compute(dto.Impact, dto.Urgency);
        p.Category = dto.Category; p.AffectedService = dto.AffectedService;
        p.KnownErrorWorkaround = dto.KnownErrorWorkaround;
        p.RootCause = dto.RootCause;
        if (dto.Status != null && dto.Status != p.Status)
        {
            p.Status = dto.Status;
            // Rouvert, un problème n'est plus clos : garder ClosedAt le faisait
            // compter dans le MTTR comme résolu (M4, revue du 2026-10-06).
            p.ClosedAt = dto.Status == "Clos" ? DateTime.UtcNow : null;
        }
        var error = Lengths.Check(p);
        if (error is not null) return BadRequest(new { message = error });
        p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(p);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await db.Problems.FindAsync(id);
        if (p is null) return NotFound();
        db.Problems.Remove(p);
        ItemLinks.RemoveFor(db, "problem", id);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
