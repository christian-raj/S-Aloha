using SAloha.Api.Core.Auth;

namespace SAloha.Api.Tests;

/// <summary>
/// Connexion de test (S5) : le mot de passe <see cref="Password"/> ouvre une session pour
/// n'importe quel compte, au rôle donné par <see cref="FakeUserDirectory"/> ; tout autre mot de
/// passe passe par le vrai service LDAP, injoignable en test (R6 : 503).
/// </summary>
public sealed class FakeAuthenticator(LdapService ldap, FakeUserDirectory directory) : IAuthenticator
{
    public const string Password = "mot-de-passe-de-test";

    public async Task<(string Username, string DisplayName, string Role, IReadOnlyCollection<string> Groups)?> AuthenticateAsync(string username, string password)
    {
        if (password != Password) return await ldap.AuthenticateAsync(username, password);
        var account = await directory.FindAsync(username);
        return account is null ? null : (username, username, account.Role, account.Groups);
    }
}
