using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Perimeters;

namespace SAloha.Api.Modules.ServiceConfigurationManagement.Controllers;

/// <summary>
/// CFG-55 : relations proposées par la découverte. Le gestionnaire du périmètre les accepte —
/// la relation entre alors dans la CMDB, aux règles des relations saisies (CFG-14) — ou les
/// écarte ; rien n'entre dans la CMDB sans sa décision.
/// </summary>
[ApiController]
[Route("api/discovery/relations")]
[Authorize(Policy = "User")]
public class RelationProposalsController(AppDbContext db, PerimeterAccess access) : ControllerBase
{
    private async Task<IQueryable<RelationProposal>?> ManagedAsync()
    {
        var scope = await access.GetAsync();
        if (scope.Global) return db.RelationProposals;
        var ids = scope.ManagedIds.ToList();
        return ids.Count == 0 ? null : db.RelationProposals.Where(r => ids.Contains(r.PerimeterId));
    }

    private static object Side(DiscoveredAsset a) => new
    {
        a.Id, a.Hostname, a.IpAddress, a.Status,
        Ci = a.MatchedCi is null ? null : new { a.MatchedCi.Id, a.MatchedCi.Reference, a.MatchedCi.Title, a.MatchedCi.Status }
    };

    /// <summary>Les deux actifs rapprochés de CI non retirés : la relation peut entrer dans la CMDB.</summary>
    private static bool Ready(RelationProposal r) =>
        r.SourceAsset?.MatchedCi is { Status: not "Retiré" } && r.TargetAsset?.MatchedCi is { Status: not "Retiré" }
        && r.SourceAsset.Status == DiscoveredAsset.Reconciled && r.TargetAsset.Status == DiscoveredAsset.Reconciled;

    private static object View(RelationProposal r) => new
    {
        r.Id, r.PerimeterId, r.Type, r.Reason, r.Status, r.CreatedAt, r.LastSeenAt, r.DecidedAt, r.DecidedBy, r.RelationId,
        Source = Side(r.SourceAsset!), Target = Side(r.TargetAsset!), Ready = Ready(r)
    };

    private static IQueryable<RelationProposal> WithAssets(IQueryable<RelationProposal> q) =>
        q.Include(r => r.SourceAsset).ThenInclude(a => a!.MatchedCi).Include(r => r.TargetAsset).ThenInclude(a => a!.MatchedCi);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int? asset = null)
    {
        if (await ManagedAsync() is not { } query) return Forbid();
        if (!string.IsNullOrEmpty(status)) query = query.Where(r => r.Status == status);
        if (asset is not null) query = query.Where(r => r.SourceAssetId == asset || r.TargetAssetId == asset);
        var list = await WithAssets(query.AsNoTracking()).OrderByDescending(r => r.LastSeenAt).Take(500).ToListAsync();
        return Ok(list.Select(View));
    }

    [HttpPost("{id:int}/accept")]
    public async Task<IActionResult> Accept(int id)
    {
        if (await ManagedAsync() is not { } query) return Forbid();
        var r = await WithAssets(query).FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return NotFound();
        if (r.Status != RelationProposal.Proposed) return BadRequest(new { message = $"Proposition déjà {r.Status.ToLowerInvariant()}." });
        if (!Ready(r))
            return BadRequest(new { message = "Rapprocher d'abord les deux actifs de leurs CI (non retirés) : la relation relie des CI de la CMDB." });
        var (source, target) = (r.SourceAsset!.MatchedCi!, r.TargetAsset!.MatchedCi!);
        if (source.Id == target.Id) return BadRequest(new { message = "Les deux actifs sont rapprochés du même CI." });
        var relation = await db.CiRelationships.FirstOrDefaultAsync(x => x.SourceId == source.Id && x.TargetId == target.Id && x.Type == r.Type);
        if (relation is null)
        {
            relation = new CiRelationship { SourceId = source.Id, TargetId = target.Id, Type = r.Type };
            db.CiRelationships.Add(relation);
            await db.SaveChangesAsync();
        }
        (r.Status, r.RelationId, r.DecidedAt, r.DecidedBy) = (RelationProposal.Accepted, relation.Id, DateTime.UtcNow, User.Identity?.Name);
        await db.SaveChangesAsync();
        return Ok(View(r));
    }

    [HttpPost("{id:int}/dismiss")]
    public async Task<IActionResult> Dismiss(int id)
    {
        if (await ManagedAsync() is not { } query) return Forbid();
        var r = await WithAssets(query).FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return NotFound();
        if (r.Status != RelationProposal.Proposed) return BadRequest(new { message = $"Proposition déjà {r.Status.ToLowerInvariant()}." });
        (r.Status, r.DecidedAt, r.DecidedBy) = (RelationProposal.Dismissed, DateTime.UtcNow, User.Identity?.Name);
        await db.SaveChangesAsync();
        return Ok(View(r));
    }
}
