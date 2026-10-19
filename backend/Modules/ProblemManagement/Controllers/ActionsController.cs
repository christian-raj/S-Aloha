using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Auth;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Links;
using SAloha.Api.Core.Notifications;
using SAloha.Api.Core.Perimeters;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ProblemManagement;

[ApiController]
[Authorize(Policy = "User")]
public class ActionsController(AppDbContext db, PerimeterAccess access, Notifier notify) : ControllerBase
{
    /// <summary>PRB-31 : action affectée → son R et son A (nouvelles affectations seulement).</summary>
    private async Task NotifyAssigneesAsync(CorrectiveAction a, IEnumerable<RaciAssignment> before)
    {
        var problem = await db.Problems.AsNoTracking().FirstAsync(p => p.Id == a.ProblemId);
        var ev = $"Action corrective affectée : {a.Title}";
        foreach (var r in a.Raci.Where(r => r.Role is "R" or "A"
                     && !before.Any(b => b.Role == r.Role && b.AssigneeType == r.AssigneeType
                                         && string.Equals(b.AssigneeId, r.AssigneeId, StringComparison.OrdinalIgnoreCase))))
            notify.ToPrincipal(problem, "problem", r.AssigneeType, r.AssigneeId, ev.Length > 200 ? ev[..200] : ev);
        await notify.SaveAsync();
    }

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
        // Les affectations RACI sortent en DTO (A2).
        var view = items.Select(i => new { i.Id, i.Title, i.Status, i.DueDate, i.CompletedAt, i.Problem, Raci = i.Raci.Select(RaciAssignmentView.From) });
        return Ok(view);
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
        await NotifyAssigneesAsync(action, []);
        return Ok(CorrectiveActionView.From(action));
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
        // PRB-25 : une action ne passe à Terminée qu'avec l'accord de son A (ou d'un gestionnaire) ;
        // son R la passe En cours puis demande l'approbation.
        if (dto.Status == "Terminée" && a.Status != "Terminée" && !isManager && !IsAccountable(a))
            return StatusCode(403, new { message = "Seul l'approbateur (A) de l'action ou un gestionnaire la déclare terminée : demander l'approbation." });
        a.Title = dto.Title; a.Description = dto.Description; a.DueDate = Dates.Utc(dto.DueDate);
        if (dto.Status != null && dto.Status != a.Status)
        {
            // Toute décision sur le statut clôt la demande d'approbation en cours.
            a.ApprovalRequestedAt = null; a.ApprovalRequestedBy = null;
            // PRB-18 : CompletedAt posé au seul passage à Terminée, effacé à la sortie (#24) ;
            // renommer une action terminée ne le change pas.
            a.CompletedAt = dto.Status == "Terminée" ? DateTime.UtcNow : null;
            a.Status = dto.Status;
        }
        var raciBefore = a.Raci.Select(r => new RaciAssignment { Role = r.Role, AssigneeType = r.AssigneeType, AssigneeId = r.AssigneeId }).ToList();
        db.RaciAssignments.RemoveRange(a.Raci);
        a.Raci = dto.Raci.Select(r => new RaciAssignment
        {
            Role = r.Role, AssigneeType = r.AssigneeType,
            AssigneeId = r.AssigneeId, AssigneeDisplayName = r.AssigneeDisplayName
        }).ToList();
        var error = LengthError(a);
        if (error is not null) return BadRequest(new { message = error });
        await db.SaveChangesAsync();
        await NotifyAssigneesAsync(a, raciBefore);
        return Ok(CorrectiveActionView.From(a));
    }

    private bool IsAccountable(CorrectiveAction a) =>
        a.Raci.Any(r => r.Role == "A" && User.Designates(r.AssigneeType, r.AssigneeId));

    /// <summary>
    /// PRB-25 : le R d'une action En cours demande à son A de l'approuver (notification à l'A).
    /// </summary>
    [HttpPost("api/actions/{id:int}/request-approval")]
    public async Task<IActionResult> RequestApproval(int id)
    {
        var a = await db.Actions.Include(x => x.Raci).FirstOrDefaultAsync(x => x.Id == id);
        var perimeter = a is null ? null : await VisiblePerimeterAsync(a.ProblemId);
        if (a is null || perimeter is null) return NotFound();
        var isR = a.Raci.Any(r => r.Role == "R" && User.Designates(r.AssigneeType, r.AssigneeId));
        if (!isR && !(await access.GetAsync()).ManagerOn(perimeter.Value)) return Forbid();
        if (a.Status != "En cours")
            return BadRequest(new { message = "Passer l'action En cours avant de demander son approbation." });
        if (!a.Raci.Any(r => r.Role == "A"))
            return BadRequest(new { message = "L'action n'a pas d'approbateur (A) : en désigner un dans la matrice RACI." });
        a.ApprovalRequestedAt = DateTime.UtcNow;
        a.ApprovalRequestedBy = User.Identity?.Name;
        await db.SaveChangesAsync();
        var problem = await db.Problems.AsNoTracking().FirstAsync(p => p.Id == a.ProblemId);
        var ev = $"Approbation demandée : {a.Title}";
        foreach (var r in a.Raci.Where(r => r.Role == "A"))
            notify.ToPrincipal(problem, "problem", r.AssigneeType, r.AssigneeId, ev.Length > 200 ? ev[..200] : ev);
        await notify.SaveAsync();
        return Ok(CorrectiveActionView.From(a));
    }

    /// <summary>
    /// PRB-27, CHG-23 : rattache à l'action le changement qui met en œuvre son correctif
    /// (créé depuis l'action par l'interface) et relie ce changement au problème.
    /// </summary>
    [HttpPut("api/actions/{id:int}/change")]
    public async Task<IActionResult> SetChange(int id, ActionChangeDto dto)
    {
        var a = await db.Actions.Include(x => x.Raci).FirstOrDefaultAsync(x => x.Id == id);
        var perimeter = a is null ? null : await VisiblePerimeterAsync(a.ProblemId);
        if (a is null || perimeter is null) return NotFound();
        var scope = await access.GetAsync();
        var isRorA = a.Raci.Any(r => r.Role is "R" or "A" && User.Designates(r.AssigneeType, r.AssigneeId));
        if (!isRorA && !scope.ManagerOn(perimeter.Value)) return Forbid();
        var change = await db.Changes.AsNoTracking()
            .Where(c => c.Reference == dto.Reference.Trim().ToUpper())
            .Select(c => new { c.Id, c.Reference, c.PerimeterId }).FirstOrDefaultAsync();
        if (change is null || !scope.Sees(change.PerimeterId))
            return BadRequest(new { message = $"Changement introuvable : {dto.Reference}." });
        a.ChangeId = change.Id; a.ChangeReference = change.Reference;
        var linked = await db.ItemLinks.AnyAsync(l =>
            (l.FromType == "problem" && l.FromId == a.ProblemId && l.ToType == "change" && l.ToId == change.Id) ||
            (l.FromType == "change" && l.FromId == change.Id && l.ToType == "problem" && l.ToId == a.ProblemId));
        if (!linked)
            db.ItemLinks.Add(new ItemLink { FromType = "problem", FromId = a.ProblemId, ToType = "change", ToId = change.Id, CreatedBy = User.Identity?.Name ?? "" });
        await db.SaveChangesAsync();
        return Ok(CorrectiveActionView.From(a));
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
