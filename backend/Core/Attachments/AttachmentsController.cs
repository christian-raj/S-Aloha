using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Links;
using SAloha.Api.Core.Perimeters;
using SAloha.Api.Core.Platform;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Core.Attachments;

/// <summary>
/// SOC-25 : pièces jointes d'un enregistrement, dans le stockage objet réglé par l'Admin
/// (ADM-61). Quiconque voit l'enregistrement les liste, les télécharge et en dépose ; son
/// auteur, un gestionnaire du périmètre ou un Admin en retire. Chaque dépôt et retrait est
/// tracé au journal d'audit de l'enregistrement.
/// </summary>
[ApiController]
[Route("api/attachments")]
[Authorize(Policy = "User")]
public class AttachmentsController(AppDbContext db, PerimeterAccess access, ObjectStorage storage, ILogger<AttachmentsController> logger) : ControllerBase
{
    /// <summary>Plafond de la requête : 100 Mo, la plus grande taille réglable (ADM-61), plus l'enveloppe multipart.</summary>
    private const long MaxRequest = 101L * 1024 * 1024;

    private string Me => User.Identity?.Name ?? "";
    private string MyDisplay => User.FindFirst("displayName")?.Value ?? Me;

    private async Task<(Record? Record, PerimeterScope Scope)> ResolveAsync(string type, int id)
    {
        var scope = await access.GetAsync();
        var kind = ItemLinks.ByType(type);
        var records = ItemLinks.Records(db, type);
        if (kind is null || records is null) return (null, scope);
        var target = await kind.Query(db).FirstOrDefaultAsync(t => t.Id == id);
        if (target is null || !scope.Sees(target)) return (null, scope);
        return (await records.AsNoTracking().FirstAsync(x => x.Id == id), scope);
    }

    private void Trace(Record r, string type, string action, string text) => db.AuditEntries.Add(new Audit.AuditEntry
    {
        EntityType = type, EntityId = r.Id, Reference = r.Reference, Action = action, Field = "pièce jointe", NewValue = text,
        Author = Me, AuthorDisplayName = MyDisplay, At = DateTime.UtcNow
    });

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string type, [FromQuery] int id)
    {
        var (record, _) = await ResolveAsync(type, id);
        if (record is null) return NotFound();
        var settings = await storage.SettingsAsync();
        var items = await db.Attachments.AsNoTracking().Where(a => a.TargetType == type && a.TargetId == id)
            .OrderBy(a => a.CreatedAt).Select(a => AttachmentView.From(a)).ToListAsync();
        return Ok(new { Enabled = ObjectStorage.Ready(settings), MaxFileMb = settings?.MaxFileMb ?? 10, Extensions = AttachmentTypes.Extensions, Items = items });
    }

    [HttpPost]
    [RequestSizeLimit(MaxRequest)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequest)]
    public async Task<IActionResult> Upload([FromForm] string type, [FromForm] int id, IFormFile? file)
    {
        var (record, scope) = await ResolveAsync(type, id);
        if (record is null) return NotFound();
        // Déposer suppose d'être dans le périmètre de l'enregistrement (PER-05, PER-06).
        if (!scope.Sees(record.PerimeterId)) return Forbid();
        var settings = await storage.SettingsAsync();
        if (!ObjectStorage.Ready(settings)) return BadRequest(new { message = "Pièces jointes non activées : l'administrateur règle le stockage (Administration › Messagerie et stockage)." });
        if (file is null || file.Length == 0) return BadRequest(new { message = "Choisir un fichier non vide." });
        if (file.Length > settings!.MaxFileMb * 1024L * 1024) return BadRequest(new { message = $"Fichier trop volumineux : {settings.MaxFileMb} Mo au plus." });

        var name = AttachmentTypes.SafeName(file.FileName);
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        var bytes = buffer.ToArray();
        var (contentType, error) = AttachmentTypes.Check(name, bytes[..Math.Min(bytes.Length, 512)]);
        if (error is not null) return BadRequest(new { message = error });

        var a = new Attachment
        {
            TargetType = type, TargetId = id, FileName = name, ContentType = contentType!, Size = bytes.Length,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)), StorageKey = $"pieces-jointes/{type}/{id}/{Guid.NewGuid():N}",
            CreatedBy = Me, CreatedByDisplayName = MyDisplay
        };
        try { await storage.PutAsync(settings, a.StorageKey, new MemoryStream(bytes), contentType!); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError("Pièce jointe non déposée dans le stockage : {Error}", ex.Message.Split('\n')[0]);
            return StatusCode(503, new { message = "Stockage des pièces jointes injoignable : réessayer plus tard." });
        }
        db.Attachments.Add(a);
        Trace(record, type, "Pièce jointe ajoutée", $"{name} ({bytes.Length} octets)");
        await db.SaveChangesAsync();
        return Ok(AttachmentView.From(a));
    }

    /// <summary>Téléchargement : toujours en pièce jointe, type tiré de l'extension, sans interprétation par le navigateur.</summary>
    [HttpGet("{attachmentId:int}/content")]
    public async Task<IActionResult> Content(int attachmentId)
    {
        var a = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == attachmentId);
        if (a is null || (await ResolveAsync(a.TargetType, a.TargetId)).Record is null) return NotFound();
        var settings = await storage.SettingsAsync();
        if (!ObjectStorage.Ready(settings)) return StatusCode(503, new { message = "Stockage des pièces jointes non activé." });
        Stream content;
        try { content = await storage.GetAsync(settings!, a.StorageKey); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError("Pièce jointe illisible dans le stockage : {Error}", ex.Message.Split('\n')[0]);
            return StatusCode(503, new { message = "Stockage des pièces jointes injoignable : réessayer plus tard." });
        }
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        return File(content, a.ContentType.Split(';')[0], a.FileName);
    }

    /// <summary>Retirer : l'auteur du dépôt, un gestionnaire du périmètre ou un Admin.</summary>
    [HttpDelete("{attachmentId:int}")]
    public async Task<IActionResult> Delete(int attachmentId)
    {
        var a = await db.Attachments.FirstOrDefaultAsync(x => x.Id == attachmentId);
        if (a is null) return NotFound();
        var (record, scope) = await ResolveAsync(a.TargetType, a.TargetId);
        if (record is null) return NotFound();
        if (!scope.ManagerOn(record.PerimeterId) && !string.Equals(a.CreatedBy, Me, StringComparison.OrdinalIgnoreCase)) return Forbid();
        db.Attachments.Remove(a);
        Trace(record, a.TargetType, "Pièce jointe retirée", a.FileName);
        await db.SaveChangesAsync();
        await RemoveObjectsAsync(storage, [a.StorageKey], logger);
        return NoContent();
    }

    /// <summary>Supprime des objets du stockage ; un échec est journalisé (l'objet orphelin ne se lit plus).</summary>
    public static async Task RemoveObjectsAsync(ObjectStorage storage, IEnumerable<string> keys, ILogger logger)
    {
        var settings = await storage.SettingsAsync();
        if (!ObjectStorage.Ready(settings)) return;
        foreach (var key in keys)
            try { await storage.DeleteAsync(settings!, key); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("Objet de pièce jointe non supprimé du stockage : {Error}", ex.Message.Split('\n')[0]);
            }
    }
}
