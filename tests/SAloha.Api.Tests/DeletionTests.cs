using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SAloha.Api.Tests;

/// <summary>SOC-14 (M8) : aperçu de ce qu'une suppression emporte, suppression par l'Admin.</summary>
[Collection("api")]
public class DeletionTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();

    private static Dictionary<string, int> Impacts(JsonElement preview) =>
        preview.GetProperty("impacts").EnumerateArray().ToDictionary(i => i.GetProperty("label").GetString()!, i => i.GetProperty("count").GetInt32());

    [Fact]
    public async Task SOC_14_l_apercu_compte_ce_qui_part_avec_le_probleme_puis_la_suppression_l_emporte()
    {
        var admin = api.Client("Admin", "admin.soc14");
        var p = await Json(await admin.PostAsJsonAsync("/api/problems", new
        {
            title = "Suppression soc14", description = "d", impact = "Moyen", urgency = "Moyenne", category = "Test", affectedService = "Test"
        }));
        await Json(await admin.PostAsJsonAsync($"/api/problems/{Id(p)}/analyses", new { method = "FIVE_WHYS", dataJson = "{}", conclusion = (string?)null }));
        await Json(await admin.PostAsJsonAsync($"/api/problems/{Id(p)}/actions", new
        {
            title = "Corriger", description = "", dueDate = (DateTime?)null,
            raci = new[] { new { role = "R", assigneeType = "User", assigneeId = "r.soc14", assigneeDisplayName = "R" },
                           new { role = "A", assigneeType = "User", assigneeId = "a.soc14", assigneeDisplayName = "A" } }
        }));
        var inc = await Json(await admin.PostAsJsonAsync("/api/incidents", new
        {
            title = "Incident soc14", description = "d", impact = "Moyen", urgency = "Moyenne", category = "Test", affectedService = "Test"
        }));
        await Json(await admin.PostAsJsonAsync("/api/links", new { fromType = "incident", fromId = Id(inc), toReference = p.GetProperty("reference").GetString() }));
        await Json(await admin.PostAsJsonAsync("/api/comments", new { type = "problem", id = Id(p), body = "À supprimer." }));

        var preview = await Json(await admin.GetAsync($"/api/problems/{Id(p)}/deletion"));
        Assert.Equal(p.GetProperty("reference").GetString(), preview.GetProperty("reference").GetString());
        var impacts = Impacts(preview);
        Assert.Equal(1, impacts["liens vers d'autres enregistrements"]);
        Assert.Equal(1, impacts["commentaires"]);
        Assert.Equal(1, impacts["analyses de cause racine"]);
        Assert.Equal(1, impacts["actions correctives"]);

        // Réservé à l'Admin, comme la suppression.
        var manager = api.Client("Manager", "chef.soc14");
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync($"/api/problems/{Id(p)}/deletion")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.DeleteAsync($"/api/problems/{Id(p)}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/problems/{Id(p)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/problems/{Id(p)}")).StatusCode);
        Assert.Empty((await Json(await admin.GetAsync($"/api/links?type=incident&id={Id(inc)}"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/problems/{Id(p)}/deletion")).StatusCode);
    }

    [Fact]
    public async Task SOC_14_un_CI_annonce_ses_relations_et_un_enregistrement_seul_rien()
    {
        var admin = api.Client("Admin", "admin.soc14b");
        object Ci(string title) => new
        {
            title, ciType = "Serveur", environment = "Production", status = "Planifié"
        };
        var a = await Json(await admin.PostAsJsonAsync("/api/configuration-items", Ci("srv-soc14-a")));
        var b = await Json(await admin.PostAsJsonAsync("/api/configuration-items", Ci("srv-soc14-b")));
        await Json(await admin.PostAsJsonAsync($"/api/configuration-items/{Id(a)}/relations",
            new { targetReference = b.GetProperty("reference").GetString(), type = "Dépend de" }));
        Assert.Equal(1, Impacts(await Json(await admin.GetAsync($"/api/configuration-items/{Id(b)}/deletion")))["relations entre CI"]);

        var lone = await Json(await admin.PostAsJsonAsync("/api/configuration-items", Ci("srv-soc14-seul")));
        Assert.Empty(Impacts(await Json(await admin.GetAsync($"/api/configuration-items/{Id(lone)}/deletion"))));
    }
}
