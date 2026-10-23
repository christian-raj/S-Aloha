using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Links;
using SAloha.Api.Core.Notifications;

namespace SAloha.Api.Tests;

/// <summary>Indicateurs des incidents, problèmes et changements au tableau de bord (INC-31, INC-33, PRB-33 à PRB-35, CHG-33 à CHG-35).</summary>
[Collection("api")]
public class IndicatorsLot3Tests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();
    private readonly string _svc = $"Indic {Guid.NewGuid():N}"[..16];
    private HttpClient Admin => api.Client("Admin", "admin.indic");

    [Fact]
    public async Task INC_31_33_PRB_33_35_prise_en_charge_reouverture_cause_racine_et_recurrences()
    {
        var ids = new List<int>();
        foreach (var t in new[] { "a", "b" })
            ids.Add(Id(await Json(await Admin.PostAsJsonAsync("/api/incidents", new { title = $"Indic {t}", impact = "Faible", urgency = "Faible", affectedService = _svc }))));
        var problems = new List<int>();
        foreach (var t in new[] { "avec cause", "sans cause" })
            problems.Add(Id(await Json(await Admin.PostAsJsonAsync("/api/problems", new { title = $"Problème {t}", description = "d", impact = "Moyen", urgency = "Moyenne", category = "", affectedService = _svc, source = "Proactif" }))));
        var now = DateTime.UtcNow;
        await using (var db = api.Database())
        {
            var incidents = await db.Incidents.Where(i => ids.Contains(i.Id)).OrderBy(i => i.Id).ToListAsync();
            foreach (var i in incidents) { i.CreatedAt = now.AddHours(-10); i.FirstResponseAt = now.AddHours(-8); i.Status = "Résolu"; i.ResolvedAt = now.AddHours(-1); }
            incidents[0].ReopenCount = 1;
            var ps = await db.Problems.Where(p => problems.Contains(p.Id)).OrderBy(p => p.Id).ToListAsync();
            foreach (var p in ps) p.Status = "Résolu";
            ps[0].RootCause = "Certificat expiré";
            db.ItemLinks.AddRange(ids.Select(i => new ItemLink { FromType = "incident", FromId = i, ToType = "problem", ToId = problems[0], CreatedBy = "admin.indic" }));
            db.Notifications.Add(new Notification { RecipientType = Notification.ToManagers, PerimeterId = ps[0].PerimeterId, TargetType = "problem", TargetId = problems[0],
                Reference = ps[0].Reference, Title = ps[0].Title, Event = "Récurrence après correctif : INC-x", Actor = "système", ActorDisplayName = "S-Aloha" });
            await db.SaveChangesAsync();
        }
        var q = $"service={Uri.EscapeDataString(_svc)}";
        var inc = (await Json(await Admin.GetAsync($"/api/reports/dashboard/incidents?{q}"))).GetProperty("kpis");
        Assert.Equal((2.0, 50.0), (inc.GetProperty("responseHours").GetDouble(), inc.GetProperty("reopenRate").GetDouble()));
        var prb = await Json(await Admin.GetAsync($"/api/reports/dashboard/problems?{q}"));
        Assert.Equal((50.0, 1), (prb.GetProperty("kpis").GetProperty("rootCauseRate").GetDouble(), prb.GetProperty("kpis").GetProperty("recurrences").GetInt32()));
        var top = Assert.Single(prb.GetProperty("topProblems").EnumerateArray());
        Assert.Equal((problems[0], 2), (Id(top), top.GetProperty("incidents").GetInt32()));
    }

    [Fact]
    public async Task CHG_33_35_incident_apres_changement_delai_d_autorisation_et_respect_des_creneaux()
    {
        var perimeter = Id(await Json(await Admin.PostAsJsonAsync("/api/admin/perimeters", new { name = $"Indicateurs {Guid.NewGuid():N}"[..24] })));
        var slot = DateTime.UtcNow.Date.AddDays(-3).AddHours(20);
        var changes = new List<int>();
        foreach (var t in new[] { "dans le créneau", "hors créneau" })
            changes.Add(Id(await Json(await Admin.PostAsJsonAsync("/api/changes", new
            {
                title = $"Changement {t}", changeType = "Normal", risk = "Faible", perimeterId = perimeter,
                plannedStart = slot, plannedEnd = slot.AddHours(2), implementationPlan = "Appliquer", backoutPlan = "Restaurer"
            }))));
        var incident = Id(await Json(await Admin.PostAsJsonAsync("/api/incidents", new { title = "Panne après changement", impact = "Faible", urgency = "Faible", perimeterId = perimeter })));
        await using (var db = api.Database())
        {
            var list = await db.Changes.Where(c => changes.Contains(c.Id)).OrderBy(c => c.Id).ToListAsync();
            foreach (var c in list) { c.Status = "Mis en œuvre"; c.CreatedAt = slot.AddDays(-2); c.AuthorizedAt = slot.AddDays(-2).AddHours(6); c.ImplementedAt = slot.AddHours(2); c.ActualStart = slot; }
            list[0].ActualEnd = slot.AddHours(1);
            list[1].ActualEnd = slot.AddHours(5);
            (await db.Incidents.SingleAsync(i => i.Id == incident)).CreatedAt = slot.AddDays(1);
            db.ItemLinks.Add(new ItemLink { FromType = "incident", FromId = incident, ToType = "change", ToId = changes[1], CreatedBy = "admin.indic" });
            await db.SaveChangesAsync();
        }
        var k = (await Json(await Admin.GetAsync($"/api/reports/dashboard/changes-projects?perimeter={perimeter}"))).GetProperty("kpis");
        Assert.Equal((50.0, 1), (k.GetProperty("incidentAfterChange").GetDouble(), k.GetProperty("incidentAfterChangeCount").GetInt32()));
        Assert.Equal(6.0, k.GetProperty("authorizationHours").GetDouble());
        Assert.Equal((50.0, 2), (k.GetProperty("slotRespect").GetDouble(), k.GetProperty("slotMeasured").GetInt32()));
    }
}
