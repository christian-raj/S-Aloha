using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SAloha.Api.Tests;

/// <summary>Amélioration continue : source (CSI-10), priorisation (CSI-12), retard (CSI-21), récurrence (CSI-20, PRB-23).</summary>
[Collection("api")]
public class ImprovementLifecycleTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();
    private static string Ref(JsonElement e) => e.GetProperty("reference").GetString()!;

    private static object Ami(string title, string? status = null, string? source = null, int? value = null, int? effort = null,
        string? origin = null, DateTime? due = null, string? owner = null) => new
    {
        title, step = 2, priority = "Moyenne", status, benefit = "b", baseline = "5 j", target = "2 j",
        source, value, effort, originReference = origin, dueDate = due,
        ownerType = owner is null ? null : "User", ownerId = owner, ownerDisplayName = owner
    };

    [Fact]
    public async Task CSI_10_source_et_enregistrement_d_origine()
    {
        var u = api.Client("User", "proposant.csi10");
        var inc = await Json(await u.PostAsJsonAsync("/api/incidents", new { title = "Lenteur VPN", impact = "Faible", urgency = "Faible" }));
        var a = await Json(await u.PostAsJsonAsync("/api/improvements", Ami("Tunnel VPN", source: "Incident", origin: Ref(inc).ToLower())));
        Assert.Equal("Incident", a.GetProperty("source").GetString());
        var links = await Json(await u.GetAsync($"/api/links?type=improvement&id={Id(a)}"));
        Assert.Contains(links.EnumerateArray(), l => Ref(l) == Ref(inc));
        // Par défaut, une suggestion ; source inconnue et origine introuvable refusées.
        Assert.Equal("Suggestion", (await Json(await u.PostAsJsonAsync("/api/improvements", Ami("Idée")))).GetProperty("source").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await u.PostAsJsonAsync("/api/improvements", Ami("x", source: "Rumeur"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await u.PostAsJsonAsync("/api/improvements", Ami("x", origin: "INC-1999-9999"))).StatusCode);
    }

    [Fact]
    public async Task CSI_12_valeur_et_effort_a_la_validation_priorite_calculee()
    {
        var m = api.Client("Manager", "chef.csi12");
        var a = await Json(await m.PostAsJsonAsync("/api/improvements", Ami("Automatiser les comptes")));
        var url = $"/api/improvements/{Id(a)}";
        Assert.Contains("valeur et l'effort", await (await m.PutAsJsonAsync(url, Ami("Automatiser les comptes", "Validée"))).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await m.PutAsJsonAsync(url, Ami("Automatiser les comptes", "Validée", value: 4, effort: 1))).StatusCode);
        var v = await Json(await m.PutAsJsonAsync(url, Ami("Automatiser les comptes", "Validée", value: 3, effort: 1)));
        Assert.Equal("Élevée", v.GetProperty("priority").GetString());
        var low = await Json(await m.PutAsJsonAsync(url, Ami("Automatiser les comptes", "Validée", value: 1, effort: 3)));
        Assert.Equal("Faible", low.GetProperty("priority").GetString());
        var mid = await Json(await m.PutAsJsonAsync(url, Ami("Automatiser les comptes", "Validée", value: 2, effort: 2)));
        Assert.Equal("Moyenne", mid.GetProperty("priority").GetString());

        // Validée avant la règle (sans note) : elle reste modifiable.
        await using (var db = api.Database())
        {
            var e = await db.Improvements.FirstAsync(x => x.Id == Id(a));
            e.Value = null; e.Effort = null;
            await db.SaveChangesAsync();
        }
        await Json(await m.PutAsJsonAsync(url, Ami("Automatiser les comptes (retitrée)", "Validée")));
    }

    [Fact]
    public async Task CSI_21_amelioration_en_retard_relancee_au_porteur()
    {
        var m = api.Client("Manager", "chef.csi21");
        var a = await Json(await m.PostAsJsonAsync("/api/improvements", Ami("Supervision", owner: "porteur.csi21")));
        await Json(await m.PutAsJsonAsync($"/api/improvements/{Id(a)}", Ami("Supervision", "Validée", value: 2, effort: 2, owner: "porteur.csi21")));
        await Json(await m.PutAsJsonAsync($"/api/improvements/{Id(a)}", Ami("Supervision", "En cours", value: 2, effort: 2, owner: "porteur.csi21",
            due: DateTime.UtcNow.Date.AddDays(-3))));
        await Json(await api.Client().PostAsync("/api/notifications/run", null));
        var events = (await Json(await api.Client("User", "porteur.csi21").GetAsync("/api/notifications"))).GetProperty("items").EnumerateArray()
            .Where(n => n.GetProperty("reference").GetString() == Ref(a)).Select(n => n.GetProperty("event").GetString()).ToList();
        Assert.Contains(events, e => e!.StartsWith("Amélioration en retard"));
    }

    [Fact]
    public async Task PRB_23_CSI_20_recurrence_apres_correctif()
    {
        var m = api.Client("Manager", "chef.prb23");
        var p = await Json(await m.PostAsJsonAsync("/api/problems", new
        {
            title = "Saturation disque", description = "d", impact = "Moyen", urgency = "Moyenne", category = "c", affectedService = "s"
        }));
        await using (var db = api.Database())
        {
            (await db.Problems.FirstAsync(x => x.Id == Id(p))).Status = "Résolu";
            await db.SaveChangesAsync();
        }
        var inc = await Json(await m.PostAsJsonAsync("/api/incidents", new { title = "Disque plein à nouveau", impact = "Faible", urgency = "Faible" }));
        await Json(await m.PostAsJsonAsync("/api/links", new { fromType = "incident", fromId = Id(inc), toReference = Ref(p) }));

        // Une amélioration est proposée, reliée au problème ; les gestionnaires sont alertés.
        var links = await Json(await m.GetAsync($"/api/links?type=problem&id={Id(p)}"));
        var ami = links.EnumerateArray().Single(l => l.GetProperty("type").GetString() == "improvement");
        var improvement = await Json(await m.GetAsync($"/api/improvements/{Id(ami)}"));
        Assert.Equal("Problème", improvement.GetProperty("source").GetString());
        Assert.Equal("Proposée", improvement.GetProperty("status").GetString());
        var other = api.Client("Manager", "autre.chef.prb23");
        var events = (await Json(await other.GetAsync("/api/notifications"))).GetProperty("items").EnumerateArray()
            .Select(n => (Ref: n.GetProperty("reference").GetString(), Event: n.GetProperty("event").GetString())).ToList();
        Assert.Contains((Ref(p), $"Récurrence après correctif : {Ref(inc)}"), events);
        Assert.Contains((Ref(improvement), "Amélioration proposée"), events);

        // Un second incident ne propose pas une seconde amélioration (une ouverte suffit).
        var inc2 = await Json(await m.PostAsJsonAsync("/api/incidents", new { title = "Encore plein", impact = "Faible", urgency = "Faible" }));
        await Json(await m.PostAsJsonAsync("/api/links", new { fromType = "incident", fromId = Id(inc2), toReference = Ref(p) }));
        Assert.Single((await Json(await m.GetAsync($"/api/links?type=problem&id={Id(p)}"))).EnumerateArray(),
            l => l.GetProperty("type").GetString() == "improvement");
    }
}
