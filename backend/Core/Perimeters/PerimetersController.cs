using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Core.Perimeters;

/// <summary>
/// Périmètres et affectations (docs/reference/perimetres.md) : sélecteurs pour
/// tous, gestion par l'Admin (PER-01, PER-02, PER-12). Les créations,
/// modifications et suppressions sont tracées au journal d'audit (PER-11).
/// </summary>
[ApiController]
[Authorize(Policy = "User")]
public class PerimetersController(AppDbContext db, PerimeterAccess access) : ControllerBase
{
    private string Me => User.Identity?.Name ?? "";

    /// <summary>
    /// Mes périmètres en service, avec mon rôle sur chacun (PER-03) : sélecteurs de
    /// création et de déplacement. L'Admin les a tous.
    /// </summary>
    [HttpGet("api/perimeters")]
    public async Task<IActionResult> Visible()
    {
        var scope = await access.GetAsync();
        var ids = scope.Ids.ToList();
        var list = await db.Perimeters.AsNoTracking()
            .Where(p => p.DeletedAt == null && (scope.Global || ids.Contains(p.Id))).OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.Description }).ToListAsync();
        return Ok(list.Select(p => new { p.Id, p.Name, p.Description,
            Role = scope.Global ? "Admin" : scope.Roles[p.Id] }));
    }

    /// <summary>Tous les périmètres, supprimés compris (grisés, restaurables).</summary>
    [HttpGet("api/admin/perimeters")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> List() =>
        Ok(await db.Perimeters.AsNoTracking().OrderBy(p => p.DeletedAt != null).ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.Name, p.Description, p.DeletedAt, p.DeletedBy,
                Assignments = db.PerimeterAssignments.Count(a => a.PerimeterId == p.Id)
            }).ToListAsync());

    /// <summary>Fiche : compteurs par pratique (PER-12) et affectations.</summary>
    [HttpGet("api/admin/perimeters/{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Get(int id)
    {
        var p = await db.Perimeters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        var assignments = await db.PerimeterAssignments.AsNoTracking().Where(a => a.PerimeterId == id)
            .OrderBy(a => a.Role).ThenBy(a => a.PrincipalDisplayName).Select(a => PerimeterAssignmentView.From(a)).ToListAsync();
        return Ok(new { p.Id, p.Name, p.Description, p.CreatedAt, p.CreatedBy, p.DeletedAt, p.DeletedBy,
            IsDefault = p.Id == Perimeter.DefaultId, Counts = await CountsAsync(id), Assignments = assignments });
    }

    [HttpPost("api/admin/perimeters")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Create(PerimeterDto dto)
    {
        var p = new Perimeter { CreatedBy = Me };
        var error = await FillAsync(p, dto);
        if (error is not null) return error;
        db.Perimeters.Add(p);
        await db.SaveChangesAsync();
        return Ok(PerimeterView.From(p));
    }

    [HttpPut("api/admin/perimeters/{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Update(int id, PerimeterDto dto)
    {
        var p = await db.Perimeters.FindAsync(id);
        if (p is null) return NotFound();
        var error = await FillAsync(p, dto);
        if (error is not null) return error;
        await db.SaveChangesAsync();
        return Ok(PerimeterView.From(p));
    }

    /// <summary>Suppression douce : le périmètre quitte les sélecteurs, se restaure (PER-01).</summary>
    [HttpDelete("api/admin/perimeters/{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await db.Perimeters.FindAsync(id);
        if (p is null) return NotFound();
        if (p.Id == Perimeter.DefaultId)
            return BadRequest(new { message = $"Le périmètre « {Perimeter.DefaultName} » ne se supprime pas : il reçoit les enregistrements sans autre périmètre." });
        if (p.DeletedAt is null) { p.DeletedAt = DateTime.UtcNow; p.DeletedBy = Me; await db.SaveChangesAsync(); }
        return NoContent();
    }

    [HttpPost("api/admin/perimeters/{id:int}/restore")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Restore(int id)
    {
        var p = await db.Perimeters.FindAsync(id);
        if (p is null) return NotFound();
        if (p.DeletedAt is not null) { p.DeletedAt = null; p.DeletedBy = null; await db.SaveChangesAsync(); }
        return Ok(PerimeterView.From(p));
    }

    /// <summary>Suppression définitive : seulement sans enregistrement ni affectation (409 avec les compteurs).</summary>
    [HttpDelete("api/admin/perimeters/{id:int}/purge")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Purge(int id)
    {
        var p = await db.Perimeters.FindAsync(id);
        if (p is null) return NotFound();
        if (p.Id == Perimeter.DefaultId)
            return BadRequest(new { message = $"Le périmètre « {Perimeter.DefaultName} » ne se supprime pas." });
        var counts = await CountsAsync(id);
        var assignments = await db.PerimeterAssignments.CountAsync(a => a.PerimeterId == id);
        var records = counts.Sum(c => c.Count);
        if (records > 0 || assignments > 0)
            return Conflict(new
            {
                message = $"Le périmètre « {p.Name} » porte encore {records} enregistrement(s) et {assignments} affectation(s) : "
                          + "rattachez-les ailleurs ou supprimez-le en douceur.",
                Counts = counts, Assignments = assignments
            });
        db.Perimeters.Remove(p);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("api/admin/perimeters/{id:int}/assignments")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Assignments(int id) =>
        await db.Perimeters.AnyAsync(p => p.Id == id)
            ? Ok(await db.PerimeterAssignments.AsNoTracking().Where(a => a.PerimeterId == id)
                .OrderBy(a => a.Role).ThenBy(a => a.PrincipalDisplayName).Select(a => PerimeterAssignmentView.From(a)).ToListAsync())
            : NotFound();

    /// <summary>Affecte un utilisateur ou un groupe AD avec le rôle Manager ou User (PER-02).</summary>
    [HttpPost("api/admin/perimeters/{id:int}/assignments")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Assign(int id, AssignmentDto dto)
    {
        var p = await db.Perimeters.FindAsync(id);
        if (p is null) return NotFound();
        if (p.DeletedAt is not null) return BadRequest(new { message = "Ce périmètre est supprimé : restaurez-le avant d'y affecter quelqu'un." });
        var error = Allowed.Check("Type", dto.PrincipalType, PerimeterAssignment.PrincipalTypes)
                    ?? (dto.Role == "Admin"
                        ? "Un administrateur a une portée globale : il ne s'affecte pas à un périmètre."
                        : Allowed.Check("Rôle", dto.Role, PerimeterAssignment.Roles));
        if (error is not null) return BadRequest(new { message = error });
        if (string.IsNullOrWhiteSpace(dto.PrincipalId)) return BadRequest(new { message = "Choisir un utilisateur ou un groupe de l'annuaire." });
        var key = dto.PrincipalId.Trim().ToLower();
        if (await db.PerimeterAssignments.AnyAsync(a => a.PerimeterId == id && a.PrincipalType == dto.PrincipalType
                                                        && a.PrincipalId.ToLower() == key && a.Role == dto.Role))
            return Conflict(new { message = "Cette affectation existe déjà." });
        var a = new PerimeterAssignment
        {
            PerimeterId = id, PrincipalType = dto.PrincipalType, PrincipalId = dto.PrincipalId.Trim(),
            PrincipalDisplayName = string.IsNullOrWhiteSpace(dto.PrincipalDisplayName) ? dto.PrincipalId.Trim() : dto.PrincipalDisplayName.Trim(),
            Role = dto.Role, CreatedBy = Me
        };
        var length = Lengths.Check(a);
        if (length is not null) return BadRequest(new { message = length });
        db.PerimeterAssignments.Add(a);
        await db.SaveChangesAsync();
        return Ok(PerimeterAssignmentView.From(a));
    }

    [HttpDelete("api/admin/assignments/{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Unassign(int id)
    {
        var a = await db.PerimeterAssignments.FindAsync(id);
        if (a is null) return NotFound();
        db.PerimeterAssignments.Remove(a);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Nom obligatoire et unique sans tenir compte de la casse, supprimés compris (PER-01).</summary>
    private async Task<IActionResult?> FillAsync(Perimeter p, PerimeterDto dto)
    {
        var name = dto.Name?.Trim() ?? "";
        if (name == "") return BadRequest(new { message = "Le nom du périmètre est obligatoire." });
        var key = name.ToLower();
        var twin = await db.Perimeters.AsNoTracking().Where(x => x.Id != p.Id && x.Name.ToLower() == key)
            .Select(x => new { x.Name, x.DeletedAt }).FirstOrDefaultAsync();
        if (twin is not null)
            return Conflict(new { message = twin.DeletedAt is null
                ? $"Un périmètre « {twin.Name} » existe déjà."
                : $"Un périmètre supprimé porte déjà ce nom (« {twin.Name} ») : restaurez-le plutôt." });
        p.Name = name;
        p.Description = dto.Description?.Trim() ?? "";
        var length = Lengths.Check(p);
        return length is null ? null : BadRequest(new { message = length });
    }

    /// <summary>Enregistrements rattachés au périmètre, par pratique (PER-12).</summary>
    private async Task<List<PracticeCount>> CountsAsync(int id) =>
    [
        new("Incidents", await db.Incidents.CountAsync(x => x.PerimeterId == id)),
        new("Demandes", await db.ServiceRequests.CountAsync(x => x.PerimeterId == id)),
        new("Problèmes", await db.Problems.CountAsync(x => x.PerimeterId == id)),
        new("Changements", await db.Changes.CountAsync(x => x.PerimeterId == id)),
        new("Configuration", await db.ConfigurationItems.CountAsync(x => x.PerimeterId == id)),
        new("Services", await db.Services.CountAsync(x => x.PerimeterId == id)),
        new("Accords (SLA)", await db.Agreements.CountAsync(x => x.PerimeterId == id)),
        new("Connaissances", await db.KnowledgeArticles.CountAsync(x => x.PerimeterId == id)),
        new("Améliorations", await db.Improvements.CountAsync(x => x.PerimeterId == id)),
        new("Évaluations NIS 2", await db.Assessments.CountAsync(x => x.PerimeterId == id)),
        new("Projets", await db.Projects.CountAsync(x => x.PerimeterId == id)),
    ];

    public record PracticeCount(string Practice, int Count);
}
