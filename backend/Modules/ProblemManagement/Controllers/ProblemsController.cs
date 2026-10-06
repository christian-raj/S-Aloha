using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;

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
        // La référence part du plus grand numéro de l'année, pas du nombre de
        // problèmes : compter redonnait un numéro existant dès qu'un problème
        // avait été supprimé, et l'index unique bloquait alors TOUTES les
        // déclarations suivantes (B2, revue du 2026-10-06). Deux déclarations
        // simultanées lisent le même maximum : l'index unique tranche, la
        // perdante relit et réessaie (B3).
        for (var attempt = 1; ; attempt++)
        {
            var p = new Problem
            {
                Reference = await NextReferenceAsync(),
                Title = dto.Title, Description = dto.Description,
                Impact = dto.Impact, Urgency = dto.Urgency,
                Priority = ComputePriority(dto.Impact, dto.Urgency),
                Category = dto.Category, AffectedService = dto.AffectedService,
                CreatedBy = Me, CreatedByDisplayName = MyDisplay
            };
            db.Problems.Add(p);
            try
            {
                await db.SaveChangesAsync();
                return CreatedAtAction(nameof(Get), new { id = p.Id }, p);
            }
            catch (DbUpdateException) when (attempt < MaxReferenceAttempts)
            {
                db.Entry(p).State = EntityState.Detached;
                // Toute autre erreur d'enregistrement que la collision de
                // référence remonte telle quelle.
                if (!await db.Problems.AnyAsync(x => x.Reference == p.Reference)) throw;
                await Task.Delay(Random.Shared.Next(5, 25 * attempt));
            }
        }
    }

    private const int MaxReferenceAttempts = 10;

    private async Task<string> NextReferenceAsync()
    {
        var prefix = $"PRB-{DateTime.UtcNow.Year}-";
        // Numéro sur 4 chiffres complétés de zéros : l'ordre alphabétique est
        // l'ordre numérique jusqu'à 9999 problèmes dans l'année.
        var last = await db.Problems.Where(x => x.Reference.StartsWith(prefix))
            .OrderByDescending(x => x.Reference).Select(x => x.Reference).FirstOrDefaultAsync();
        var n = last is null ? 0 : int.Parse(last[prefix.Length..]);
        return $"{prefix}{n + 1:D4}";
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
