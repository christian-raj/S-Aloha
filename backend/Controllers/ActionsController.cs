using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProblemManagement.Api.Data;
using ProblemManagement.Api.Models;

namespace ProblemManagement.Api.Controllers;

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
            q = q.Where(a => a.Raci.Any(r => r.AssigneeId == assignee));
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
        if (!dto.Raci.Any(r => r.Role == "R"))
            return BadRequest(new { message = "Chaque action doit avoir au moins un Responsable (R)." });
        if (dto.Raci.Count(r => r.Role == "A") != 1)
            return BadRequest(new { message = "Chaque action doit avoir exactement un Approbateur (A)." });

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
        db.Actions.Add(action);
        await db.SaveChangesAsync();
        return Ok(action);
    }

    [HttpPut("api/actions/{id:int}")]
    public async Task<IActionResult> Update(int id, ActionDto dto)
    {
        var a = await db.Actions.Include(x => x.Raci).FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        a.Title = dto.Title; a.Description = dto.Description; a.DueDate = dto.DueDate;
        if (dto.Status != null)
        {
            a.Status = dto.Status;
            a.CompletedAt = dto.Status == "Terminée" ? DateTime.UtcNow : null;
        }
        db.RaciAssignments.RemoveRange(a.Raci);
        a.Raci = dto.Raci.Select(r => new RaciAssignment
        {
            Role = r.Role, AssigneeType = r.AssigneeType,
            AssigneeId = r.AssigneeId, AssigneeDisplayName = r.AssigneeDisplayName
        }).ToList();
        await db.SaveChangesAsync();
        return Ok(a);
    }

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
