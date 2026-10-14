using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ServiceRequestManagement;

/// <summary>
/// REQ-20 : catalogue de demandes. Consulté par tous (modèles actifs par défaut), tenu par
/// les gestionnaires ; un modèle se désactive au lieu de se supprimer, pour garder
/// l'historique des demandes qui en viennent (suppression : Admin, sans demande liée).
/// </summary>
[ApiController]
[Authorize(Policy = "User")]
[Route("api/request-catalog")]
public class RequestCatalogController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool all = false) =>
        Ok(await db.RequestCatalogItems.AsNoTracking().Where(x => all || x.Active).OrderBy(x => x.Name).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id) =>
        await db.RequestCatalogItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) is { } item ? Ok(item) : NotFound();

    private async Task<string?> CheckAsync(RequestCatalogItemDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return "Nommer le modèle (objet demandé).";
        if (dto.TargetHours is <= 0) return "Le délai cible est un nombre d'heures positif.";
        if (dto.ServiceId is int s && !await db.Services.AnyAsync(x => x.Id == s && x.Status != "Retiré"))
            return "Choisir un service du catalogue qui n'est pas retiré.";
        return null;
    }

    private static void Fill(RequestCatalogItem x, RequestCatalogItemDto dto)
    {
        x.Name = dto.Name.Trim(); x.Description = dto.Description?.Trim() ?? ""; x.ServiceId = dto.ServiceId;
        x.RequiresApproval = dto.RequiresApproval; x.TargetHours = dto.TargetHours;
        x.FulfillmentGroup = string.IsNullOrWhiteSpace(dto.FulfillmentGroup) ? null : dto.FulfillmentGroup.Trim();
        x.Active = dto.Active; x.UpdatedAt = DateTime.UtcNow;
    }

    [HttpPost]
    [Authorize(Policy = "Manager")]
    public async Task<IActionResult> Create(RequestCatalogItemDto dto)
    {
        var error = await CheckAsync(dto);
        var item = new RequestCatalogItem { CreatedBy = User.Identity?.Name ?? "" };
        Fill(item, dto);
        error ??= Lengths.Check(item);
        if (error is not null) return BadRequest(new { message = error });
        db.RequestCatalogItems.Add(item);
        await db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Manager")]
    public async Task<IActionResult> Update(int id, RequestCatalogItemDto dto)
    {
        var item = await db.RequestCatalogItems.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        var error = await CheckAsync(dto);
        Fill(item, dto);
        error ??= Lengths.Check(item);
        if (error is not null) return BadRequest(new { message = error });
        await db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await db.RequestCatalogItems.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        if (await db.ServiceRequests.AnyAsync(r => r.CatalogItemId == id))
            return BadRequest(new { message = "Des demandes viennent de ce modèle : le désactiver plutôt que le supprimer." });
        db.RequestCatalogItems.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
