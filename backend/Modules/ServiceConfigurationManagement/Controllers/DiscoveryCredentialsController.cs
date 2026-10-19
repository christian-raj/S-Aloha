using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Perimeters;
using SAloha.Api.Core.Records;
using SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

namespace SAloha.Api.Modules.ServiceConfigurationManagement.Controllers;

/// <summary>
/// Coffre d'identifiants (CFG-45), Admin seul : le secret s'écrit (création, rotation), ne se
/// relit jamais ; l'API indique seulement s'il est présent et quand il a changé.
/// </summary>
[ApiController]
[Route("api/discovery/credentials")]
[Authorize(Policy = "Admin")]
public class DiscoveryCredentialsController(AppDbContext db, PerimeterAccess access, CredentialVault vault) : ControllerBase
{
    private static object View(DiscoveryCredential c, int usedBy) => new
    {
        c.Id, c.PerimeterId, c.Name, c.CredentialType, c.Username, c.Domain, c.CreatedAt, c.CreatedBy,
        HasSecret = c.EncryptedSecret != "", c.SecretChangedAt, UsedBy = usedBy
    };

    private Task<Dictionary<int, int>> UsageAsync() => db.Set<DiscoveryJob>().AsNoTracking().Where(j => j.CredentialId != null)
        .GroupBy(j => j.CredentialId!.Value).ToDictionaryAsync(g => g.Key, g => g.Count());

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var usage = await UsageAsync();
        var list = await db.Set<DiscoveryCredential>().AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        return Ok(new { VaultConfigured = vault.Configured, Items = list.Select(c => View(c, usage.GetValueOrDefault(c.Id))) });
    }

    [HttpPost]
    public async Task<IActionResult> Create(DiscoveryCredentialDto dto)
    {
        if (!vault.Configured) return StatusCode(503, new { message = CredentialVault.NotConfigured });
        if (string.IsNullOrEmpty(dto.Secret)) return BadRequest(new { message = Secret(dto.CredentialType) + " est obligatoire." });
        var (perimeterId, refused, forbidden) = await access.ChooseAsync(dto.PerimeterId);
        if (forbidden) return Forbid();
        if (refused is not null) return BadRequest(new { message = refused });
        var c = new DiscoveryCredential { PerimeterId = perimeterId!.Value, CreatedBy = User.Identity?.Name ?? "" };
        if (Apply(c, dto) is { } error) return BadRequest(new { message = error });
        c.EncryptedSecret = vault.Protect(dto.Secret);
        c.SecretChangedAt = DateTime.UtcNow;
        db.Add(c);
        await db.SaveChangesAsync();
        return Ok(View(c, 0));
    }

    /// <summary>Modification ; un secret fourni remplace l'ancien (rotation), vide il est conservé.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, DiscoveryCredentialDto dto)
    {
        var c = await db.Set<DiscoveryCredential>().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        if (dto.CredentialType != c.CredentialType && await db.Set<DiscoveryJob>().AnyAsync(j => j.CredentialId == id))
            return BadRequest(new { message = "Cet identifiant sert à des tâches de scan : son type ne peut plus changer." });
        if (Apply(c, dto) is { } error) return BadRequest(new { message = error });
        if (!string.IsNullOrEmpty(dto.Secret))
        {
            if (!vault.Configured) return StatusCode(503, new { message = CredentialVault.NotConfigured });
            c.EncryptedSecret = vault.Protect(dto.Secret);
            c.SecretChangedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        return Ok(View(c, (await UsageAsync()).GetValueOrDefault(c.Id)));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await db.Set<DiscoveryCredential>().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        var jobs = await db.Set<DiscoveryJob>().Where(j => j.CredentialId == id).Select(j => j.Name).ToListAsync();
        if (jobs.Count > 0)
            return Conflict(new { message = $"Identifiant utilisé par {string.Join(", ", jobs.Select(n => $"« {n} »"))} : changer d'abord ces tâches." });
        db.Remove(c);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string Secret(string? type) => type == "SnmpV2c" ? "La communauté SNMP" : "Le mot de passe";

    private static string? Apply(DiscoveryCredential c, DiscoveryCredentialDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return "Nommer l'identifiant (ex. « Communauté SNMP du siège »).";
        if (Allowed.Check("Type d'identifiant", dto.CredentialType, DiscoveryCredential.Types) is { } error) return error;
        if (dto.CredentialType == "WindowsDomain" && string.IsNullOrWhiteSpace(dto.Username))
            return "Un compte de domaine porte son nom de compte.";
        c.Name = dto.Name.Trim();
        c.CredentialType = dto.CredentialType;
        c.Username = dto.CredentialType == "WindowsDomain" ? dto.Username?.Trim() : null;
        c.Domain = dto.CredentialType == "WindowsDomain" && !string.IsNullOrWhiteSpace(dto.Domain) ? dto.Domain.Trim() : null;
        return Lengths.Check(c);
    }
}

/// <summary>
/// Fins de support (CFG-47) : référentiel partagé des produits et de leur date de fin de
/// support, lu par tous (inventaire, tableau de bord), tenu par les gestionnaires.
/// </summary>
[ApiController]
[Route("api/discovery/end-of-life")]
[Authorize(Policy = "User")]
public class EndOfLifeRulesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await db.Set<EndOfLifeRule>().AsNoTracking().OrderBy(r => r.EndOfSupport).ThenBy(r => r.Product).ToListAsync());

    [HttpPost]
    [Authorize(Policy = "Manager")]
    public async Task<IActionResult> Create(EndOfLifeRuleDto dto)
    {
        var rule = new EndOfLifeRule { CreatedBy = User.Identity?.Name ?? "" };
        if (await ApplyAsync(rule, dto) is { } error) return BadRequest(new { message = error });
        db.Add(rule);
        await db.SaveChangesAsync();
        return Ok(rule);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Manager")]
    public async Task<IActionResult> Update(int id, EndOfLifeRuleDto dto)
    {
        var rule = await db.Set<EndOfLifeRule>().FirstOrDefaultAsync(r => r.Id == id);
        if (rule is null) return NotFound();
        if (await ApplyAsync(rule, dto) is { } error) return BadRequest(new { message = error });
        await db.SaveChangesAsync();
        return Ok(rule);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var rule = await db.Set<EndOfLifeRule>().FirstOrDefaultAsync(r => r.Id == id);
        if (rule is null) return NotFound();
        db.Remove(rule);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string?> ApplyAsync(EndOfLifeRule rule, EndOfLifeRuleDto dto)
    {
        var product = dto.Product?.Trim() ?? "";
        if (product.Length < 3) return "Nommer le produit (3 caractères au moins), tel qu'il apparaît dans le système des CI (ex. « Windows Server 2012 »).";
        if (dto.EndOfSupport is null) return "Indiquer la date de fin de support.";
        var key = product.ToLower();
        if (await db.Set<EndOfLifeRule>().AnyAsync(r => r.Id != rule.Id && r.Product.ToLower() == key))
            return $"« {product} » a déjà une date de fin de support.";
        rule.Product = product;
        rule.EndOfSupport = Dates.Utc(dto.EndOfSupport)!.Value.Date;
        rule.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        return Lengths.Check(rule);
    }
}
