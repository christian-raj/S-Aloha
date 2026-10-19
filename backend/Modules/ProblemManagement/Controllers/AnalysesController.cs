using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Perimeters;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ProblemManagement;

[ApiController]
[Route("api/problems/{problemId:int}/analyses")]
[Authorize(Policy = "User")]
public class AnalysesController(AppDbContext db, PerimeterAccess access) : ControllerBase
{
    private Task<int?> VisiblePerimeterAsync(int problemId) => ProblemScope.VisiblePerimeterAsync(db, access, problemId);

    [HttpGet]
    public async Task<IActionResult> List(int problemId) =>
        await VisiblePerimeterAsync(problemId) is null ? NotFound() : Ok(await db.Analyses.AsNoTracking()
            .Where(a => a.ProblemId == problemId)
            .OrderBy(a => a.CreatedAt).Select(a => RcaAnalysisView.From(a)).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(int problemId, AnalysisDto dto)
    {
        if (await VisiblePerimeterAsync(problemId) is null) return NotFound();
        var invalid = Allowed.Check("Méthode d'analyse", dto.Method, ProblemRules.Methods);
        if (invalid is not null) return BadRequest(new { message = invalid });
        var a = new RcaAnalysis
        {
            ProblemId = problemId, Method = dto.Method,
            DataJson = dto.DataJson, Conclusion = dto.Conclusion,
            CreatedBy = User.Identity?.Name ?? ""
        };
        var error = Lengths.Check(a);
        if (error is not null) return BadRequest(new { message = error });
        db.Analyses.Add(a);
        // un problème avec analyse en cours passe "En analyse" s'il était Nouveau
        var p = await db.Problems.FindAsync(problemId);
        if (p!.Status == "Nouveau") { p.Status = "En analyse"; p.UpdatedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync();
        return Ok(RcaAnalysisView.From(a));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int problemId, int id, AnalysisDto dto)
    {
        if (await VisiblePerimeterAsync(problemId) is null) return NotFound();
        var a = await db.Analyses.FirstOrDefaultAsync(x => x.Id == id && x.ProblemId == problemId);
        if (a is null) return NotFound();
        a.DataJson = dto.DataJson; a.Conclusion = dto.Conclusion; a.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(RcaAnalysisView.From(a));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int problemId, int id)
    {
        // Supprimer une analyse : gestionnaire du périmètre du problème (PER-06).
        var perimeter = await VisiblePerimeterAsync(problemId);
        if (perimeter is null) return NotFound();
        if (!(await access.GetAsync()).ManagerOn(perimeter.Value)) return Forbid();
        var a = await db.Analyses.FirstOrDefaultAsync(x => x.Id == id && x.ProblemId == problemId);
        if (a is null) return NotFound();
        db.Analyses.Remove(a);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
