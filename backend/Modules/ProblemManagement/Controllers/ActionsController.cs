using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Auth;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Perimeters;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ProblemManagement;

[ApiController]
[Authorize(Policy = "User")]
public class ActionsController(AppDbContext db, PerimeterAccess access) : ControllerBase
{
    private Task<int?> VisiblePerimeterAsync(int problemId) => ProblemScope.VisiblePerimeterAsync(db, access, problemId);

    /// <summary>Toutes les actions (suivi transverse), filtrables par responsable ou statut.</summary>
    [HttpGet("api/actions")]
    public async Task<IActionResult> All([FromQuery] string? assignee, [FromQuery] string? status)
    {
        var q = db.Actions.AsNoTracking()
            .Include(a => a.Raci).Include(a => a.Problem).AsQueryable();
        // PER-05 : les actions des problèmes de ses périmètres.
        var scope = await access.GetAsync();
        if (!scope.Global) { var ids = scope.Ids.ToList(); q = q.Where(a => ids.Contains(a.Problem!.PerimeterId)); }
        if (!string.IsNullOrEmpty(status)) q = q.Where(a => a.Status == status);
        if (!string.IsNullOrEmpty(assignee))
        {
            var key = assignee.ToLowerInvariant(); // l'AD ignore la casse (M2)
            // PRB-26 : mes actions comprennent celles confiées à l'un de mes groupes AD.
            var groups = key == User.MeKey() ? User.GroupKeys() : [];
            q = q.Where(a => a.Raci.Any(r => r.AssigneeType == "Group"
                ? groups.Contains(r.AssigneeId.ToLower()) : r.AssigneeId.ToLower() == key));
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
        if (await VisiblePerimeterAsync(problemId) is null) return NotFound();
        var invalid = ProblemRules.CheckRaci(dto.Raci);
        if (invalid is not null) return BadRequest(new { message = invalid });

        var action = new CorrectiveAction
        {
            ProblemId = problemId, Title = dto.Title, Description = dto.Description,
            // Une date reçue sans fuseau (« 2026-10-20 ») est lue en UTC : Npgsql la refuserait (500).
            DueDate = Dates.Utc(dto.DueDate), CreatedBy = User.Identity?.Name ?? "",
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
        var perimeter = a is null ? null : await VisiblePerimeterAsync(a.ProblemId);
        if (a is null || perimeter is null) return NotFound();

        // PRB-24 : modifier une action (titre, description, échéance, RACI) est réservé
        // à son R, son A ou un gestionnaire (M7) ; mettre à jour son statut reste ouvert à tous.
        // Un membre du groupe AD désigné R ou A en a les droits (PRB-26).
        var isManager = (await access.GetAsync()).ManagerOn(perimeter.Value);   // PER-06
        var isRorA = a.Raci.Any(r => r.Role is "R" or "A" && User.Designates(r.AssigneeType, r.AssigneeId));
        if (!isManager && !isRorA && !StatusOnly(a, dto)) return Forbid();

        // PRB-02 : statut fermé ; PRB-17 : les règles RACI valent aussi à la modification (#23).
        var invalid = Allowed.Check("Statut d'action", dto.Status, ProblemRules.ActionStatuses, optional: true)
                      ?? ProblemRules.CheckRaci(dto.Raci);
        if (invalid is not null) return BadRequest(new { message = invalid });
        a.Title = dto.Title; a.Description = dto.Description; a.DueDate = Dates.Utc(dto.DueDate);
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

    /// <summary>Vrai si la requête ne change que le statut de l'action.</summary>
    private static bool StatusOnly(CorrectiveAction a, ActionDto dto) =>
        dto.Title == a.Title && (dto.Description ?? "") == (a.Description ?? "")
        && Micro(Dates.Utc(dto.DueDate)) == Micro(Dates.Utc(a.DueDate))
        && dto.Raci.Count == a.Raci.Count
        && dto.Raci.All(r => a.Raci.Any(x => x.Role == r.Role && x.AssigneeType == r.AssigneeType
            && string.Equals(x.AssigneeId, r.AssigneeId, StringComparison.OrdinalIgnoreCase)));

    // PostgreSQL garde la microseconde, un DateTime le dixième de microseconde.
    private static long? Micro(DateTime? d) => d?.Ticks / 10;

    private static string? LengthError(CorrectiveAction a) =>
        Lengths.Check(a) ?? a.Raci.Select(Lengths.Check).FirstOrDefault(e => e is not null);

    [HttpDelete("api/actions/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var a = await db.Actions.FindAsync(id);
        var perimeter = a is null ? null : await VisiblePerimeterAsync(a.ProblemId);
        if (a is null || perimeter is null) return NotFound();
        // Supprimer une action : gestionnaire du périmètre du problème (PER-06).
        if (!(await access.GetAsync()).ManagerOn(perimeter.Value)) return Forbid();
        db.Actions.Remove(a);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
