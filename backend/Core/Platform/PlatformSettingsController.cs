using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using SAloha.Api.Core.Audit;
using SAloha.Api.Core.Data;
using SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

namespace SAloha.Api.Core.Platform;

/// <summary>
/// Administration → Messagerie et stockage (ADM-60 à ADM-63, ADR-0020) : SMTP des courriels
/// et stockage objet des pièces jointes, chacun avec un essai. Les secrets sont chiffrés par
/// le coffre et ne sont jamais renvoyés ; chaque modification est tracée au journal d'audit.
/// </summary>
[ApiController]
[Route("api/admin/settings")]
[Authorize(Policy = "Admin")]
public partial class PlatformSettingsController(AppDbContext db, CredentialVault vault, MailSender mail, ObjectStorage storage) : ControllerBase
{
    public const string AuditType = "parametre";

    private string Me => User.Identity?.Name ?? "";
    private string MyDisplay => User.FindFirst("displayName")?.Value ?? Me;

    private static string FirstLine(Exception ex) => (ex.InnerException is { } inner && ex.Message.Length < 40 ? $"{ex.Message} {inner.Message}" : ex.Message).Split('\n')[0];

    /// <summary>Champs modifiés, au journal d'audit ; un secret n'y figure jamais, seulement « modifié » ou « effacé ».</summary>
    private void Trace(string reference, int id, IEnumerable<(string Field, string? Old, string? New)> changes)
    {
        foreach (var (field, old, @new) in changes.Where(c => c.Old != c.New))
            db.AuditEntries.Add(new AuditEntry
            {
                EntityType = AuditType, EntityId = id, Reference = reference, Action = AuditEntry.Modification,
                Field = field, OldValue = old, NewValue = @new, Author = Me, AuthorDisplayName = MyDisplay, At = DateTime.UtcNow
            });
    }

    /// <summary>Nouveau secret : null (inchangé), "" (effacé) ou chiffré ; lève si le coffre n'a pas de clé.</summary>
    private string? Secret(string? current, string? input) =>
        input is null ? current : input == "" ? null : vault.Protect(input);

    private static string? Change(string? before, string? after) => before == after ? "inchangé" : after is null ? "effacé" : "modifié";

    // --- SMTP (ADM-60, ADM-62) ---

    [HttpGet("mail")]
    public async Task<MailSettingsView> GetMail() => MailSettingsView.From(await db.MailSettings.AsNoTracking().FirstOrDefaultAsync() ?? new MailSettings());

    [HttpPut("mail")]
    public async Task<IActionResult> PutMail(MailSettingsDto dto)
    {
        var host = dto.Host?.Trim() ?? "";
        var from = dto.FromAddress?.Trim() ?? "";
        var security = dto.Security ?? MailSettings.StartTls;
        if (!MailSettings.SecurityModes.Contains(security)) return BadRequest($"Chiffrement inconnu : {security} (Aucun, STARTTLS ou TLS).");
        if (dto.Port is < 1 or > 65535) return BadRequest("Port SMTP entre 1 et 65535.");
        if (from != "" && (!MailboxAddress.TryParse(from, out var parsed) || !parsed.Address.Contains('@')))
            return BadRequest("Adresse d'expéditeur invalide.");
        if (dto.Enabled && (host == "" || from == "")) return BadRequest("Serveur et adresse d'expéditeur requis pour activer l'envoi.");
        if (!string.IsNullOrEmpty(dto.Password) && !vault.Configured) return BadRequest(CredentialVault.NotConfigured);
        var publicUrl = dto.PublicUrl?.Trim().TrimEnd('/') ?? "";
        if (publicUrl != "" && (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var site) || site.Scheme is not ("http" or "https") || site.Query != "" || site.UserInfo != ""))
            return BadRequest("Adresse de l'interface invalide : une URL http(s), par exemple https://itsm.example.org.");

        var s = await db.MailSettings.FirstOrDefaultAsync();
        if (s is null) db.MailSettings.Add(s = new MailSettings());
        var password = Secret(s.EncryptedPassword, dto.Password);
        var username = string.IsNullOrWhiteSpace(dto.Username) ? null : dto.Username.Trim();
        var fromName = string.IsNullOrWhiteSpace(dto.FromName) ? "S-Aloha" : dto.FromName.Trim();
        Trace("Messagerie", 1,
        [
            ("enabled", s.Enabled ? "oui" : "non", dto.Enabled ? "oui" : "non"), ("host", s.Host, host), ("port", s.Port.ToString(), dto.Port.ToString()),
            ("security", s.Security, security), ("username", s.Username, username), ("fromAddress", s.FromAddress, from), ("fromName", s.FromName, fromName), ("publicUrl", s.PublicUrl, publicUrl),
            ("password", null, s.EncryptedPassword == password ? null : Change(s.EncryptedPassword, password)),
        ]);
        (s.Enabled, s.Host, s.Port, s.Security, s.Username, s.EncryptedPassword, s.FromAddress, s.FromName) =
            (dto.Enabled, host, dto.Port, security, username, password, from, fromName);
        s.PublicUrl = publicUrl;
        (s.UpdatedAt, s.UpdatedBy) = (DateTime.UtcNow, MyDisplay);
        await db.SaveChangesAsync();
        return Ok(MailSettingsView.From(s));
    }

    /// <summary>ADM-62 : courriel d'essai avec les réglages enregistrés, même désactivés.</summary>
    [HttpPost("mail/test")]
    public async Task<IActionResult> TestMail(MailTestDto dto)
    {
        if (!MailboxAddress.TryParse(dto.To?.Trim() ?? "", out var to) || !to.Address.Contains('@')) return BadRequest("Adresse du destinataire invalide.");
        var s = await mail.SettingsAsync();
        if (s is null || s.Host == "" || s.FromAddress == "") return BadRequest("Enregistrer d'abord le serveur et l'adresse d'expéditeur.");
        try
        {
            await mail.SendAsync(s, [to.Address], "S-Aloha : courriel d'essai",
                $"Courriel d'essai envoyé depuis la console d'administration par {MyDisplay}.\n\nServeur : {s.Host}:{s.Port} ({s.Security}).");
            return Ok(new ConnectionTest(true, $"Courriel d'essai remis au serveur {s.Host} pour {to.Address}."));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !HttpContext.RequestAborted.IsCancellationRequested)
        {
            return Ok(new ConnectionTest(false, $"Échec de l'envoi : {FirstLine(ex)}"));
        }
    }

    // --- Stockage objet (ADM-61, ADM-63) ---

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$")]
    private static partial Regex BucketName();

    [HttpGet("storage")]
    public async Task<StorageSettingsView> GetStorage() => StorageSettingsView.From(await db.StorageSettings.AsNoTracking().FirstOrDefaultAsync() ?? new StorageSettings());

    [HttpPut("storage")]
    public async Task<IActionResult> PutStorage(StorageSettingsDto dto)
    {
        var endpoint = dto.Endpoint?.Trim().TrimEnd('/') ?? "";
        var bucket = dto.Bucket?.Trim() ?? "";
        var accessKey = dto.AccessKey?.Trim() ?? "";
        var region = string.IsNullOrWhiteSpace(dto.Region) ? "garage" : dto.Region.Trim();
        if (endpoint != "" && (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.Query != "" || uri.UserInfo != ""))
            return BadRequest("Adresse du stockage invalide : une URL http(s), par exemple http://garage:3900.");
        if (bucket != "" && !BucketName().IsMatch(bucket)) return BadRequest("Nom de bucket invalide (3 à 63 caractères : minuscules, chiffres, points, tirets).");
        if (dto.MaxFileMb is < 1 or > 100) return BadRequest("Taille maximale d'une pièce jointe entre 1 et 100 Mo.");
        if (!string.IsNullOrEmpty(dto.SecretKey) && !vault.Configured) return BadRequest(CredentialVault.NotConfigured);

        var s = await db.StorageSettings.FirstOrDefaultAsync();
        if (s is null) db.StorageSettings.Add(s = new StorageSettings());
        var secret = Secret(s.EncryptedSecretKey, dto.SecretKey);
        if (dto.Enabled && (endpoint == "" || bucket == "" || accessKey == "" || secret is null))
            return BadRequest("Adresse, bucket, clé d'accès et clé secrète requis pour activer le stockage.");
        Trace("Stockage", 1,
        [
            ("enabled", s.Enabled ? "oui" : "non", dto.Enabled ? "oui" : "non"), ("endpoint", s.Endpoint, endpoint), ("region", s.Region, region),
            ("bucket", s.Bucket, bucket), ("accessKey", s.AccessKey, accessKey), ("pathStyle", s.PathStyle ? "oui" : "non", dto.PathStyle ? "oui" : "non"),
            ("maxFileMb", s.MaxFileMb.ToString(), dto.MaxFileMb.ToString()),
            ("secretKey", null, s.EncryptedSecretKey == secret ? null : Change(s.EncryptedSecretKey, secret)),
        ]);
        (s.Enabled, s.Endpoint, s.Region, s.Bucket, s.AccessKey, s.EncryptedSecretKey, s.PathStyle, s.MaxFileMb) =
            (dto.Enabled, endpoint, region, bucket, accessKey, secret, dto.PathStyle, dto.MaxFileMb);
        (s.UpdatedAt, s.UpdatedBy) = (DateTime.UtcNow, MyDisplay);
        await db.SaveChangesAsync();
        return Ok(StorageSettingsView.From(s));
    }

    /// <summary>ADM-63 : dépôt, relecture et suppression d'un objet d'essai avec les réglages enregistrés.</summary>
    [HttpPost("storage/test")]
    public async Task<IActionResult> TestStorage()
    {
        var s = await storage.SettingsAsync();
        if (s is null || s.Endpoint == "" || s.Bucket == "" || s.AccessKey == "" || s.EncryptedSecretKey is null)
            return BadRequest("Enregistrer d'abord l'adresse, le bucket et les clés.");
        var key = $"essai/{Guid.NewGuid():N}.txt";
        var text = $"Essai S-Aloha du {DateTime.UtcNow:O}";
        try
        {
            await storage.PutAsync(s, key, new MemoryStream(Encoding.UTF8.GetBytes(text)), "text/plain");
            string read;
            await using (var stream = await storage.GetAsync(s, key))
            using (var reader = new StreamReader(stream)) read = await reader.ReadToEndAsync();
            await storage.DeleteAsync(s, key);
            return Ok(read == text
                ? new ConnectionTest(true, $"Objet d'essai déposé, relu et supprimé dans le bucket {s.Bucket}.")
                : new ConnectionTest(false, "L'objet relu diffère de l'objet déposé."));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !HttpContext.RequestAborted.IsCancellationRequested)
        {
            return Ok(new ConnectionTest(false, $"Échec : {FirstLine(ex)}"));
        }
    }
}
