using System.Security.Claims;

namespace SAloha.Api.Core.Auth;

/// <summary>
/// Identité de l'utilisateur connecté vue par les règles d'affectation :
/// son compte et ses groupes AD, portés par le jeton (SOC-10). L'AD ignore la
/// casse : les clés sont en minuscules (M2).
/// </summary>
public static class Principals
{
    /// <summary>
    /// Type de claim d'un groupe AD (nom court, CN), un claim par groupe. Pas « group » ni
    /// « groups » : le gestionnaire JWT renommerait ces types à la lecture du jeton.
    /// </summary>
    public const string GroupClaim = "adgroup";

    public static string MeKey(this ClaimsPrincipal user) => (user.Identity?.Name ?? "").ToLowerInvariant();

    /// <summary>Groupes AD de l'utilisateur, en minuscules ; vide pour un jeton émis avant SOC-10.</summary>
    public static List<string> GroupKeys(this ClaimsPrincipal user) =>
        user.FindAll(GroupClaim).Select(c => c.Value.ToLowerInvariant()).Distinct().ToList();

    /// <summary>Vrai si (type, id) désigne l'utilisateur ou l'un de ses groupes.</summary>
    public static bool Designates(this ClaimsPrincipal user, string? type, string? id) =>
        id is not null && (type == "Group"
            ? user.GroupKeys().Contains(id.ToLowerInvariant())
            : id.ToLowerInvariant() == user.MeKey());
}
