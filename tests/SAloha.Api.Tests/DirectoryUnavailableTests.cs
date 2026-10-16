using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SAloha.Api.Tests;

/// <summary>
/// R6 : annuaire injoignable — l'environnement de test pointe sur un contrôleur de domaine
/// introuvable (appsettings : dc01.mondomaine.local). La connexion répond 503, pas « identifiants
/// invalides » ; la recherche dans l'annuaire répond 503, pas 500.
/// </summary>
[Collection("api")]
public class DirectoryUnavailableTests(ApiFixture api)
{
    private static async Task<string> Message(HttpResponseMessage res, HttpStatusCode expected)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Attendu {(int)expected}, reçu {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("message").GetString()!;
    }

    [Fact]
    public async Task R6_connexion_et_recherche_repondent_503_quand_l_annuaire_est_injoignable()
    {
        var login = await api.Client().PostAsJsonAsync("/api/auth/login", new { username = "demo.user", password = "secret" });
        Assert.StartsWith("Annuaire injoignable", await Message(login, HttpStatusCode.ServiceUnavailable));

        var search = await api.Client("User", "r6.user").GetAsync("/api/directory/search?q=demo");
        Assert.StartsWith("Annuaire injoignable", await Message(search, HttpStatusCode.ServiceUnavailable));

        // Saisie incomplète : refusée avant tout appel à l'annuaire.
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Client().PostAsJsonAsync("/api/auth/login", new { username = "", password = "" })).StatusCode);
    }

    [Fact]
    public async Task R7_un_controleur_qui_ne_repond_pas_est_abandonne_apres_le_delai()
    {
        // Adresse non routable : la connexion TCP n'aboutit jamais ; sans délai, le système attendrait bien plus.
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ldap:Host"] = "10.255.255.1", ["Ldap:Port"] = "389", ["Ldap:BaseDn"] = "DC=test,DC=local",
            ["Ldap:BindUser"] = "CN=svc,DC=test,DC=local", ["Ldap:BindPassword"] = "x", ["Ldap:TimeoutSeconds"] = "1"
        }).Build();
        var ldap = new SAloha.Api.Core.Auth.LdapService(config, Microsoft.Extensions.Logging.Abstractions.NullLogger<SAloha.Api.Core.Auth.LdapService>.Instance);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAsync<SAloha.Api.Core.Auth.DirectoryUnavailableException>(() => ldap.AuthenticateAsync("demo.user", "secret"));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"Abandon après {watch.Elapsed.TotalSeconds:0.0} s");
    }
}
