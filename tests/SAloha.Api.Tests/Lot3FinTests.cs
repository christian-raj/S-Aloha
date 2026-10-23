using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SAloha.Api.Tests;

/// <summary>Fin du lot 3 : disponibilité mesurée (SLM-21), durabilité (CSI-15), modèle de contenu des déclarations (DRG-08).</summary>
[Collection("api")]
public class Lot3FinTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();
    private static string Str(JsonElement e, string p) => e.GetProperty(p).GetString()!;

    [Fact]
    public async Task SLM_21_disponibilite_mesuree_sur_les_incidents_majeurs_et_alerte_sous_la_cible()
    {
        var m = api.Client("Manager", "slm21.manager");
        var svc = Id(await Json(await m.PostAsJsonAsync("/api/services", new
        {
            title = $"Service SLM-21 {Guid.NewGuid():N}"[..28], status = "En service", criticality = "Élevée", ownerType = "User", ownerId = "svc.owner", ownerDisplayName = "Owner"
        })));
        object Sla(string? status) => new
        {
            title = "SLA SLM-21", status, serviceId = svc, customer = "SLM-21", resolutionHoursP1 = 4, resolutionHoursP2 = 8,
            resolutionHoursP3 = 24, resolutionHoursP4 = 48, validFrom = DateTime.UtcNow.Date.AddMonths(-6), availabilityTarget = 99.5
        };
        var sla = await Json(await m.PostAsJsonAsync("/api/agreements", Sla(null)));
        await Json(await m.PutAsJsonAsync($"/api/agreements/{Id(sla)}", Sla("En vigueur")));

        // Le mois dernier : deux incidents majeurs qui se chevauchent (10 h au total), un incident non majeur ignoré.
        var now = DateTime.UtcNow;
        var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
        var ids = new List<int>();
        foreach (var t in new[] { "majeur A", "majeur B", "mineur" })
            ids.Add(Id(await Json(await m.PostAsJsonAsync("/api/incidents", new { title = $"SLM-21 {t}", impact = "Faible", urgency = "Faible", serviceId = svc }))));
        await using (var db = api.Database())
        {
            var list = await db.Incidents.Where(i => ids.Contains(i.Id)).OrderBy(i => i.Id).ToListAsync();
            (list[0].IsMajor, list[0].CreatedAt, list[0].ResolvedAt, list[0].Status) = (true, month.AddDays(3), month.AddDays(3).AddHours(8), "Résolu");
            (list[1].IsMajor, list[1].CreatedAt, list[1].ResolvedAt, list[1].Status) = (true, month.AddDays(3).AddHours(6), month.AddDays(3).AddHours(10), "Résolu");
            (list[2].CreatedAt, list[2].ResolvedAt, list[2].Status) = (month.AddDays(5), month.AddDays(6), "Résolu");
            await db.SaveChangesAsync();
        }
        var c = await Json(await m.GetAsync($"/api/agreements/{Id(sla)}/compliance?from={month:yyyy-MM}&to={month:yyyy-MM}"));
        Assert.Equal(99.5, c.GetProperty("availabilityTarget").GetDouble());
        var a = Assert.Single(c.GetProperty("months").EnumerateArray()).GetProperty("availability");
        var hours = (month.AddMonths(1) - month).TotalHours;
        Assert.Equal((10, 2, hours), (a.GetProperty("downtimeHours").GetDouble(), a.GetProperty("majorIncidents").GetInt32(), a.GetProperty("serviceHours").GetDouble()));
        Assert.Equal(Math.Round(100 - 10 * 100 / hours, 2), a.GetProperty("percent").GetDouble());
        Assert.False(a.GetProperty("met").GetBoolean());

        await Json(await api.Client().PostAsync("/api/notifications/run", null));
        await Json(await api.Client().PostAsync("/api/notifications/run", null));
        await using var check = api.Database();
        Assert.Equal(1, await check.Notifications.CountAsync(n => n.TargetType == "agreement" && n.TargetId == Id(sla) && n.Event.StartsWith($"Disponibilité sous la cible ({month:yyyy-MM}")));
        var linked = await check.ItemLinks.Where(l => l.FromType == "improvement" && l.ToType == "agreement" && l.ToId == Id(sla)).Select(l => l.FromId).ToListAsync();
        Assert.Contains(await check.Improvements.Where(i => linked.Contains(i.Id)).Select(i => i.Title).ToListAsync(), t => t.Contains("disponibilité sous la cible"));
    }

    [Fact]
    public async Task CSI_15_controle_de_durabilite_confirme_ou_nouvelle_amelioration_reliee()
    {
        var m = api.Client("Manager", "chef.csi15");
        var owner = api.Client("User", "porteur.csi15");
        object Imp(string title, string? status, DateTime? followUp) => new
        {
            title, status, step = 6, priority = "Moyenne", benefit = "b", baseline = "20 min", target = "5 min", outcome = status is "Réalisée" ? "6 min" : null,
            value = 2, effort = 2, followUpDate = followUp, ownerType = "User", ownerId = "porteur.csi15", ownerDisplayName = "Porteur"
        };
        async Task<JsonElement> Done(string title, DateTime followUp)
        {
            var x = await Json(await m.PostAsJsonAsync("/api/improvements", Imp(title, null, followUp)));
            foreach (var st in new[] { "Validée", "En cours", "Réalisée" }) await Json(await m.PutAsJsonAsync($"/api/improvements/{Id(x)}", Imp(title, st, followUp)));
            return x;
        }
        var later = await Done("Accueil plus rapide", DateTime.UtcNow.Date.AddDays(10));
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/improvements/{Id(later)}/follow-up", new { holds = true })).StatusCode);   // pas encore la date

        var due = await Done("Moins de tickets en double", DateTime.UtcNow.Date);
        var url = $"/api/improvements/{Id(due)}/follow-up";
        var console = await Json(await owner.GetAsync("/api/console"));
        Assert.Contains(console.GetProperty("myFollowUpChecks").EnumerateArray(), x => Id(x) == Id(due));
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client("User", "tiers.csi15").PostAsJsonAsync(url, new { holds = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(url, new { holds = false })).StatusCode);   // constat exigé
        var res = await Json(await owner.PostAsJsonAsync(url, new { holds = false, note = "Les doublons sont revenus depuis la migration" }));
        var created = Str(res, "created");
        Assert.False(res.GetProperty("improvement").GetProperty("followUpHolds").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(url, new { holds = true })).StatusCode);   // déjà conclu
        await using var db = api.Database();
        var next = await db.Improvements.SingleAsync(i => i.Reference == created);
        Assert.Equal(("Proposée", "porteur.csi15"), (next.Status, next.OwnerId));
        Assert.True(await db.ItemLinks.AnyAsync(l => l.FromType == "improvement" && l.FromId == next.Id && l.ToType == "improvement" && l.ToId == Id(due)));

        await Json(await api.Client().PostAsync("/api/notifications/run", null));
        Assert.False(await db.Notifications.AnyAsync(n => n.TargetType == "improvement" && n.TargetId == Id(due) && n.Event.StartsWith("Contrôle de durabilité")));   // conclu
    }

    [Fact]
    public async Task DRG_08_rubriques_attendues_par_regime()
    {
        var m = api.Client("Manager", "rssi.drg08");
        var inc = await Json(await m.PostAsJsonAsync("/api/incidents", new
        {
            title = "Rançongiciel sur la messagerie", description = "d", impact = "Élevé", urgency = "Élevée", category = "", affectedService = "", isSecurity = true
        }));
        await Json(await m.PostAsJsonAsync($"/api/incidents/{Id(inc)}/qualification", new { nis2Important = true, personalDataBreach = true }));
        var list = (await Json(await m.GetAsync($"/api/regulatory-notifications?incident={Id(inc)}"))).EnumerateArray().ToList();
        var nis2 = list.Single(d => Str(d, "regime") == "NIS 2");
        var gdpr = list.Single(d => Str(d, "regime") == "RGPD");
        Assert.Equal(new[] { "Malveillance supposée", "Effets transfrontières", "Gravité", "Impact", "Indicateurs de compromission" },
            nis2.GetProperty("missingRubrics").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("Conséquences probables", gdpr.GetProperty("missingRubrics").EnumerateArray().Select(x => x.GetString()));

        Dictionary<string, object?> Update(JsonElement d, Dictionary<string, object?> changes)
        {
            var dto = JsonSerializer.Deserialize<Dictionary<string, object?>>(d.GetRawText())!;
            foreach (var (k, v) in changes) dto[k] = v;
            return dto;
        }
        var url = $"/api/regulatory-notifications/{Id(nis2)}";
        Assert.Equal(HttpStatusCode.BadRequest, (await m.PutAsJsonAsync(url, Update(nis2, new() { ["severity"] = "Énorme" }))).StatusCode);
        var filled = await Json(await m.PutAsJsonAsync(url, Update(nis2, new()
        {
            ["suspectedMalicious"] = true, ["crossBorderImpact"] = false, ["severity"] = "Importante",
            ["impact"] = "Messagerie arrêtée 6 h", ["compromiseIndicators"] = "IP 203.0.113.7, hachage du binaire"
        })));
        Assert.Empty(filled.GetProperty("missingRubrics").EnumerateArray());
        Assert.Equal("Importante", Str(filled, "severity"));
    }
}
