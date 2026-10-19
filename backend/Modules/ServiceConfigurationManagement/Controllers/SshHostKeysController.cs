using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Perimeters;

namespace SAloha.Api.Modules.ServiceConfigurationManagement.Controllers;

/// <summary>
/// CFG-53 : clés d'hôte SSH retenues au premier contact. Les gestionnaires voient celles de
/// leurs périmètres, dont les clés changées ; seul l'Admin accepte une nouvelle clé ou oublie
/// un hôte (la clé suivante sera alors retenue comme au premier contact).
/// </summary>
[ApiController]
[Route("api/discovery/ssh-host-keys")]
[Authorize(Policy = "User")]
public class SshHostKeysController(AppDbContext db, PerimeterAccess access) : ControllerBase
{
    private static object View(SshHostKey k) => new
    {
        k.Id, k.PerimeterId, k.Protocol, k.Host, k.Port, k.Algorithm, k.Fingerprint, k.FirstSeenAt, k.LastSeenAt,
        k.PendingAlgorithm, k.PendingFingerprint, k.PendingSeenAt, k.AcceptedAt, k.AcceptedBy,
        Changed = k.PendingFingerprint != null
    };

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var scope = await access.GetAsync();
        var managed = scope.ManagedIds.ToList();
        if (!scope.Global && managed.Count == 0) return Forbid();
        var keys = await db.SshHostKeys.AsNoTracking().Where(k => scope.Global || managed.Contains(k.PerimeterId))
            .OrderByDescending(k => k.PendingFingerprint != null).ThenBy(k => k.Host).ToListAsync();
        return Ok(keys.Select(View));
    }

    /// <summary>Accepte la clé changée : elle devient la clé de confiance de l'hôte.</summary>
    [HttpPost("{id:int}/accept")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Accept(int id)
    {
        var key = await db.SshHostKeys.FirstOrDefaultAsync(k => k.Id == id);
        if (key is null) return NotFound();
        if (key.PendingFingerprint is null) return BadRequest(new { message = "Aucune clé changée à accepter pour cet hôte." });
        (key.Algorithm, key.Fingerprint) = (key.PendingAlgorithm ?? key.Algorithm, key.PendingFingerprint);
        (key.PendingAlgorithm, key.PendingFingerprint, key.PendingSeenAt) = (null, null, null);
        (key.AcceptedAt, key.AcceptedBy) = (DateTime.UtcNow, User.Identity?.Name);
        await db.SaveChangesAsync();
        return Ok(View(key));
    }

    /// <summary>Oublie l'hôte : sa prochaine clé sera retenue comme au premier contact.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Forget(int id)
    {
        var key = await db.SshHostKeys.FirstOrDefaultAsync(k => k.Id == id);
        if (key is null) return NotFound();
        db.SshHostKeys.Remove(key);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
