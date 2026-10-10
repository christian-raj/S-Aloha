using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Audit;
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

    /// <summary>Graphe des transitions permises (SOC-05).</summary>
    [HttpGet("transitions")]
    public IActionResult GetTransitions() => Ok(ProblemManagement.Problem.Transitions);

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
        var invalid = ClosedValues(dto);
        if (invalid is not null) return BadRequest(new { message = invalid });
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
        var invalid = ClosedValues(dto) ?? Allowed.Check("Code de clôture", dto.ClosureCode, ProblemRules.ClosureCodes, optional: true);
        if (invalid is not null) return BadRequest(new { message = invalid });
        p.Title = dto.Title; p.Description = dto.Description;
        p.Impact = dto.Impact; p.Urgency = dto.Urgency;
        p.Priority = Priority.Compute(dto.Impact, dto.Urgency);
        p.Category = dto.Category; p.AffectedService = dto.AffectedService;
        p.KnownErrorWorkaround = dto.KnownErrorWorkaround;
        p.RootCause = dto.RootCause;
        p.ClosureCode = string.IsNullOrEmpty(dto.ClosureCode) ? null : dto.ClosureCode;
        var target = dto.Status ?? p.Status;
        var changing = target != p.Status;
        if (changing)
        {
            var (transition, forbidden) = ForcedTransition.Apply(HttpContext,
                StatusGraph.Check(ProblemManagement.Problem.Transitions, p.Status, target));
            if (forbidden) return Forbid();
            if (transition is not null) return BadRequest(new { message = transition });
        }
        var condition = await CheckStatusAsync(p, target, changing);
        if (condition is not null) return BadRequest(new { message = condition });
        if (changing)
        {
            var from = p.Status;
            p.Status = target;
            // Rouvert, un problème n'est plus clos : garder ClosedAt le faisait
            // compter dans le MTTR comme résolu (M4, revue du 2026-10-06).
            p.ClosedAt = target == "Clos" ? DateTime.UtcNow : null;
            // PRB-14 : ResolvedAt posé à l'entrée de Résolu, effacé au retour en analyse.
            if (target == "Résolu") p.ResolvedAt = DateTime.UtcNow;
            else if (target == "En analyse") p.ResolvedAt = null;
            // Une réouverture efface le code de clôture.
            if (from == "Clos") p.ClosureCode = null;
        }
        var error = Lengths.Check(p);
        if (error is not null) return BadRequest(new { message = error });
        p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(p);
    }

    /// <summary>Impact et urgence : valeurs fermées (PRB-02, #28).</summary>
    private static string? ClosedValues(ProblemDto dto) =>
        Allowed.Check("Impact", dto.Impact, Priority.Impacts) ?? Allowed.Check("Urgence", dto.Urgency, Priority.Urgencies);

    /// <summary>
    /// Conditions du statut visé (PRB-11 à PRB-13), vérifiées à chaque
    /// enregistrement (SOC-06). L'absence d'action ouverte (PRB-12) se vérifie
    /// au passage à Résolu : les actions vivent leur vie ensuite.
    /// </summary>
    private async Task<string?> CheckStatusAsync(ProblemManagement.Problem p, string target, bool changing)
    {
        switch (target)
        {
            case "Erreur connue" when string.IsNullOrWhiteSpace(p.KnownErrorWorkaround):
                return "Documenter le contournement : une erreur connue en a toujours un.";
            case "Résolu":
                if (string.IsNullOrWhiteSpace(p.RootCause)) return "Renseigner la cause racine validée pour résoudre le problème.";
                if (!changing) return null;
                var actions = await db.Actions.Where(a => a.ProblemId == p.Id).Select(a => a.Status).ToListAsync();
                if (actions.Any(s => ProblemRules.OpenActionStatuses.Contains(s)))
                    return "Des actions correctives sont encore ouvertes : les terminer ou les annuler avant de résoudre.";
                if (p.Status == "Erreur connue" && !actions.Contains("Terminée"))
                    return "Une erreur connue se résout par au moins une action corrective terminée.";
                return null;
            case "Clos":
                var allowed = changing ? ProblemRules.ClosureCodesFrom(p.Status) : ProblemRules.ClosureCodes;
                if (p.ClosureCode is null || !allowed.Contains(p.ClosureCode))
                    return $"Choisir un code de clôture : {string.Join(", ", allowed)}.";
                if (p.ClosureCode == "Doublon" && !await ItemLinks.IsLinkedToAsync(db, "problem", p.Id, "problem"))
                    return "Un doublon doit être relié au problème conservé (onglet Liens).";
                return null;
            default:
                return null;
        }
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
