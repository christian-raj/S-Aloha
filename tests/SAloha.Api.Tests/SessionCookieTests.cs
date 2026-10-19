using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SAloha.Api.Tests;

/// <summary>S5 (ADR-0018) : jeton en cookie HttpOnly, protection CSRF, déconnexion.</summary>
[Collection("api")]
public class SessionCookieTests(ApiFixture api)
{
    private static object Incident(string title) => new
    {
        title, description = "d", impact = "Faible", urgency = "Faible", category = "Test", affectedService = "Test"
    };

    private async Task<(HttpClient Client, HttpResponseMessage Login)> SignIn(string user, bool returnToken = false)
    {
        var client = api.AnonymousClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = user, password = FakeAuthenticator.Password, returnToken });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (client, login);
    }

    [Fact]
    public async Task S5_la_connexion_pose_un_cookie_HttpOnly_et_ne_renvoie_pas_le_jeton()
    {
        var (client, login) = await SignIn("s5.user");
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("saloha_session="));
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.Contains("samesite=strict", cookie.ToLowerInvariant());
        Assert.Contains("path=/api", cookie.ToLowerInvariant());
        var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement;
        Assert.False(body.TryGetProperty("token", out _));
        Assert.Equal("User", body.GetProperty("role").GetString());

        // Le cookie seul suffit à lire ; la session se retrouve par /me.
        var me = JsonDocument.Parse(await client.GetStringAsync("/api/auth/me")).RootElement;
        Assert.Equal("s5.user", me.GetProperty("username").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/incidents")).StatusCode);
    }

    [Fact]
    public async Task S5_une_modification_par_cookie_exige_l_en_tete_CSRF()
    {
        var (client, _) = await SignIn("s5.csrf");
        var refused = await client.PostAsJsonAsync("/api/incidents", Incident("Sans en-tête"));
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Contains("CSRF", await refused.Content.ReadAsStringAsync());

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/incidents") { Content = JsonContent.Create(Incident("Avec en-tête")) };
        request.Headers.Add("X-S-Aloha", "1");
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(request)).StatusCode);
        // Un client d'API (en-tête Authorization) n'est pas concerné.
        Assert.Equal(HttpStatusCode.Created, (await api.Client("User", "s5.bearer").PostAsJsonAsync("/api/incidents", Incident("Bearer"))).StatusCode);
    }

    [Fact]
    public async Task S5_la_deconnexion_efface_le_cookie_et_un_script_peut_demander_le_jeton()
    {
        var (client, _) = await SignIn("s5.logout");
        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Add("X-S-Aloha", "1");
        var res = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Contains(res.Headers.GetValues("Set-Cookie"), c => c.StartsWith("saloha_session=;") || c.Contains("expires=Thu, 01 Jan 1970"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

        var (_, scripted) = await SignIn("s5.script", returnToken: true);
        var token = JsonDocument.Parse(await scripted.Content.ReadAsStringAsync()).RootElement.GetProperty("token").GetString();
        var bearer = api.AnonymousClient();
        bearer.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await bearer.GetAsync("/api/auth/me")).StatusCode);
    }
}
