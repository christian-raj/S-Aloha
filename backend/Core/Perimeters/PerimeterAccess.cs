using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Auth;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Core.Perimeters;

/// <summary>
/// Portée de l'utilisateur connecté : son rôle effectif sur chaque périmètre
/// (PER-03). Admin : portée globale. Sinon, le plus élevé des rôles que lui
/// donnent ses affectations directes et celles de ses groupes AD (SOC-10), sur
/// les périmètres non supprimés. Un compte qu'aucune affectation ne désigne (ni
/// directement ni par ses groupes) est réputé affecté à « Organisation » avec son
/// rôle AD (PER-10, ADR-0014 D3) : la première affectation créée par l'Admin ne
/// coupe pas l'accès des autres.
/// </summary>
public sealed class PerimeterScope(bool global, IReadOnlyDictionary<int, string> roles)
{
    public bool Global => global;
    /// <summary>Périmètres où l'utilisateur a un rôle (vide pour l'Admin : il les a tous).</summary>
    public IReadOnlyCollection<int> Ids => roles.Keys.ToList();
    public IReadOnlyDictionary<int, string> Roles => roles;
    public bool Sees(int perimeterId) => global || roles.ContainsKey(perimeterId);
    public bool ManagerOn(int perimeterId) => global || roles.GetValueOrDefault(perimeterId) == "Manager";
}

public class PerimeterAccess(AppDbContext db, IHttpContextAccessor http)
{
    private PerimeterScope? _scope;

    public async Task<PerimeterScope> GetAsync()
    {
        if (_scope is not null) return _scope;
        var user = http.HttpContext!.User;
        if (user.IsInRole("Admin")) return _scope = new PerimeterScope(true, new Dictionary<int, string>());

        var adRole = user.IsInRole("Manager") ? "Manager" : "User";
        var me = user.MeKey();
        var groups = user.GroupKeys();
        var mine = await db.PerimeterAssignments.AsNoTracking()
            .Where(a => a.Perimeter!.DeletedAt == null
                        && (a.PrincipalType == "Group" ? groups.Contains(a.PrincipalId.ToLower()) : a.PrincipalId.ToLower() == me))
            .Select(a => new { a.PerimeterId, a.Role }).ToListAsync();
        // ADR-0014 D4 : le rôle par périmètre prime sur le rôle AD (un User AD peut être Manager d'un périmètre).
        var roles = mine.Count == 0
            ? new Dictionary<int, string> { [Perimeter.DefaultId] = adRole }
            : mine.GroupBy(a => a.PerimeterId).ToDictionary(g => g.Key, g => g.Any(a => a.Role == "Manager") ? "Manager" : "User");
        return _scope = new PerimeterScope(false, roles);
    }

    /// <summary>
    /// PER-04 : périmètre d'un nouvel enregistrement — celui demandé, ou l'unique périmètre
    /// de l'utilisateur (« Organisation » pour l'Admin). Refus : périmètre à choisir (400),
    /// hors de ses périmètres (403), supprimé (400).
    /// </summary>
    public async Task<(int? Id, string? Error, bool Forbidden)> ChooseAsync(int? requested)
    {
        var scope = await GetAsync();
        var id = requested ?? (scope.Global ? Perimeter.DefaultId : scope.Ids.Count == 1 ? scope.Ids.First() : null);
        if (id is null)
            return scope.Ids.Count == 0 ? (null, null, true) : (null, "Choisir le périmètre de l'enregistrement.", false);
        if (!scope.Sees(id.Value)) return (null, null, true);
        if (!await db.Perimeters.AnyAsync(p => p.Id == id && p.DeletedAt == null))
            return (null, "Ce périmètre n'existe pas ou a été supprimé.", false);
        return (id, null, false);
    }

    /// <summary>PER-07 : déplacer un enregistrement exige d'être gestionnaire des deux périmètres (ou Admin).</summary>
    public async Task<(string? Error, bool Forbidden)> CheckMoveAsync(int from, int to)
    {
        var scope = await GetAsync();
        if (!scope.ManagerOn(from) || !scope.ManagerOn(to)) return (null, true);
        return await db.Perimeters.AnyAsync(p => p.Id == to && p.DeletedAt == null)
            ? (null, false) : ("Ce périmètre n'existe pas ou a été supprimé.", false);
    }
}
