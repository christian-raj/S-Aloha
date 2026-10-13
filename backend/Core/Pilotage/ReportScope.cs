using SAloha.Api.Core.Perimeters;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Core.Pilotage;

/// <summary>
/// PER-09 : les indicateurs portent sur les périmètres de l'utilisateur ; l'Admin voit
/// tout, et chacun peut restreindre à l'un de ses périmètres (<c>perimeter</c>).
/// </summary>
public sealed class ReportScope
{
    public bool All { get; private init; }
    public List<int> Ids { get; private init; } = [];

    public static async Task<ReportScope> ForAsync(PerimeterAccess access, int? perimeter)
    {
        var scope = await access.GetAsync();
        return new ReportScope
        {
            All = scope.Global && perimeter is null,
            Ids = (perimeter is { } only ? (scope.Sees(only) ? [only] : []) : scope.Ids).ToList()
        };
    }

    public IQueryable<T> In<T>(IQueryable<T> q) where T : Record => All ? q : q.Where(x => Ids.Contains(x.PerimeterId));
}
