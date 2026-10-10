using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ProblemManagement;

[ApiController]
[Authorize(Policy = "User")]
public class ActionsController(AppDbContext db) : ControllerBase
{
    /// <summary>Toutes les actions (suivi transverse), filtrables par responsable ou statut.</summary>
    [HttpGet("api/actions")]
    public async Task<IActionResult> All([FromQuery] string? assignee, [FromQuery] string? status)
    {
        var q = db.Actions.AsNoTracking()
            .Include(a => a.Raci).Include(a => a.Problem).AsQueryable();
        if (!string.IsNullOrEmpty(status)) q = q.Where(a => a.Status == status);
        if (!string.IsNullOrEmpty(assignee))
        {
            var key = assignee.ToLowerInvariant(); // l'AD ignore la casse (M2)
            q = q.Where(a => a.Raci.Any(r => r.AssigneeId.ToLower() == key));
        }
        var items = await q.OrderBy(a => a.DueDate).Select(a => new
        {
            a.Id, a.Title, a.Status, a.DueDate, a.CompletedAt,
            Problem = new { a.Problem!.Id, a.Problem.Reference, a.Problem.Title },
            a.Raci
        }).ToListAsync();
        return Ok(items);
    }

    [HttpPost("api/problems/{problemId:int}/actions")]
    public async Task<IActionResult> Create(int problemId, ActionDto dto)
    {
        if (!await db.Problems.AnyAsync(p => p.Id == problemId)) return NotFound();
        var invalid = ProblemRules.CheckRaci(dto.Raci);
        if (invalid is not null) return BadRequest(new { message = invalid });

        var action = new CorrectiveAction
        {
            ProblemId = problemId, Title = dto.Title, Description = dto.Description,
            DueDate = dto.DueDate, CreatedBy = User.Identity?.Name ?? "",
            Raci = dto.Raci.Select(r => new RaciAssignment
            {
                Role = r.Role, AssigneeType = r.AssigneeType,
                AssigneeId = r.AssigneeId, AssigneeDisplayName = r.AssigneeDisplayName
            }).ToList()
        };
        var error = LengthError(action);
        if (error is not null) return BadRequest(new { message = error });
        db.Actions.Add(action);
        await db.SaveChangesAsync();
        return Ok(action);
    }

    [HttpPut("api/actions/{id:int}")]
    public async Task<IActionResult> Update(int id, ActionDto dto)
    {
        var a = await db.Actions.Include(x => x.Raci).FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        // PRB-02 : statut fermé ; PRB-17 : les règles RACI valent aussi à la modification (#23).
        var invalid = Allowed.Check("Statut d'action", dto.Status, ProblemRules.ActionStatuses, optional: true)
                      ?? ProblemRules.CheckRaci(dto.Raci);
        if (invalid is not null) return BadRequest(new { message = invalid });
        a.Title = dto.Title; a.Description = dto.Description; a.DueDate = dto.DueDate;
        if (dto.Status != null && dto.Status != a.Status)
        {
            // PRB-18 : CompletedAt posé au seul passage à Terminée, effacé à la sortie (#24) ;
            // renommer une action terminée ne le change pas.
            a.CompletedAt = dto.Status == "Terminée" ? DateTime.UtcNow : null;
            a.Status = dto.Status;
        }
        db.RaciAssignments.RemoveRange(a.Raci);
        a.Raci = dto.Raci.Select(r => new RaciAssignment
        {
            Role = r.Role, AssigneeType = r.AssigneeType,
            AssigneeId = r.AssigneeId, AssigneeDisplayName = r.AssigneeDisplayName
        }).ToList();
        var error = LengthError(a);
        if (error is not null) return BadRequest(new { message = error });
        await db.SaveChangesAsync();
        return Ok(a);
    }

    private static string? LengthError(CorrectiveAction a) =>
        Lengths.Check(a) ?? a.Raci.Select(Lengths.Check).FirstOrDefault(e => e is not null);

    [HttpDelete("api/actions/{id:int}")]
    [Authorize(Policy = "Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var a = await db.Actions.FindAsync(id);
        if (a is null) return NotFound();
        db.Actions.Remove(a);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
