using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Auth;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Links;
using SAloha.Api.Core.Perimeters;
using SAloha.Api.Core.Records;
using SAloha.Api.Modules.ServiceRequestManagement;

namespace SAloha.Api.Core.Comments;

/// <summary>
/// Fil de commentaires d'un enregistrement (SOC-24). Un commentaire **public** se lit par
/// quiconque voit l'enregistrement ; une **note de travail** par ceux qui le traitent :
/// quiconque le voit dans ses périmètres, sauf le côté demandeur (déclarant, bénéficiaire
/// d'une demande) quand il n'est ni responsable (en personne ou par son groupe) ni
/// gestionnaire du périmètre.
/// </summary>
[ApiController]
[Route("api/comments")]
[Authorize(Policy = "User")]
public class CommentsController(AppDbContext db, PerimeterAccess access) : ControllerBase
{
    private string Me => User.Identity?.Name ?? "";

    /// <summary>L'enregistrement s'il est visible, et le droit de lire et d'écrire des notes de travail.</summary>
    private async Task<(Record? Record, bool Notes)> ResolveAsync(string type, int id)
    {
        var kind = ItemLinks.ByType(type);
        var records = ItemLinks.Records(db, type);
        if (kind is null || records is null) return (null, false);
        var target = await kind.Query(db).FirstOrDefaultAsync(t => t.Id == id);
        var scope = await access.GetAsync();
        if (target is null || !scope.Sees(target)) return (null, false);
        var r = await records.AsNoTracking().FirstAsync(x => x.Id == id);
        var me = User.MeKey();
        var handler = scope.ManagerOn(r.PerimeterId) || User.Designates(r.OwnerType, r.OwnerId);
        var requester = r.CreatedBy.ToLowerInvariant() == me
                        || (r is ServiceRequest sr && (sr.RequestedFor ?? "").ToLowerInvariant() == me);
        return (r, scope.Sees(r.PerimeterId) && (handler || !requester));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string type, [FromQuery] int id)
    {
        var (record, notes) = await ResolveAsync(type, id);
        if (record is null) return NotFound();
        var comments = await db.Comments.AsNoTracking()
            .Where(c => c.TargetType == type && c.TargetId == id && (notes || c.Visibility == Comment.Public))
            .OrderBy(c => c.CreatedAt).ToListAsync();
        return Ok(new { Comments = comments, CanWriteNotes = notes });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CommentDto dto)
    {
        var (record, notes) = await ResolveAsync(dto.Type, dto.Id);
        if (record is null) return NotFound();
        var visibility = string.IsNullOrEmpty(dto.Visibility) ? Comment.Public : dto.Visibility;
        var error = Allowed.Check("Visibilité", visibility, Comment.Visibilities);
        if (error is not null) return BadRequest(new { message = error });
        if (visibility == Comment.WorkNote && !notes) return Forbid();
        var body = dto.Body?.Trim() ?? "";
        if (body.Length == 0) return BadRequest(new { message = "Le commentaire est vide." });
        if (body.Length > Comment.MaxBody) return BadRequest(new { message = $"Un commentaire compte au plus {Comment.MaxBody} caractères." });
        var c = new Comment
        {
            TargetType = dto.Type, TargetId = dto.Id, Visibility = visibility, Body = body,
            Author = Me, AuthorDisplayName = User.FindFirst("displayName")?.Value ?? Me
        };
        db.Comments.Add(c);
        await db.SaveChangesAsync();
        return Ok(c);
    }

    /// <summary>Retirer un commentaire : son auteur ou un Admin.</summary>
    [HttpDelete("{commentId:int}")]
    public async Task<IActionResult> Delete(int commentId)
    {
        var c = await db.Comments.FindAsync(commentId);
        if (c is null) return NotFound();
        var (record, notes) = await ResolveAsync(c.TargetType, c.TargetId);
        if (record is null || (c.Visibility == Comment.WorkNote && !notes)) return NotFound();
        if (!User.IsInRole("Admin") && !string.Equals(c.Author, Me, StringComparison.OrdinalIgnoreCase)) return Forbid();
        db.Comments.Remove(c);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
