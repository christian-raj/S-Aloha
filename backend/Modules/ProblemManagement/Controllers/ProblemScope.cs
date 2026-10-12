using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Perimeters;

namespace SAloha.Api.Modules.ProblemManagement;

/// <summary>Analyses et actions suivent le périmètre de leur problème (PER-05, PER-06).</summary>
public static class ProblemScope
{
    /// <summary>Périmètre du problème s'il est visible de l'utilisateur, sinon null (404).</summary>
    public static async Task<int?> VisiblePerimeterAsync(AppDbContext db, PerimeterAccess access, int problemId)
    {
        var perimeter = await db.Problems.Where(p => p.Id == problemId).Select(p => (int?)p.PerimeterId).FirstOrDefaultAsync();
        return perimeter is not null && (await access.GetAsync()).Sees(perimeter.Value) ? perimeter : null;
    }
}
