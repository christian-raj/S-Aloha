using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProblemManagement.Api.Data;
using ProblemManagement.Api.Models;

namespace ProblemManagement.Api.Controllers;

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
            query = query.Where(p => p.Title.Contains(q) || p.Reference.Contains(q));
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
        var year = DateTime.UtcNow.Year;
        var count = await db.Problems.CountAsync(p => p.CreatedAt.Year == year);
        var p = new Problem
        {
            Reference = $"PRB-{year}-{count + 1:D4}",
            Title = dto.Title, Description = dto.Description,
            Impact = dto.Impact, Urgency = dto.Urgency,
            Priority = ComputePriority(dto.Impact, dto.Urgency),
            Category = dto.Category, AffectedService = dto.AffectedService,
            CreatedBy = Me, CreatedByDisplayName = MyDisplay
        };
        db.Problems.Add(p);
        await db.SaveChangesAsync();
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
        p.Priority = ComputePriority(dto.Impact, dto.Urgency);
        p.Category = dto.Category; p.AffectedService = dto.AffectedService;
        p.KnownErrorWorkaround = dto.KnownErrorWorkaround;
        p.RootCause = dto.RootCause;
        if (dto.Status != null && dto.Status != p.Status)
        {
            p.Status = dto.Status;
            if (dto.Status == "Clos") p.ClosedAt = DateTime.UtcNow;
        }
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
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string ComputePriority(string impact, string urgency)
    {
        int i = impact switch { "Élevé" => 3, "Moyen" => 2, _ => 1 };
        int u = urgency switch { "Élevée" => 3, "Moyenne" => 2, _ => 1 };
        return (i + u) switch { 6 => "P1", 5 => "P2", 4 => "P3", _ => "P4" };
    }
}
