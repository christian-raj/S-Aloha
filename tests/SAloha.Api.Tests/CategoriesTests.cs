using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SAloha.Api.Tests;

/// <summary>Paramétrage › Catégories (ADM-50 à ADM-53, SOC-27).</summary>
[Collection("api")]
public class CategoriesTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement;
    }

    private static object Incident(string title, string category) => new
    {
        title, description = "d", impact = "Faible", urgency = "Faible", category, affectedService = "Test"
    };

    /// <summary>La base est partagée : la liste créée par un test est vidée à la fin, la saisie redevient libre.</summary>
    private async Task Clean(string practice)
    {
        var admin = api.Client("Admin", "admin.adm50");
        foreach (var c in (await Json(await admin.GetAsync($"/api/categories?practice={practice}"))).EnumerateArray())
        {
            var id = c.GetProperty("id").GetInt32();
            var res = await admin.DeleteAsync($"/api/categories/{id}");
            if (res.StatusCode == HttpStatusCode.Conflict)
                await Json(await admin.PutAsJsonAsync($"/api/categories/{id}", new { practice, name = c.GetProperty("name").GetString(), active = false }));
        }
    }

    [Fact]
    public async Task ADM_50_ADM_51_liste_imposee_des_qu_elle_existe()
    {
        var manager = api.Client("Manager", "chef.adm50");
        // Sans liste : saisie libre (installation neuve).
        await Json(await manager.PostAsJsonAsync("/api/incidents", Incident("Libre adm50", "Quelconque adm50")));
        try
        {
            var reseau = await Json(await manager.PostAsJsonAsync("/api/categories", new { practice = "incident", name = "Réseau adm50" }));
            Assert.Equal(HttpStatusCode.BadRequest,
                (await manager.PostAsJsonAsync("/api/categories", new { practice = "incident", name = "réseau ADM50" })).StatusCode);

            var refused = await manager.PostAsJsonAsync("/api/incidents", Incident("Refusé adm50", "Inconnue adm50"));
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Contains("Réseau adm50", await refused.Content.ReadAsStringAsync());

            // Ramenée au nom exact de la liste.
            var ok = await Json(await manager.PostAsJsonAsync("/api/incidents", Incident("Accepté adm50", "  réseau ADM50 ")));
            Assert.Equal("Réseau adm50", ok.GetProperty("category").GetString());

            // Liste propre à la pratique : les problèmes restent libres.
            await Json(await manager.PostAsJsonAsync("/api/problems", new
            {
                title = "Problème adm50", description = "d", impact = "Moyen", urgency = "Moyenne", category = "Inconnue adm50", affectedService = "Test"
            }));

            var listed = (await Json(await api.Client("User", "agent.adm50").GetAsync("/api/categories?practice=incident"))).EnumerateArray()
                .Single(c => c.GetProperty("name").GetString() == "Réseau adm50");
            Assert.Equal(1, listed.GetProperty("usage").GetInt32());

            // Utilisée : non supprimable (409), désactivable ; une valeur désactivée déjà portée se conserve.
            var id = reseau.GetProperty("id").GetInt32();
            Assert.Equal(HttpStatusCode.Conflict, (await manager.DeleteAsync($"/api/categories/{id}")).StatusCode);
            await Json(await manager.PostAsJsonAsync("/api/categories", new { practice = "incident", name = "Serveur adm50" }));
            await Json(await manager.PutAsJsonAsync($"/api/categories/{id}", new { practice = "incident", name = "Réseau adm50", active = false }));
            var incidentId = ok.GetProperty("id").GetInt32();
            var kept = await Json(await manager.PutAsJsonAsync($"/api/incidents/{incidentId}", Incident("Accepté adm50 (modifié)", "Réseau adm50")));
            Assert.Equal("Réseau adm50", kept.GetProperty("category").GetString());
            Assert.Equal(HttpStatusCode.BadRequest,
                (await manager.PostAsJsonAsync("/api/incidents", Incident("Désactivée adm50", "Réseau adm50"))).StatusCode);
        }
        finally { await Clean("incident"); }
    }

    [Fact]
    public async Task ADM_50_tenue_par_les_gestionnaires()
    {
        var user = api.Client("User", "agent.adm50b");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await user.PostAsJsonAsync("/api/categories", new { practice = "problem", name = "Refus adm50b" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.GetAsync("/api/categories?practice=change")).StatusCode);
        var admin = api.Client("Admin", "admin.adm50b");
        try
        {
            var c = await Json(await admin.PostAsJsonAsync("/api/categories", new { practice = "problem", name = "Base adm50b" }));
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/categories/{c.GetProperty("id").GetInt32()}")).StatusCode);
        }
        finally { await Clean("problem"); }
    }
}
