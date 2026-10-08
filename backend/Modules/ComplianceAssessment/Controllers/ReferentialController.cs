using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Modules.ComplianceAssessment;

/// <summary>
/// Référentiel NIS 2 (ReCyF) : consultation par tous, import du texte des
/// exigences par un administrateur (NIS-20, NIS-21 ; ADR-0012).
/// </summary>
[ApiController]
[Route("api/compliance/referential")]
[Authorize(Policy = "User")]
public class ReferentialController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var objectives = await db.SecurityObjectives.AsNoTracking().Include(o => o.Requirements)
            .OrderBy(o => o.Id).ToListAsync();
        var all = objectives.SelectMany(o => o.Requirements).ToList();
        return Ok(new
        {
            Version = ReferentialStore.Version,
            Requirements = all.Count,
            WithText = all.Count(r => !string.IsNullOrEmpty(r.Text)),
            LastImport = all.Max(r => r.TextImportedAt),
            Objectives = objectives.Select(o => new
            {
                o.Id, o.Title, o.Pillar,
                Requirements = o.Requirements.OrderBy(r => r.Order).Select(r => new
                {
                    r.Id, r.Code, r.Theme, r.ForImportant, r.ForEssential, r.IsoControls, r.Text
                })
            })
        });
    }

    [HttpPost("import")]
    [Authorize(Policy = "Admin")]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> Import(ReferentialImportDto dto)
    {
        try { return Ok(await ReferentialStore.ImportTextAsync(db, dto.Csv ?? "")); }
        catch (FormatException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
