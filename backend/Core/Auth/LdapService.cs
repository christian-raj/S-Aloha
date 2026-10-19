using Novell.Directory.Ldap;
using SAloha.Api.Core.Directory;

namespace SAloha.Api.Core.Auth;

public class LdapService(IConfiguration config, ILogger<LdapService> logger) : IUserDirectory, IAuthenticator
{
    private string Host => config["Ldap:Host"]!;
    private int Port => int.Parse(config["Ldap:Port"] ?? "389");
    private bool UseSsl => bool.Parse(config["Ldap:UseSsl"] ?? "false");
    private string BaseDn => config["Ldap:BaseDn"]!;
    /// <summary>R7 : délai maximal de connexion et de chaque opération (5 s par défaut).</summary>
    private int TimeoutMs => (int.TryParse(config["Ldap:TimeoutSeconds"], out var s) && s > 0 ? s : 5) * 1000;

    // Novell.Directory.Ldap 4 : API asynchrone (connexion, liaison, recherche).
    // R6 : serveur absent ou injoignable, ou compte de service refusé → DirectoryUnavailableException ;
    // seule la liaison d'un utilisateur avec un mauvais mot de passe reste une LdapException.
    private async Task<LdapConnection> ConnectAsync(string dn, string password, bool service = false)
    {
        var conn = new LdapConnection { SecureSocketLayer = UseSsl, ConnectionTimeout = TimeoutMs };
        var constraints = conn.Constraints;
        constraints.TimeLimit = TimeoutMs;   // liaison et recherches bornées (R7)
        conn.Constraints = constraints;
        try
        {
            await conn.ConnectAsync(Host, Port);
        }
        catch (Exception ex)
        {
            conn.Dispose();
            throw new DirectoryUnavailableException($"Annuaire injoignable ({Host}:{Port}).", ex);
        }
        try
        {
            await conn.BindAsync(dn, password);
            return conn;
        }
        catch (LdapException ex) when (service || Unavailable(ex))
        {
            conn.Dispose();
            throw new DirectoryUnavailableException(service ? "Compte de service de l'annuaire refusé." : "Annuaire injoignable.", ex);
        }
        catch
        {
            conn.Dispose();
            throw;
        }
    }

    /// <summary>Codes LDAP d'un serveur injoignable ou indisponible (et non d'un refus).</summary>
    private static bool Unavailable(LdapException ex) => ex.ResultCode is LdapException.ServerDown or LdapException.ConnectError
        or LdapException.Unavailable or LdapException.Busy or LdapException.LdapTimeout;

    private Task<LdapConnection> ServiceConnectAsync() =>
        ConnectAsync(config["Ldap:BindUser"]!, config["Ldap:BindPassword"]!, service: true);

    /// <summary>Première entrée d'une recherche ; les référents d'un AD (Configuration, DomainDnsZones…) ne sont pas des comptes.</summary>
    private static async Task<LdapEntry?> FirstEntryAsync(ILdapSearchResults search)
    {
        while (await search.HasMoreAsync())
        {
            try { return await search.NextAsync(); }
            catch (LdapReferralException) { }
        }
        return null;
    }

    /// <summary>
    /// Authentifie l'utilisateur et retourne (sAMAccountName de l'annuaire,
    /// displayName, rôle applicatif) ou null. L'identifiant renvoyé est celui
    /// de l'AD, pas la saisie : l'AD ignore la casse, l'application compare
    /// des chaînes (M2, revue du 2026-10-06). Les groupes AD directs de
    /// l'utilisateur (CN) sont renvoyés pour le jeton (SOC-10).
    /// </summary>
    public async Task<(string Username, string DisplayName, string Role, IReadOnlyCollection<string> Groups)?> AuthenticateAsync(string username, string password)
    {
        try
        {
            using var svc = await ServiceConnectAsync();
            var filter = string.Format(config["Ldap:UserFilter"] ?? "(sAMAccountName={0})", EscapeFilter(username));
            var search = await svc.SearchAsync(BaseDn, LdapConnection.ScopeSub, filter,
                new[] { "distinguishedName", "sAMAccountName", "displayName", "memberOf" }, false);
            var entry = await FirstEntryAsync(search);
            if (entry is null) return null;

            // vérifie le mot de passe par un bind avec le DN de l'utilisateur
            using (var userConn = await ConnectAsync(entry.Dn, password)) { }

            var display = GetAttr(entry, "displayName") ?? username;
            var groups = GetAttrValues(entry, "memberOf")
                .Select(dn => dn.Split(',')[0].Replace("CN=", "", StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var account = GetAttr(entry, "sAMAccountName") ?? username;
            var role = ResolveRole(groups);
            return role is null ? null : (account, display, role, groups);
        }
        catch (LdapException ex) when (Unavailable(ex))
        {
            throw new DirectoryUnavailableException("Annuaire injoignable.", ex);
        }
        catch (LdapException ex)
        {
            // Saisie non authentifiée : sans retrait des sauts de ligne, un
            // identifiant forgé injecterait de fausses lignes dans le journal.
            var logged = username.Replace("\r", "").Replace("\n", "");
            logger.LogWarning(ex, "Échec d'authentification LDAP pour {User}", logged);
            return null;
        }
    }

    /// <summary>INC-13 : rôle et groupes d'un compte, lus par le compte de service (sans mot de passe de l'utilisateur).</summary>
    public async Task<DirectoryAccount?> FindAsync(string username)
    {
        using var svc = await ServiceConnectAsync();
        var filter = string.Format(config["Ldap:UserFilter"] ?? "(sAMAccountName={0})", EscapeFilter(username));
        var search = await svc.SearchAsync(BaseDn, LdapConnection.ScopeSub, filter, new[] { "memberOf" }, false);
        // Un AD renvoie des référents (DomainDnsZones…) : ils sont ignorés. Sans cela, un compte
        // inexistant levait une exception, prise pour un annuaire injoignable.
        var entry = await FirstEntryAsync(search);
        if (entry is null) return null;
        var groups = GetAttrValues(entry, "memberOf")
            .Select(dn => dn.Split(',')[0].Replace("CN=", "", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var role = ResolveRole(groups);
        return role is null ? null : new DirectoryAccount(role, groups);
    }

    private string? ResolveRole(HashSet<string> groups)
    {
        var map = config.GetSection("Ldap:Groups").GetChildren(); // Admin, Manager, User -> nom de groupe AD
        foreach (var role in new[] { "Admin", "Manager", "User" })
        {
            var g = config[$"Ldap:Groups:{role}"];
            if (g != null && groups.Contains(g)) return role;
        }
        return null; // utilisateur hors des groupes autorisés
    }

    /// <summary>Recherche d'utilisateurs et de groupes pour l'affectation RACI.</summary>
    public async Task<List<DirectoryEntry>> SearchAsync(string query, int max = 15)
    {
        var results = new List<DirectoryEntry>();
        using var svc = await ServiceConnectAsync();
        var q = EscapeFilter(query);
        var filter = $"(|(&(objectClass=user)(|(sAMAccountName=*{q}*)(displayName=*{q}*)))(&(objectClass=group)(cn=*{q}*)))";
        var search = await svc.SearchAsync(BaseDn, LdapConnection.ScopeSub, filter,
            new[] { "sAMAccountName", "displayName", "cn", "objectClass" }, false);
        while (results.Count < max && await search.HasMoreAsync())
        {
            try
            {
                var e = await search.NextAsync();
                var classes = GetAttrValues(e, "objectClass");
                bool isGroup = classes.Contains("group");
                var id = GetAttr(e, isGroup ? "cn" : "sAMAccountName") ?? "";
                var name = GetAttr(e, "displayName") ?? GetAttr(e, "cn") ?? id;
                if (id != "") results.Add(new DirectoryEntry(id, name, isGroup ? "Group" : "User"));
            }
            catch (LdapReferralException) { /* ignorer les référents */ }
        }
        return results;
    }

    private static string? GetAttr(LdapEntry e, string attr)
    {
        try { return e.GetOrDefault(attr, null!)?.StringValue; }
        catch { return null; }
    }

    private static IEnumerable<string> GetAttrValues(LdapEntry e, string attr)
    {
        try { return e.GetOrDefault(attr, null!)?.StringValueArray ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    private static string EscapeFilter(string v) => v
        .Replace(@"\", @"\5c").Replace("*", @"\2a")
        .Replace("(", @"\28").Replace(")", @"\29").Replace("\0", @"\00");
}
