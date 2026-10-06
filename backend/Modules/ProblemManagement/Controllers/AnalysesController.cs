using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ProblemManagement;

[ApiController]
[Route("api/problems/{problemId:int}/analyses")]
[Authorize(Policy = "User")]
public class AnalysesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int problemId) =>
        Ok(await db.Analyses.AsNoTracking()
            .Where(a => a.ProblemId == problemId)
            .OrderBy(a => a.CreatedAt).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(int problemId, AnalysisDto dto)
    {
        if (!await db.Problems.AnyAsync(p => p.Id == problemId)) return NotFound();
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
        return Ok(a);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int problemId, int id, AnalysisDto dto)
    {
        var a = await db.Analyses.FirstOrDefaultAsync(x => x.Id == id && x.ProblemId == problemId);
        if (a is null) return NotFound();
        a.DataJson = dto.DataJson; a.Conclusion = dto.Conclusion; a.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(a);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Manager")]
    public async Task<IActionResult> Delete(int problemId, int id)
    {
        var a = await db.Analyses.FirstOrDefaultAsync(x => x.Id == id && x.ProblemId == problemId);
        if (a is null) return NotFound();
        db.Analyses.Remove(a);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
