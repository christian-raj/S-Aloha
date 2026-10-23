using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Links;
using SAloha.Api.Modules.ComplianceAssessment;

namespace SAloha.Api.Tests;

/// <summary>
/// Indicateurs de configuration, connaissances, amélioration, niveaux de service, NIS 2 et
/// déclarations (CFG-32 à 34, KB-30 à 32, CSI-30 à 32, SLM-31, NIS-30, NIS-31, DRG-21, DRG-22),
/// mesurés dans un périmètre propre au test.
/// </summary>
[Collection("api")]
public class IndicatorsLot3bTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();
    private HttpClient Admin => api.Client("Admin", "admin.indic2");
    private async Task<int> Perimeter() => Id(await Json(await Admin.PostAsJsonAsync("/api/admin/perimeters", new { name = $"Indic2 {Guid.NewGuid():N}"[..20] })));

    [Fact]
    public async Task CFG_32_34_exactitude_ci_impliques_et_ci_orphelins()
    {
        var p = await Perimeter();
        object Ci(string t, string status, string? owner) => new { title = $"{t}-{p}", ciType = "Autre", environment = "Production", status, perimeterId = p,
            ownerType = owner is null ? null : "User", ownerId = owner, ownerDisplayName = owner };
        var a = await Json(await Admin.PostAsJsonAsync("/api/configuration-items", Ci("ci-a", "En service", "own.a")));
        var b = await Json(await Admin.PostAsJsonAsync("/api/configuration-items", Ci("ci-b", "En service", "own.b")));
        await Json(await Admin.PostAsJsonAsync("/api/configuration-items", Ci("ci-orphelin", "Planifié", null)));
        await Json(await Admin.PostAsync($"/api/configuration-items/{Id(a)}/verify", null));
        var inc = await Json(await Admin.PostAsJsonAsync("/api/incidents", new { title = "Panne ci-a", impact = "Faible", urgency = "Faible", perimeterId = p }));
        await using (var db = api.Database())
        {
            db.ItemLinks.Add(new ItemLink { FromType = "incident", FromId = Id(inc), ToType = "ci", ToId = Id(a), CreatedBy = "admin" });
            (await db.ConfigurationItems.SingleAsync(c => c.Id == Id(b))).LastVerifiedAt = DateTime.UtcNow.AddMonths(-14);
            await db.SaveChangesAsync();
        }
        var tab = await Json(await Admin.GetAsync($"/api/reports/dashboard/configuration?perimeter={p}"));
        Assert.Equal((50.0, 2, 1), (tab.GetProperty("kpis").GetProperty("accuracy").GetDouble(), tab.GetProperty("kpis").GetProperty("accuracyMeasured").GetInt32(),
            tab.GetProperty("kpis").GetProperty("orphans").GetInt32()));
        var top = Assert.Single(tab.GetProperty("topIncidentCis").EnumerateArray());
        Assert.Equal((Id(a), 1), (Id(top), top.GetProperty("incidents").GetInt32()));
    }

    [Fact]
    public async Task KB_CSI_DRG_SLM_NIS_indicateurs_de_conformite()
    {
        var p = await Perimeter();
        var now = DateTime.UtcNow;
        // KB-30 à KB-32 : un article publié, relié à l'un des deux incidents résolus.
        var art = await Json(await api.Client("Admin", "redac.indic2").PostAsJsonAsync("/api/knowledge", new { title = "Redémarrer le spouleur", articleType = "Procédure", content = "c", perimeterId = p }));
        await Json(await Admin.PutAsJsonAsync($"/api/knowledge/{Id(art)}", new { title = "Redémarrer le spouleur", articleType = "Procédure", content = "c", status = "Publié" }));
        var incs = new List<int>();
        foreach (var t in new[] { "avec article", "sans article" })
            incs.Add(Id(await Json(await Admin.PostAsJsonAsync("/api/incidents", new { title = $"Impression {t}", impact = "Faible", urgency = "Faible", perimeterId = p }))));
        // CSI : une réalisée en 10 jours (source Incident), une abandonnée.
        var imps = new List<int>();
        foreach (var t in new[] { "réalisée", "abandonnée" })
            imps.Add(Id(await Json(await Admin.PostAsJsonAsync("/api/improvements", new { title = $"Amélioration {t}", step = 1, priority = "Moyenne", source = "Incident", perimeterId = p }))));
        // DRG : incident cyber qualifié 3 h après la prise de connaissance, notification envoyée.
        var cyber = await Json(await Admin.PostAsJsonAsync("/api/incidents", new { title = "Hameçonnage", impact = "Élevé", urgency = "Élevée", isSecurity = true, perimeterId = p }));
        await Json(await Admin.PostAsJsonAsync($"/api/incidents/{Id(cyber)}/qualification", new { nis2Important = true, personalDataBreach = false, personalDataBreachReason = "Aucune donnée" }));
        // SLM-31 : SLA du périmètre avec une cible de disponibilité, un incident majeur de 2 h.
        var svc = Id(await Json(await Admin.PostAsJsonAsync("/api/services", new { title = $"Svc indic2 {p}", status = "En service", criticality = "Moyenne", perimeterId = p, ownerType = "User", ownerId = "o", ownerDisplayName = "O" })));
        object Sla(string? status) => new { title = "SLA indic2", status, serviceId = svc, customer = "c", resolutionHoursP1 = 4, resolutionHoursP2 = 8, resolutionHoursP3 = 24, resolutionHoursP4 = 48,
            validFrom = now.Date.AddMonths(-2), availabilityTarget = 99.99, perimeterId = p };
        var sla = await Json(await Admin.PostAsJsonAsync("/api/agreements", Sla(null)));
        await Json(await Admin.PutAsJsonAsync($"/api/agreements/{Id(sla)}", Sla("En vigueur")));
        var major = Id(await Json(await Admin.PostAsJsonAsync("/api/incidents", new { title = "Arrêt du service", impact = "Faible", urgency = "Faible", serviceId = svc, perimeterId = p })));
        // NIS-30, NIS-31 : deux évaluations validées ; la seconde a deux écarts, dont un traité.
        var prev = Id(await Json(await Admin.PostAsJsonAsync("/api/assessments", new { title = "NIS2 2025", description = "d", entityCategory = Assessment.Important, perimeterId = p })));
        var last = Id(await Json(await Admin.PostAsJsonAsync("/api/assessments", new { title = "NIS2 2026", description = "d", entityCategory = Assessment.Important, perimeterId = p })));
        string gapCode;
        await using (var db = api.Database())
        {
            var reqs = await AssessmentReferential.CurrentAsync(db, await db.Assessments.SingleAsync(a => a.Id == last));
            foreach (var (id, gaps) in new[] { (prev, 0), (last, 2) })
                db.AssessmentResponses.AddRange(reqs.Select((r, i) => new AssessmentResponse { AssessmentId = id, RequirementId = r.Id, Score = i < gaps ? 1 : 3, UpdatedBy = "admin" }));
            gapCode = reqs[0].Code;
            foreach (var i in await db.Incidents.Where(x => incs.Contains(x.Id)).ToListAsync()) { i.Status = "Résolu"; i.ResolvedAt = now.AddHours(-1); }
            db.ItemLinks.Add(new ItemLink { FromType = "incident", FromId = incs[0], ToType = "article", ToId = Id(art), CreatedBy = "admin" });
            var im = await db.Improvements.Where(x => imps.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync();
            (im[0].Status, im[0].ValidatedAt, im[0].CompletedAt) = ("Réalisée", now.AddDays(-11), now.AddDays(-1));
            im[1].Status = "Abandonnée";
            var d = await db.RegulatoryNotifications.SingleAsync(x => x.IncidentId == Id(cyber));
            (d.AwareAt, d.NotificationSentAt) = (now.AddHours(-5), now.AddHours(-1));
            (await db.Incidents.SingleAsync(x => x.Id == Id(cyber))).QualifiedAt = now.AddHours(-2);
            var m = await db.Incidents.SingleAsync(x => x.Id == major);
            (m.IsMajor, m.CreatedAt, m.ResolvedAt, m.Status) = (true, now.AddDays(-2), now.AddDays(-2).AddHours(2), "Résolu");
            await db.SaveChangesAsync();
        }
        await Json(await Admin.PostAsJsonAsync($"/api/assessments/{last}/improvements", new { code = gapCode }));
        await using (var db = api.Database())
        {
            foreach (var (id, at) in new[] { (prev, now.AddDays(-300)), (last, now.AddDays(-1)) })
            {
                var a = await db.Assessments.SingleAsync(x => x.Id == id);
                (a.Status, a.ValidatedAt) = ("Validée", at);
            }
            await db.SaveChangesAsync();
        }

        var c = await Json(await Admin.GetAsync($"/api/reports/dashboard/compliance?perimeter={p}"));
        var kb = c.GetProperty("knowledge");
        // Trois incidents résolus sur la période (dont le majeur de la disponibilité), un relié à l'article.
        Assert.Equal((100.0, 33.3), (kb.GetProperty("upToDate").GetDouble(), kb.GetProperty("incidentsWithArticle").GetDouble()));
        Assert.Equal(Id(art), Id(Assert.Single(kb.GetProperty("topUsed").EnumerateArray())));
        var csi = c.GetProperty("improvements");
        Assert.Equal((10.0, 50.0), (csi.GetProperty("completionDays").GetDouble(), csi.GetProperty("abandonRate").GetDouble()));
        Assert.Equal("Incident", Assert.Single(csi.GetProperty("completedBySource").EnumerateArray()).GetProperty("label").GetString());
        Assert.Equal(3.0, c.GetProperty("qualificationHours").GetDouble());
        Assert.Equal(1, c.GetProperty("regulatory").EnumerateArray().Single(r => r.GetProperty("regime").GetString() == "NIS 2").GetProperty("declared").GetInt32());
        var avail = Assert.Single(c.GetProperty("availability").EnumerateArray());
        Assert.False(avail.GetProperty("met").GetBoolean());
        var nis2 = c.GetProperty("nis2");
        Assert.Equal((2, 1, 50.0), (nis2.GetProperty("gaps").GetInt32(), nis2.GetProperty("gapsTreated").GetInt32(), nis2.GetProperty("gapsTreatedRate").GetDouble()));
        Assert.Equal(100.0, nis2.GetProperty("previous").GetProperty("percent").GetDouble());
        Assert.True(nis2.GetProperty("delta").GetDouble() < 0);
    }
}
