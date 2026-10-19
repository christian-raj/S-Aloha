using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Perimeters;
using SAloha.Api.Core.Records;
using SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

namespace SAloha.Api.Modules.ServiceConfigurationManagement.Controllers;

/// <summary>
/// Coffre d'identifiants (CFG-45), tenu par l'Admin : le secret s'écrit (création, rotation), ne
/// se relit jamais ; l'API indique seulement s'il est présent et quand il a changé. Les
/// gestionnaires lisent la liste de leurs périmètres (noms, types) pour choisir l'identifiant
/// d'une tâche de scan (PER-06).
/// </summary>
[ApiController]
[Route("api/discovery/credentials")]
[Authorize(Policy = "User")]
public class DiscoveryCredentialsController(AppDbContext db, PerimeterAccess access, CredentialVault vault) : ControllerBase
{
    private static object View(DiscoveryCredential c, int usedBy) => new
    {
        c.Id, c.PerimeterId, c.Name, c.CredentialType, c.Username, c.Domain, c.AuthProtocol, c.PrivProtocol, c.CreatedAt, c.CreatedBy,
        HasSecret = c.EncryptedSecret != "", c.SecretChangedAt, UsedBy = usedBy
    };

    private Task<Dictionary<int, int>> UsageAsync() => db.Set<DiscoveryJob>().AsNoTracking().Where(j => j.CredentialId != null)
        .GroupBy(j => j.CredentialId!.Value).ToDictionaryAsync(g => g.Key, g => g.Count());

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var scope = await access.GetAsync();
        var managed = scope.ManagedIds.ToList();
        if (!scope.Global && managed.Count == 0) return Forbid();
        var usage = await UsageAsync();
        var list = await db.Set<DiscoveryCredential>().AsNoTracking()
            .Where(c => scope.Global || managed.Contains(c.PerimeterId)).OrderBy(c => c.Name).ToListAsync();
        return Ok(new { VaultConfigured = vault.Configured, Items = list.Select(c => View(c, usage.GetValueOrDefault(c.Id))) });
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Create(DiscoveryCredentialDto dto)
    {
        if (!vault.Configured) return StatusCode(503, new { message = CredentialVault.NotConfigured });
        if (string.IsNullOrEmpty(dto.Secret)) return BadRequest(new { message = Secret(dto.CredentialType) + " est obligatoire." });
        var (perimeterId, refused, forbidden) = await access.ChooseAsync(dto.PerimeterId);
        if (forbidden) return Forbid();
        if (refused is not null) return BadRequest(new { message = refused });
        var c = new DiscoveryCredential { PerimeterId = perimeterId!.Value, CreatedBy = User.Identity?.Name ?? "" };
        if (Apply(c, dto) is { } error || (error = SecretOf(c, dto, null, out var plain)) is not null) return BadRequest(new { message = error });
        c.EncryptedSecret = vault.Protect(plain!);
        c.SecretChangedAt = DateTime.UtcNow;
        db.Add(c);
        await db.SaveChangesAsync();
        return Ok(View(c, 0));
    }

    /// <summary>Modification ; un secret fourni remplace l'ancien (rotation), vide il est conservé.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Update(int id, DiscoveryCredentialDto dto)
    {
        var c = await db.Set<DiscoveryCredential>().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        if (dto.CredentialType != c.CredentialType && await db.Set<DiscoveryJob>().AnyAsync(j => j.CredentialId == id))
            return BadRequest(new { message = "Cet identifiant sert à des tâches de scan : son type ne peut plus changer." });
        var typeBefore = c.CredentialType;
        var hadPrivacy = typeBefore == "SnmpV3" && c.PrivProtocol != "None";
        if (Apply(c, dto) is { } error) return BadRequest(new { message = error });
        // SNMP v3 : une phrase fournie remplace la sienne ; l'autre est conservée.
        if (!string.IsNullOrEmpty(dto.Secret) || !string.IsNullOrEmpty(dto.PrivacySecret) || NeedsPrivacy(c, hadPrivacy))
        {
            if (!vault.Configured) return StatusCode(503, new { message = CredentialVault.NotConfigured });
            var current = typeBefore == "SnmpV3" && c.EncryptedSecret != "" ? SnmpV3Secret.Parse(vault.Unprotect(c.EncryptedSecret)) : null;
            if (SecretOf(c, dto, current, out var plain) is { } secretError) return BadRequest(new { message = secretError });
            c.EncryptedSecret = vault.Protect(plain!);
            c.SecretChangedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        return Ok(View(c, (await UsageAsync()).GetValueOrDefault(c.Id)));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Admin")]
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

    private static string Secret(string? type) => type switch
    {
        "SnmpV2c" => "La communauté SNMP", "SnmpV3" => "La phrase d'authentification", _ => "Le mot de passe"
    };

    /// <summary>Passage au chiffrement SNMP v3 sans phrase de chiffrement connue : il faut la fournir.</summary>
    private static bool NeedsPrivacy(DiscoveryCredential c, bool hadPrivacy) =>
        c.CredentialType == "SnmpV3" && c.PrivProtocol != "None" && !hadPrivacy;

    /// <summary>
    /// Secret à chiffrer : tel quel (communauté, mot de passe) ou, en SNMP v3, les deux phrases
    /// (8 caractères au moins, RFC 3414), celles absentes reprises de <paramref name="current"/>.
    /// </summary>
    private static string? SecretOf(DiscoveryCredential c, DiscoveryCredentialDto dto, SnmpV3Secret? current, out string? plain)
    {
        plain = null;
        if (c.CredentialType != "SnmpV3")
        {
            if (string.IsNullOrEmpty(dto.Secret)) return Secret(c.CredentialType) + " est obligatoire.";
            plain = dto.Secret;
            return null;
        }
        var auth = string.IsNullOrEmpty(dto.Secret) ? current?.Auth : dto.Secret;
        var priv = c.PrivProtocol == "None" ? null : string.IsNullOrEmpty(dto.PrivacySecret) ? current?.Priv : dto.PrivacySecret;
        if (auth is null || auth.Length < 8) return "La phrase d'authentification SNMP v3 compte 8 caractères au moins.";
        if (c.PrivProtocol != "None" && (priv is null || priv.Length < 8))
            return "La phrase de chiffrement SNMP v3 compte 8 caractères au moins (ou choisir « None » : authentification seule).";
        plain = new SnmpV3Secret(auth, priv).Serialize();
        return null;
    }

    private static string? Apply(DiscoveryCredential c, DiscoveryCredentialDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return "Nommer l'identifiant (ex. « Communauté SNMP du siège »).";
        if (Allowed.Check("Type d'identifiant", dto.CredentialType, DiscoveryCredential.Types) is { } error) return error;
        if (dto.CredentialType == "WindowsDomain" && string.IsNullOrWhiteSpace(dto.Username))
            return "Un compte de domaine porte son nom de compte.";
        var v3 = dto.CredentialType == "SnmpV3";
        if (v3 && string.IsNullOrWhiteSpace(dto.Username)) return "Un utilisateur SNMP v3 porte son nom (securityName).";
        if (v3 && Allowed.Check("Authentification SNMP v3", dto.AuthProtocol ?? "SHA256", SnmpAccess.AuthProtocols) is { } auth) return auth;
        if (v3 && Allowed.Check("Chiffrement SNMP v3", dto.PrivProtocol ?? "AES", SnmpAccess.PrivProtocols) is { } priv) return priv;
        c.Name = dto.Name.Trim();
        c.CredentialType = dto.CredentialType;
        c.Username = dto.CredentialType is "WindowsDomain" or "SnmpV3" ? dto.Username?.Trim() : null;
        c.AuthProtocol = v3 ? dto.AuthProtocol ?? "SHA256" : null;
        c.PrivProtocol = v3 ? dto.PrivProtocol ?? "AES" : null;
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
        Ok(await db.Set<EndOfLifeRule>().AsNoTracking().OrderBy(r => r.EndOfSupport).ThenBy(r => r.Product)
            .Select(r => EndOfLifeRuleView.From(r)).ToListAsync());

    [HttpPost]
    [Authorize(Policy = "Manager")]
    public async Task<IActionResult> Create(EndOfLifeRuleDto dto)
    {
        var rule = new EndOfLifeRule { CreatedBy = User.Identity?.Name ?? "" };
        if (await ApplyAsync(rule, dto) is { } error) return BadRequest(new { message = error });
        db.Add(rule);
        await db.SaveChangesAsync();
        return Ok(EndOfLifeRuleView.From(rule));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Manager")]
    public async Task<IActionResult> Update(int id, EndOfLifeRuleDto dto)
    {
        var rule = await db.Set<EndOfLifeRule>().FirstOrDefaultAsync(r => r.Id == id);
        if (rule is null) return NotFound();
        if (await ApplyAsync(rule, dto) is { } error) return BadRequest(new { message = error });
        await db.SaveChangesAsync();
        return Ok(EndOfLifeRuleView.From(rule));
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
