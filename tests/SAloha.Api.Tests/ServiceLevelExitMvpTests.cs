using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SAloha.Api.Tests;

/// <summary>Niveaux de service : expiration (SLM-04), délais de prise en charge (SLM-13), revue de SLA (SLM-22).</summary>
[Collection("api")]
public class ServiceLevelExitMvpTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return body == "" ? default : JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();

    private async Task<int> Service(HttpClient m, string name) =>
        Id(await Json(await m.PostAsJsonAsync("/api/services", new
        {
            title = name, status = "En service", criticality = "Moyenne", ownerType = "User", ownerId = "svc.owner", ownerDisplayName = "Owner"
        })));

    private static object Sla(int serviceId, string customer, string? status, int?[]? response = null, DateTime? validTo = null) => new
    {
        title = $"SLA {customer}", status, serviceId, customer, resolutionHoursP1 = 4, resolutionHoursP2 = 8,
        resolutionHoursP3 = 24, resolutionHoursP4 = 48, validFrom = DateTime.UtcNow.Date.AddMonths(-6), validTo,
        responseHoursP1 = response?[0], responseHoursP2 = response?[1], responseHoursP3 = response?[2], responseHoursP4 = response?[3]
    };

    [Fact]
    public async Task SLM_13_delais_de_prise_en_charge_et_echeance_de_l_incident()
    {
        var m = api.Client("Manager", "slm13.manager");
        var svc = await Service(m, "Service SLM-13");
        var sla = await Json(await m.PostAsJsonAsync("/api/agreements", Sla(svc, "SLM-13", null)));
        var url = $"/api/agreements/{Id(sla)}";
        // Incomplets, décroissants, ou au-delà du délai de résolution : refusés à la mise en vigueur.
        Assert.Equal(HttpStatusCode.BadRequest, (await m.PutAsJsonAsync(url, Sla(svc, "SLM-13", "En vigueur", [1, null, null, null]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await m.PutAsJsonAsync(url, Sla(svc, "SLM-13", "En vigueur", [2, 1, 4, 8]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await m.PutAsJsonAsync(url, Sla(svc, "SLM-13", "En vigueur", [5, 6, 8, 8]))).StatusCode);
        await Json(await m.PutAsJsonAsync(url, Sla(svc, "SLM-13", "En vigueur", [1, 2, 4, 8])));

        // Incident P4 sur ce service : prise en charge sous 8 h, résolution sous 48 h.
        var inc = await Json(await m.PostAsJsonAsync("/api/incidents", new { title = "SLM-13", impact = "Faible", urgency = "Faible", affectedService = "service slm-13" }));
        var created = inc.GetProperty("createdAt").GetDateTime();
        Assert.Equal(created.AddHours(8), inc.GetProperty("responseDueAt").GetDateTime(), TimeSpan.FromSeconds(1));
        Assert.Equal(created.AddHours(48), inc.GetProperty("resolutionDueAt").GetDateTime(), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SLM_04_un_SLA_dont_la_validite_est_passee_expire()
    {
        var m = api.Client("Manager", "slm04.manager");
        var svc = await Service(m, "Service SLM-04");
        var sla = await Json(await m.PostAsJsonAsync("/api/agreements", Sla(svc, "SLM-04", null)));
        await Json(await m.PutAsJsonAsync($"/api/agreements/{Id(sla)}", Sla(svc, "SLM-04", "En vigueur", validTo: DateTime.UtcNow.Date.AddDays(10))));
        // Fin de validité hier : la règle de cohérence de l'API refuserait une date passée à la mise en vigueur.
        await using (var db = api.Database())
        {
            var a = await db.Agreements.FirstAsync(x => x.Id == Id(sla));
            a.ValidTo = DateTime.UtcNow.Date.AddDays(-1);
            await db.SaveChangesAsync();
        }
        await Json(await api.Client().PostAsync("/api/notifications/run", null));
        var after = await Json(await m.GetAsync($"/api/agreements/{Id(sla)}"));
        Assert.Equal("Expiré", after.GetProperty("status").GetString());
        var events = (await Json(await api.Client("Manager", "slm04.autre").GetAsync("/api/notifications"))).GetProperty("items").EnumerateArray()
            .Where(n => n.GetProperty("reference").GetString() == sla.GetProperty("reference").GetString())
            .Select(n => n.GetProperty("event").GetString()).ToList();
        Assert.Contains(events, e => e!.StartsWith("SLA expiré"));
        // Le journal d'audit garde le passage à Expiré.
        var history = await Json(await m.GetAsync($"/api/audit?type=agreement&id={Id(sla)}"));
        Assert.Contains(history.EnumerateArray(), h => h.GetProperty("field").GetString() == "status" && h.GetProperty("newValue").GetString() == "Expiré");
    }

    [Fact]
    public async Task SLM_22_revue_du_SLA_reporte_la_prochaine_revue()
    {
        var m = api.Client("Manager", "slm22.manager");
        var svc = await Service(m, "Service SLM-22");
        var sla = await Json(await m.PostAsJsonAsync("/api/agreements", Sla(svc, "SLM-22", null)));
        var url = $"/api/agreements/{Id(sla)}/reviews";

        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client("User", "slm22.user").PostAsJsonAsync(url, new { findings = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await m.PostAsJsonAsync(url, new { findings = "", decisions = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await m.PostAsJsonAsync(url, new { findings = "x", nextReviewDate = DateTime.UtcNow.Date })).StatusCode);

        var review = await Json(await m.PostAsJsonAsync(url, new { findings = "Délais P3 tenus à 92 %", decisions = "Renforcer l'astreinte" }));
        Assert.Equal(DateTime.UtcNow.Date.AddMonths(12), review.GetProperty("nextReviewDate").GetDateTime().Date);
        await Json(await m.PostAsJsonAsync(url, new { decisions = "Revue anticipée", nextReviewDate = DateTime.UtcNow.Date.AddMonths(3) }));

        var after = await Json(await m.GetAsync($"/api/agreements/{Id(sla)}"));
        Assert.Equal(DateTime.UtcNow.Date.AddMonths(3), after.GetProperty("reviewDate").GetDateTime().Date);
        var reviews = after.GetProperty("reviews").EnumerateArray().ToList();
        Assert.Equal(2, reviews.Count);
        Assert.Equal("Revue anticipée", reviews[0].GetProperty("decisions").GetString());   // la plus récente d'abord
    }
}
