using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Links;

namespace SAloha.Api.Core.Records;

/// <summary>
/// API CRUD commune des processus : liste filtrée, détail, création avec
/// référence, modification avec contrôle des statuts, suppression (Admin).
/// Chaque module ne décrit que ce qui lui est propre (champs, statuts,
/// statuts réservés aux gestionnaires, effets d'un changement de statut).
/// </summary>
[ApiController]
[Authorize(Policy = "User")]
public abstract class RecordController<T, TDto>(AppDbContext db) : ControllerBase
    where T : Record, new()
    where TDto : IRecordDto
{
    protected AppDbContext Db => db;
    protected string Me => User.Identity?.Name ?? "";
    protected string MyDisplay => User.FindFirst("displayName")?.Value ?? Me;
    protected bool IsManager => User.IsInRole("Manager") || User.IsInRole("Admin");

    /// <summary>Préfixe de référence (INC, CHG…).</summary>
    protected abstract string Code { get; }
    /// <summary>Type de l'enregistrement dans les liens inter-processus.</summary>
    protected abstract string LinkType { get; }
    /// <summary>Statuts admis ; le premier est le statut de création par défaut.</summary>
    protected abstract string[] Statuses { get; }
    /// <summary>Référentiel : création et modification réservées aux gestionnaires.</summary>
    protected virtual bool ManagerOnly => false;

    /// <summary>Recopie les champs propres au processus : message d'erreur (400) ou null.</summary>
    protected abstract string? Apply(T e, TDto dto, bool creating);
    /// <summary>Contrôles nécessitant la base (existence d'un parent…).</summary>
    protected virtual Task<string?> ValidateAsync(T e) => Task.FromResult<string?>(null);
    /// <summary>Statut de création : le premier statut, sauf règle propre au processus.</summary>
    protected virtual string InitialStatus(T e) => Statuses[0];
    /// <summary>Inventaires (CI, services) : le statut de création est choisi à la saisie.</summary>
    protected virtual bool StatusChosenAtCreation => false;
    /// <summary>Statuts que seul un gestionnaire peut poser (autoriser, approuver, publier…).</summary>
    protected virtual bool RequiresManager(string status) => false;
    /// <summary>Pré-condition d'un changement de statut : message d'erreur (400) ou null.</summary>
    protected virtual string? CheckTransition(T e, string to) => null;
    /// <summary>Effets d'un changement de statut (horodatages) ; <c>from</c> vide à la création.</summary>
    protected virtual void OnStatusChanged(T e, string from) { }
    protected virtual IQueryable<T> Filter(IQueryable<T> q) => q;
    protected virtual IQueryable<T> Search(IQueryable<T> q, string key) =>
        q.Where(x => x.Title.ToLower().Contains(key) || x.Reference.ToLower().Contains(key));
    protected virtual IQueryable<T> WithDetails(IQueryable<T> q) => q;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? q, [FromQuery] string? owner)
    {
        var query = Filter(db.Set<T>().AsNoTracking());
        if (!string.IsNullOrEmpty(status)) query = query.Where(x => x.Status == status);
        // Insensible à la casse, comme l'annuaire (M2, M5).
        if (!string.IsNullOrWhiteSpace(q)) query = Search(query, q.Trim().ToLower());
        if (!string.IsNullOrEmpty(owner))
        {
            var key = (owner == "me" ? Me : owner).ToLower();
            query = query.Where(x => x.OwnerId != null && x.OwnerId.ToLower() == key);
        }
        return Ok(await query.OrderByDescending(x => x.CreatedAt).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var e = await WithDetails(db.Set<T>().AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id);
        return e is null ? NotFound() : Ok(e);
    }

    [HttpPost]
    public async Task<IActionResult> Create(TDto dto)
    {
        if (ManagerOnly && !IsManager) return Forbid();
        var probe = new T();
        var error = Fill(probe, dto, true) ?? await ValidateAsync(probe)
            ?? (StatusChosenAtCreation ? Allowed.Check("Statut", dto.Status, Statuses, optional: true) : null);
        if (error is not null) return BadRequest(new { message = error });

        var e = await References.CreateAsync(db, Code, () =>
        {
            var x = new T();
            Fill(x, dto, true);
            x.Status = StatusChosenAtCreation && !string.IsNullOrEmpty(dto.Status) ? dto.Status : InitialStatus(x);
            x.CreatedBy = Me; x.CreatedByDisplayName = MyDisplay;
            OnStatusChanged(x, "");
            return x;
        });
        return CreatedAtAction(nameof(Get), new { id = e.Id }, e);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TDto dto)
    {
        var e = await db.Set<T>().FindAsync(id);
        if (e is null) return NotFound();
        if (ManagerOnly && !IsManager) return Forbid();
        var error = Fill(e, dto, false) ?? await ValidateAsync(e);
        if (error is not null) return BadRequest(new { message = error });

        if (dto.Status is not null && dto.Status != e.Status)
        {
            error = Allowed.Check("Statut", dto.Status, Statuses);
            if (error is not null) return BadRequest(new { message = error });
            if (RequiresManager(dto.Status) && !IsManager) return Forbid();
            error = CheckTransition(e, dto.Status);
            if (error is not null) return BadRequest(new { message = error });
            var from = e.Status;
            e.Status = dto.Status;
            OnStatusChanged(e, from);
        }
        e.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(e);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var e = await db.Set<T>().FindAsync(id);
        if (e is null) return NotFound();
        db.Set<T>().Remove(e);
        ItemLinks.RemoveFor(db, LinkType, id);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private string? Fill(T e, TDto dto, bool creating)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) return "Le titre est obligatoire.";
        var error = Allowed.Check("Type de responsable", dto.OwnerType, ["User", "Group"], optional: true);
        if (error is not null) return error;
        e.Title = dto.Title.Trim();
        e.Description = dto.Description ?? "";
        var hasOwner = !string.IsNullOrWhiteSpace(dto.OwnerId);
        e.OwnerType = hasOwner ? dto.OwnerType ?? "User" : null;
        e.OwnerId = hasOwner ? dto.OwnerId : null;
        e.OwnerDisplayName = hasOwner ? dto.OwnerDisplayName ?? dto.OwnerId : null;
        return Apply(e, dto, creating);
    }
}
