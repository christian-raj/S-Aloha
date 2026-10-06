using Novell.Directory.Ldap;
using SAloha.Api.Core.Directory;

namespace SAloha.Api.Core.Auth;

public class LdapService(IConfiguration config, ILogger<LdapService> logger)
{
    private string Host => config["Ldap:Host"]!;
    private int Port => int.Parse(config["Ldap:Port"] ?? "389");
    private bool UseSsl => bool.Parse(config["Ldap:UseSsl"] ?? "false");
    private string BaseDn => config["Ldap:BaseDn"]!;

    private LdapConnection Connect(string dn, string password)
    {
        var conn = new LdapConnection { SecureSocketLayer = UseSsl };
        conn.Connect(Host, Port);
        conn.Bind(dn, password);
        return conn;
    }

    private LdapConnection ServiceConnect() =>
        Connect(config["Ldap:BindUser"]!, config["Ldap:BindPassword"]!);

    /// <summary>
    /// Authentifie l'utilisateur et retourne (sAMAccountName de l'annuaire,
    /// displayName, rôle applicatif) ou null. L'identifiant renvoyé est celui
    /// de l'AD, pas la saisie : l'AD ignore la casse, l'application compare
    /// des chaînes (M2, revue du 2026-10-06).
    /// </summary>
    public (string Username, string DisplayName, string Role)? Authenticate(string username, string password)
    {
        try
        {
            using var svc = ServiceConnect();
            var filter = string.Format(config["Ldap:UserFilter"] ?? "(sAMAccountName={0})", EscapeFilter(username));
            var search = svc.Search(BaseDn, LdapConnection.ScopeSub, filter,
                new[] { "distinguishedName", "sAMAccountName", "displayName", "memberOf" }, false);
            if (!search.HasMore()) return null;
            var entry = search.Next();

            // vérifie le mot de passe par un bind avec le DN de l'utilisateur
            using (var userConn = Connect(entry.Dn, password)) { }

            var display = GetAttr(entry, "displayName") ?? username;
            var groups = GetAttrValues(entry, "memberOf")
                .Select(dn => dn.Split(',')[0].Replace("CN=", "", StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var account = GetAttr(entry, "sAMAccountName") ?? username;
            var role = ResolveRole(groups);
            return role is null ? null : (account, display, role);
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
    public List<DirectoryEntry> Search(string query, int max = 15)
    {
        var results = new List<DirectoryEntry>();
        using var svc = ServiceConnect();
        var q = EscapeFilter(query);
        var filter = $"(|(&(objectClass=user)(|(sAMAccountName=*{q}*)(displayName=*{q}*)))(&(objectClass=group)(cn=*{q}*)))";
        var search = svc.Search(BaseDn, LdapConnection.ScopeSub, filter,
            new[] { "sAMAccountName", "displayName", "cn", "objectClass" }, false);
        while (search.HasMore() && results.Count < max)
        {
            try
            {
                var e = search.Next();
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
        try { return e.GetAttribute(attr)?.StringValue; }
        catch { return null; }
    }

    private static IEnumerable<string> GetAttrValues(LdapEntry e, string attr)
    {
        try { return e.GetAttribute(attr)?.StringValueArray ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    private static string EscapeFilter(string v) => v
        .Replace(@"\", @"\5c").Replace("*", @"\2a")
        .Replace("(", @"\28").Replace(")", @"\29").Replace("\0", @"\00");
}
