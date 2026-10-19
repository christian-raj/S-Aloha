using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SAloha.Api.Core.Administration;

namespace SAloha.Api.Tests;

/// <summary>Administration › Nettoyage des doublons (ADM-40 à ADM-42).</summary>
[Collection("api")]
public class AdminDuplicatesTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();
    private static string Ref(JsonElement e) => e.GetProperty("reference").GetString()!;

    private static object Incident(string title) => new
    {
        title, description = "d", impact = "Faible", urgency = "Faible", category = "Test", affectedService = "Test"
    };

    private static async Task<List<JsonElement>> PairsWith(HttpClient admin, int id) =>
        (await Json(await admin.GetAsync("/api/admin/duplicates"))).EnumerateArray()
            .Where(p => Id(p.GetProperty("first")) == id || Id(p.GetProperty("second")) == id).ToList();

    [Fact]
    public void ADM_40_titre_normalise()
    {
        Assert.Equal("messagerie lente au siege", Duplicates.Normalize("  Messagerie LENTE — au siège !"));
        Assert.Equal(Duplicates.Normalize("Imprimante HS (2e étage)"), Duplicates.Normalize("imprimante hs 2e etage"));
    }

    [Fact]
    public async Task ADM_40_ADM_41_doublon_detecte_puis_fusionne_liens_et_commentaires_reportes()
    {
        var admin = api.Client("Admin", "admin.adm41");
        var keep = await Json(await admin.PostAsJsonAsync("/api/incidents", Incident("Messagerie lente adm41")));
        var dup = await Json(await admin.PostAsJsonAsync("/api/incidents", Incident("messagerie LENTE — adm41 !")));
        var other = await Json(await admin.PostAsJsonAsync("/api/incidents", Incident("Imprimante adm41")));
        var change = await Json(await admin.PostAsJsonAsync("/api/changes", new
        {
            title = "Changement adm41", changeType = "Normal", risk = "Faible", implementationPlan = "p", backoutPlan = "b"
        }));
        var problem = await Json(await admin.PostAsJsonAsync("/api/problems", new
        {
            title = "Problème adm41", description = "d", impact = "Moyen", urgency = "Moyenne", category = "Test", affectedService = "Test"
        }));
        // Le doublon est relié à un changement (reporté) et au même problème que l'incident conservé (pas de lien en double).
        await Json(await admin.PostAsJsonAsync("/api/links", new { fromType = "incident", fromId = Id(dup), toReference = Ref(change) }));
        await Json(await admin.PostAsJsonAsync("/api/links", new { fromType = "incident", fromId = Id(dup), toReference = Ref(problem) }));
        await Json(await admin.PostAsJsonAsync("/api/links", new { fromType = "incident", fromId = Id(keep), toReference = Ref(problem) }));
        await Json(await admin.PostAsJsonAsync("/api/comments", new { type = "incident", id = Id(dup), body = "Même panne au 3e." }));

        var pair = Assert.Single(await PairsWith(admin, Id(keep)));
        Assert.Equal(new[] { Id(keep), Id(dup) }.Order(), new[] { Id(pair.GetProperty("first")), Id(pair.GetProperty("second")) }.Order());
        Assert.Empty(await PairsWith(admin, Id(other)));
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client("Manager", "chef.adm41").GetAsync("/api/admin/duplicates")).StatusCode);

        var merged = await Json(await admin.PostAsJsonAsync("/api/admin/duplicates/merge", new { type = "incident", keepId = Id(keep), removeId = Id(dup) }));
        Assert.Equal((1, 1), (merged.GetProperty("linksMoved").GetInt32(), merged.GetProperty("commentsMoved").GetInt32()));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/incidents/{Id(dup)}")).StatusCode);
        var links = (await Json(await admin.GetAsync($"/api/links?type=incident&id={Id(keep)}"))).EnumerateArray().Select(Ref).ToList();
        Assert.Equal(new[] { Ref(change), Ref(problem) }.Order(), links.Order());
        var comments = (await Json(await admin.GetAsync($"/api/comments?type=incident&id={Id(keep)}"))).GetProperty("comments").EnumerateArray();
        Assert.Contains(comments, c => c.GetProperty("body").GetString() == "Même panne au 3e.");
        var audit = (await Json(await admin.GetAsync($"/api/audit?type=incident&id={Id(keep)}"))).EnumerateArray();
        Assert.Contains(audit, a => a.GetProperty("action").GetString() == "Fusion" && a.GetProperty("newValue").GetString()!.Contains(Ref(dup)));
        Assert.Empty(await PairsWith(admin, Id(keep)));
    }

    [Fact]
    public async Task ADM_41_ADM_42_fusion_refusee_si_elements_propres_et_paire_ecartee()
    {
        var admin = api.Client("Admin", "admin.adm42");
        object Problem() => new { title = "Fuite mémoire adm42", description = "d", impact = "Moyen", urgency = "Moyenne", category = "Test", affectedService = "Test" };
        var a = await Json(await admin.PostAsJsonAsync("/api/problems", Problem()));
        var b = await Json(await admin.PostAsJsonAsync("/api/problems", Problem()));
        await Json(await admin.PostAsJsonAsync($"/api/problems/{Id(b)}/analyses", new { method = "FIVE_WHYS", dataJson = "{}", conclusion = (string?)null }));
        var refused = await admin.PostAsJsonAsync("/api/admin/duplicates/merge", new { type = "problem", keepId = Id(a), removeId = Id(b) });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("analyses de cause racine", await refused.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/problems/{Id(b)}")).StatusCode);

        Assert.Single(await PairsWith(admin, Id(a)));
        await Json(await admin.PostAsJsonAsync("/api/admin/duplicates/dismiss", new { type = "problem", firstId = Id(b), secondId = Id(a) }));
        Assert.Empty(await PairsWith(admin, Id(a)));
    }
}
