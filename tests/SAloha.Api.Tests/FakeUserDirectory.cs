using SAloha.Api.Core.Auth;

namespace SAloha.Api.Tests;

/// <summary>
/// Annuaire de test (INC-13), sans LDAP : le rôle se lit dans l'identifiant — « admin » →
/// Admin, « manager » ou « chef » → Manager, « inconnu » → absent de l'annuaire,
/// « annuaire.hs » → annuaire injoignable, sinon User. Les groupes suivent « @groupe ». Comptes
/// des autres tests : hery.rakoto Admin, lova.rabe Manager.
/// </summary>
public sealed class FakeUserDirectory : IUserDirectory
{
    public Task<DirectoryAccount?> FindAsync(string username) => Task.FromResult(Find(username));

    public static DirectoryAccount? Find(string username)
    {
        var name = username.ToLowerInvariant();
        if (name.Contains("annuaire.hs")) throw new InvalidOperationException("Annuaire injoignable (test)");
        if (name.Contains("inconnu")) return null;
        var parts = name.Split('@');
        var role = parts[0] == "hery.rakoto" || parts[0].Contains("admin") ? "Admin"
            : parts[0] == "lova.rabe" ? "Manager"
            : parts[0].Contains("admin") ? "Admin" : parts[0].Contains("manager") || parts[0].Contains("chef") ? "Manager" : "User";
        return new DirectoryAccount(role, parts.Skip(1).ToList());
    }
}
