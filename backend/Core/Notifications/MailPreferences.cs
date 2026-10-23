using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using SAloha.Api.Core.Auth;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Core.Notifications;

/// <summary>
/// SOC-22 : choix d'un utilisateur pour ses notifications par courriel. Le rôle et les groupes
/// du compte sont relevés à l'enregistrement du choix puis à chaque connexion : l'envoi, fait
/// hors session, en déduit ce que le compte voit (SOC-23). Rien n'est retenu pour un compte
/// qui n'a pas fait de choix (donnée personnelle minimale, ADM-12).
/// </summary>
public class MailPreference
{
    public const string None = "Aucun", Immediate = "Immédiat", Daily = "Résumé quotidien";
    public static readonly string[] Modes = [None, Immediate, Daily];

    [Key, MaxLength(200)] public string Username { get; set; } = "";
    [MaxLength(20)] public string Mode { get; set; } = None;
    [MaxLength(255)] public string? Address { get; set; }
    [MaxLength(250)] public string DisplayName { get; set; } = "";
    [MaxLength(10)] public string Role { get; set; } = "User";
    /// <summary>Groupes AD du compte (CN, minuscules), en JSON.</summary>
    public string Groups { get; set; } = "[]";
    /// <summary>Notifications envoyées jusqu'à cette date ; point de départ du prochain envoi.</summary>
    public DateTime SentUntil { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<string> GroupList() => JsonSerializer.Deserialize<List<string>>(Groups) ?? [];

    /// <summary>Relève le rôle et les groupes du compte connecté.</summary>
    public void Snapshot(string displayName, string role, IEnumerable<string> groups)
    {
        (DisplayName, Role) = (displayName, role);
        Groups = JsonSerializer.Serialize(groups.Select(g => g.ToLowerInvariant()).Distinct().OrderBy(g => g).ToList());
    }

    /// <summary>À la connexion : un compte qui a fait un choix voit son rôle et ses groupes rafraîchis.</summary>
    public static async Task RefreshAsync(AppDbContext db, string username, string displayName, string role, IEnumerable<string> groups)
    {
        var p = await db.MailPreferences.FindAsync(username.ToLowerInvariant());
        if (p is null) return;
        p.Snapshot(displayName, role, groups);
        await db.SaveChangesAsync();
    }
}

public record MailPreferenceDto(string? Mode, string? Address);
public record MailPreferenceView(string Mode, string? Address, bool MailEnabled);

/// <summary>SOC-22 : mon choix de courriel (aucun, immédiat, résumé quotidien) et mon adresse.</summary>
[ApiController]
[Route("api/me/mail-preferences")]
[Authorize(Policy = "User")]
public class MailPreferencesController(AppDbContext db) : ControllerBase
{
    private async Task<bool> MailEnabledAsync() => Platform.MailSender.Ready(await db.MailSettings.AsNoTracking().FirstOrDefaultAsync());

    [HttpGet]
    public async Task<MailPreferenceView> Get()
    {
        var p = await db.MailPreferences.AsNoTracking().FirstOrDefaultAsync(x => x.Username == User.MeKey());
        return new(p?.Mode ?? MailPreference.None, p?.Address, await MailEnabledAsync());
    }

    [HttpPut]
    public async Task<IActionResult> Put(MailPreferenceDto dto)
    {
        var mode = dto.Mode ?? MailPreference.None;
        if (!MailPreference.Modes.Contains(mode)) return BadRequest(new { message = $"Choix inconnu : {mode} ({string.Join(", ", MailPreference.Modes)})." });
        var address = dto.Address?.Trim();
        if (string.IsNullOrEmpty(address)) address = null;
        if (address is not null && (!MailboxAddress.TryParse(address, out var parsed) || !parsed.Address.Contains('@') || address.Length > 255))
            return BadRequest(new { message = "Adresse de courriel invalide." });
        if (mode != MailPreference.None && address is null) return BadRequest(new { message = "Indiquer l'adresse où recevoir les courriels." });

        var me = User.MeKey();
        var p = await db.MailPreferences.FindAsync(me);
        if (p is null)
        {
            if (mode == MailPreference.None) return Ok(new MailPreferenceView(mode, null, await MailEnabledAsync()));
            db.MailPreferences.Add(p = new MailPreference { Username = me, SentUntil = DateTime.UtcNow });
        }
        // Passer à un envoi : on part de maintenant, sans rattraper l'historique.
        if (p.Mode == MailPreference.None && mode != MailPreference.None) p.SentUntil = DateTime.UtcNow;
        (p.Mode, p.Address, p.UpdatedAt) = (mode, address, DateTime.UtcNow);
        var role = User.IsInRole("Admin") ? "Admin" : User.IsInRole("Manager") ? "Manager" : "User";
        p.Snapshot(User.FindFirst("displayName")?.Value ?? me, role, User.GroupKeys());
        // « Aucun » sans adresse : rien à garder.
        if (mode == MailPreference.None && address is null) db.MailPreferences.Remove(p);
        await db.SaveChangesAsync();
        return Ok(new MailPreferenceView(mode, address, await MailEnabledAsync()));
    }
}
