using SAloha.Api.Core.Auth;

namespace SAloha.Api.Tests;

/// <summary>Membres des groupes de rôle pour les tests (ADM-10) ; <see cref="Unavailable"/> simule un annuaire injoignable.</summary>
public sealed class FakeRoster : IDirectoryRoster
{
    public static List<RosterEntry> Members { get; } = [];
    public static bool Unavailable { get; set; }

    public Task<List<RosterEntry>> RoleMembersAsync() => Unavailable
        ? throw new DirectoryUnavailableException("Annuaire injoignable (test).")
        : Task.FromResult(Members.ToList());
}
