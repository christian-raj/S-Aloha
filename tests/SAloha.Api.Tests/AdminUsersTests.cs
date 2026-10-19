using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Auth;

namespace SAloha.Api.Tests;

/// <summary>Administration › Utilisateurs et rôles (ADM-10 à ADM-13).</summary>
[Collection("api")]
public class AdminUsersTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    [Fact]
    public async Task ADM_10_a_ADM_13_membres_roles_par_perimetre_derniere_connexion_et_comptes_sans_acces()
    {
        var admin = api.Client("Admin", "admin.adm10");
        var site = (await Json(await admin.PostAsJsonAsync("/api/admin/perimeters", new { name = "Site adm10" }))).GetProperty("id").GetInt32();
        await Json(await admin.PostAsJsonAsync($"/api/admin/perimeters/{site}/assignments",
            new { principalType = "Group", principalId = "GRP-ADM10", principalDisplayName = "Équipe adm10", role = "Manager" }));
        await Json(await admin.PostAsJsonAsync($"/api/admin/perimeters/{site}/assignments",
            new { principalType = "User", principalId = "parti.adm10", principalDisplayName = "Parti", role = "User" }));
        FakeRoster.Members.Clear();
        FakeRoster.Members.Add(new RosterEntry("tiana.adm10", "Tiana", "User", ["GRP-SALOHA-USERS"], ["GRP-ADM10", "GRP-SALOHA-USERS"]));
        FakeRoster.Members.Add(new RosterEntry("hery.adm10", "Hery", "Admin", ["GRP-SALOHA-ADMINS"], ["GRP-SALOHA-ADMINS"]));

        // ADM-12 : une connexion réussie retient la date, rien d'autre.
        var login = await api.AnonymousClient().PostAsJsonAsync("/api/auth/login", new { username = "tiana.adm10", password = FakeAuthenticator.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await using (var db = api.Database())
            Assert.NotNull(await db.LastLogins.FindAsync("tiana.adm10"));

        var res = await Json(await admin.GetAsync("/api/admin/users"));
        var tiana = res.GetProperty("users").EnumerateArray().Single(u => u.GetProperty("username").GetString() == "tiana.adm10");
        Assert.Equal("User", tiana.GetProperty("role").GetString());
        Assert.NotEqual(JsonValueKind.Null, tiana.GetProperty("lastLoginAt").ValueKind);
        var onSite = tiana.GetProperty("perimeters").EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == site);
        Assert.Equal("Manager", onSite.GetProperty("role").GetString());   // par son groupe AD (PER-03, ADR-0014 D4)
        Assert.Contains("groupe Équipe adm10", onSite.GetProperty("via").EnumerateArray().Select(v => v.GetString()));
        var hery = res.GetProperty("users").EnumerateArray().Single(u => u.GetProperty("username").GetString() == "hery.adm10");
        Assert.Equal(JsonValueKind.Null, hery.GetProperty("perimeters").ValueKind);   // Admin : portée globale
        Assert.Equal(JsonValueKind.Null, hery.GetProperty("lastLoginAt").ValueKind);
        // ADM-11 : affectation d'un compte absent des groupes de rôle.
        Assert.Contains(res.GetProperty("withoutAccess").EnumerateArray(), o => o.GetProperty("principalId").GetString() == "parti.adm10");
        Assert.Contains(res.GetProperty("mapping").EnumerateArray(), m => m.GetProperty("role").GetString() == "Admin");
        // Filtres.
        Assert.Single((await Json(await admin.GetAsync("/api/admin/users?role=Admin"))).GetProperty("users").EnumerateArray());
        Assert.Single((await Json(await admin.GetAsync("/api/admin/users?q=tiana"))).GetProperty("users").EnumerateArray());

        // ADM-13 : annuaire injoignable, pas de liste partielle.
        FakeRoster.Unavailable = true;
        try { Assert.Equal(HttpStatusCode.ServiceUnavailable, (await admin.GetAsync("/api/admin/users")).StatusCode); }
        finally { FakeRoster.Unavailable = false; }
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client("Manager", "chef.adm10").GetAsync("/api/admin/users")).StatusCode);
    }
}
