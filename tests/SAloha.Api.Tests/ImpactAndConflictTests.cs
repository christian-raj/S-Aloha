using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SAloha.Api.Tests;

/// <summary>Vue d'impact d'un CI (#30) et conflits du calendrier des changements (#29).</summary>
[Collection("api")]
public class ImpactAndConflictTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();
    private static string Ref(JsonElement e) => e.GetProperty("reference").GetString()!;

    private static async Task<JsonElement> Ci(HttpClient c, string name) =>
        await Json(await c.PostAsJsonAsync("/api/configuration-items",
            new { title = name, ciType = "Serveur", environment = "Production" }));

    private static async Task Relate(HttpClient c, JsonElement source, JsonElement target, string type) =>
        await Json(await c.PostAsJsonAsync($"/api/configuration-items/{Id(source)}/relations",
            new { targetReference = Ref(target), type }));

    private static string[] Refs(JsonElement impact, string side) =>
        impact.GetProperty(side).EnumerateArray().Select(Ref).ToArray();

    [Fact]
    public async Task Impact_transitif_selon_le_sens_de_chaque_relation()
    {
        var c = api.Client();
        var appli = await Ci(c, "impact-appli");
        var bdd = await Ci(c, "impact-bdd");
        var hote = await Ci(c, "impact-hote");
        var baie = await Ci(c, "impact-baie");
        await Relate(c, appli, bdd, "Dépend de");     // la panne de bdd touche appli
        await Relate(c, hote, bdd, "Héberge");        // la panne de hote touche bdd
        await Relate(c, hote, baie, "Dépend de");     // la panne de baie touche hote

        var fromBaie = await Json(await c.GetAsync($"/api/configuration-items/{Id(baie)}/impact"));
        Assert.Equal(new[] { Ref(hote), Ref(bdd), Ref(appli) }, Refs(fromBaie, "downstream"));
        Assert.Empty(Refs(fromBaie, "upstream"));
        var appliEntry = fromBaie.GetProperty("downstream").EnumerateArray().Last();
        Assert.Equal(3, appliEntry.GetProperty("depth").GetInt32());
        Assert.Equal(Ref(bdd), Ref(appliEntry.GetProperty("via")));

        var fromAppli = await Json(await c.GetAsync($"/api/configuration-items/{Id(appli)}/impact"));
        Assert.Empty(Refs(fromAppli, "downstream"));
        Assert.Equal(new[] { Ref(bdd), Ref(hote), Ref(baie) }, Refs(fromAppli, "upstream"));
    }

    [Fact]
    public async Task Impact_termine_sur_un_cycle_et_ne_cite_chaque_CI_qu_une_fois()
    {
        var c = api.Client();
        var a = await Ci(c, "cycle-a");
        var b = await Ci(c, "cycle-b");
        var d = await Ci(c, "cycle-c");
        await Relate(c, a, b, "Se connecte à");
        await Relate(c, b, d, "Se connecte à");
        await Relate(c, d, a, "Se connecte à");

        var impact = await Json(await c.GetAsync($"/api/configuration-items/{Id(a)}/impact"));
        var down = Refs(impact, "downstream");
        Assert.Equal(new[] { Ref(d), Ref(b) }, down);    // a lui-même n'y figure pas
        Assert.Equal(down.Length, down.Distinct().Count());
        Assert.Equal(new[] { Ref(b), Ref(d) }, Refs(impact, "upstream"));
    }

    [Fact]
    public async Task Impact_d_un_CI_inconnu_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client().GetAsync("/api/configuration-items/999999/impact")).StatusCode);
    }

    private static readonly DateTime Day = DateTime.UtcNow.Date.AddDays(30);

    private static async Task<JsonElement> Change(HttpClient c, string title, int startHour, int? endHour, JsonElement ci)
    {
        var chg = await Json(await c.PostAsJsonAsync("/api/changes", new
        {
            title, changeType = "Standard", risk = "Faible",
            plannedStart = Day.AddHours(startHour), plannedEnd = endHour is null ? (DateTime?)null : Day.AddHours(endHour.Value)
        }));
        await Json(await c.PostAsJsonAsync("/api/links", new { fromType = "change", fromId = Id(chg), toReference = Ref(ci) }));
        return chg;
    }

    private static async Task<string[]> ConflictsOf(HttpClient c, JsonElement change) =>
        (await Json(await c.GetAsync($"/api/changes/{Id(change)}/conflicts")))
        .EnumerateArray().Select(x => x.GetProperty("changeReference").GetString()!).ToArray();

    [Fact]
    public async Task Conflit_quand_deux_changements_touchent_le_meme_CI_sur_des_creneaux_qui_se_chevauchent()
    {
        var c = api.Client();
        var srv = await Ci(c, "conflit-srv");
        var autre = await Ci(c, "conflit-autre");
        var a = await Change(c, "Conflit A", 20, 22, srv);
        var b = await Change(c, "Conflit B", 21, 23, srv);          // chevauche A sur srv
        var apres = await Change(c, "Conflit après", 22, 23, srv);  // commence quand A finit : chevauche B seulement
        var ailleurs = await Change(c, "Conflit ailleurs", 20, 22, autre);
        var sansFin = await Change(c, "Conflit sans fin", 22, null, autre); // une heure par défaut : 22 h–23 h

        Assert.Equal(new[] { Ref(b) }, await ConflictsOf(c, a));
        Assert.Equal(new[] { Ref(a), Ref(apres) }, (await ConflictsOf(c, b)).OrderBy(r => r).ToArray());
        Assert.Empty(await ConflictsOf(c, ailleurs));
        Assert.Empty(await ConflictsOf(c, sansFin));

        var schedule = await Json(await c.GetAsync("/api/changes/schedule"));
        var rowA = schedule.EnumerateArray().Single(x => Id(x) == Id(a));
        var conflict = rowA.GetProperty("conflicts").EnumerateArray().Single();
        Assert.Equal(Ref(srv), conflict.GetProperty("ciReference").GetString());
        Assert.Equal(Ref(b), conflict.GetProperty("changeReference").GetString());
    }

    [Fact]
    public async Task Un_changement_rejete_n_est_plus_en_conflit()
    {
        var c = api.Client();
        var srv = await Ci(c, "rejet-srv");
        var a = await Change(c, "Rejet A", 10, 12, srv);
        var b = await Change(c, "Rejet B", 11, 13, srv);
        Assert.Single(await ConflictsOf(c, a));

        await Json(await c.PutAsJsonAsync($"/api/changes/{Id(b)}", new
        {
            title = "Rejet B", changeType = "Standard", risk = "Faible", status = "Rejeté",
            plannedStart = Day.AddHours(11), plannedEnd = Day.AddHours(13)
        }));
        Assert.Empty(await ConflictsOf(c, a));
    }
}
