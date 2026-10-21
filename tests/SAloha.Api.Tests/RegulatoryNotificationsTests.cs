using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SAloha.Api.Tests;

/// <summary>Incidents de sécurité et déclarations réglementaires : INC-40 à INC-44, DRG-01 à DRG-13.</summary>
[Collection("api")]
public class RegulatoryNotificationsTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();
    private static string Str(JsonElement e, string p) => e.GetProperty(p).GetString()!;
    private static async Task<string> Message(HttpResponseMessage res) => (await res.Content.ReadAsStringAsync());

    private static object Incident(string title, bool security = true, string status = "Nouveau") => new
    {
        title, description = "d", impact = "Élevé", urgency = "Élevée", category = "", affectedService = "", status, isSecurity = security
    };

    private HttpClient Manager => api.Client("Manager", "rssi.drg");
    private HttpClient User => api.Client("User", "agent.drg");

    private async Task<JsonElement> Qualify(HttpClient client, int incident, bool nis2, bool gdpr, DateTime? awareAt = null) =>
        await Json(await client.PostAsJsonAsync($"/api/incidents/{incident}/qualification", new
        {
            nis2Important = nis2, nis2Reason = nis2 ? null : "Pas d'impact significatif",
            personalDataBreach = gdpr, personalDataBreachReason = gdpr ? null : "Aucune donnée personnelle",
            awareAt
        }));

    private async Task<List<JsonElement>> DeclarationsOf(HttpClient client, int incident) =>
        (await Json(await client.GetAsync($"/api/regulatory-notifications?incident={incident}"))).EnumerateArray().ToList();

    private static object Update(JsonElement d, object changes)
    {
        var dto = JsonSerializer.Deserialize<Dictionary<string, object?>>(d.GetRawText())!;
        foreach (var p in changes.GetType().GetProperties()) dto[char.ToLowerInvariant(p.Name[0]) + p.Name[1..]] = p.GetValue(changes);
        return dto;
    }

    [Fact]
    public async Task INC_40_INC_41_INC_42_INC_44_qualification_par_un_gestionnaire_cree_les_declarations()
    {
        var incident = await Json(await User.PostAsJsonAsync("/api/incidents", Incident("Rançongiciel sur le serveur de fichiers")));
        Assert.True(incident.GetProperty("isSecurity").GetBoolean());   // INC-40 : tout rôle
        var id = Id(incident);
        // INC-41 : en attente de qualification, l'incident est dans l'Urgent des gestionnaires.
        var console = await Json(await Manager.GetAsync("/api/console"));
        Assert.Contains(console.GetProperty("management").GetProperty("regulatory").EnumerateArray(), r => Id(r) == id && Str(r, "status").StartsWith("À qualifier"));

        Assert.Equal(HttpStatusCode.Forbidden, (await User.PostAsJsonAsync($"/api/incidents/{id}/qualification",
            new { nis2Important = true, personalDataBreach = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Manager.PostAsJsonAsync($"/api/incidents/{id}/qualification",
            new { nis2Important = false, personalDataBreach = true })).StatusCode);   // « non » sans motif
        var plain = await Json(await User.PostAsJsonAsync("/api/incidents", Incident("Imprimante en panne", security: false)));
        Assert.Equal(HttpStatusCode.BadRequest, (await Manager.PostAsJsonAsync($"/api/incidents/{Id(plain)}/qualification",
            new { nis2Important = true, personalDataBreach = true })).StatusCode);

        var result = await Qualify(Manager, id, nis2: true, gdpr: true);
        Assert.Equal(2, result.GetProperty("created").GetArrayLength());
        Assert.True(result.GetProperty("proposeMajor").GetBoolean());   // INC-44
        var declarations = await DeclarationsOf(User, id);
        Assert.Equal(new[] { "NIS 2", "RGPD" }, declarations.Select(d => Str(d, "regime")).Order());
        Assert.All(declarations, d => Assert.Equal(("À déclarer", Str(incident, "reference")), (Str(d, "status"), Str(d, "incidentReference"))));
        Assert.Equal(("ANSSI", "CNIL"), (Str(declarations.Single(d => Str(d, "regime") == "NIS 2"), "authority"),
            Str(declarations.Single(d => Str(d, "regime") == "RGPD"), "authority")));
        // DRG-01 : la déclaration est reliée à l'incident.
        var links = (await Json(await User.GetAsync($"/api/links?type=incident&id={id}"))).EnumerateArray().Select(l => Str(l, "reference"));
        Assert.Contains(Str(declarations[0], "reference"), links);
        // Qualifié : il le reste, et n'est plus à qualifier.
        var after = await Json(await User.GetAsync($"/api/incidents/{id}"));
        Assert.Equal(HttpStatusCode.BadRequest, (await User.PutAsJsonAsync($"/api/incidents/{id}", Update(after, new { isSecurity = false }))).StatusCode);
        console = await Json(await Manager.GetAsync("/api/console"));
        Assert.DoesNotContain(console.GetProperty("management").GetProperty("regulatory").EnumerateArray(), r => Str(r, "type") == "incident" && Id(r) == id);
        // DRG-03 : une seule déclaration par régime ; une requalification n'en recrée pas.
        Assert.Equal(HttpStatusCode.Conflict, (await Manager.PostAsJsonAsync("/api/regulatory-notifications",
            new { title = "NIS 2 bis", incidentId = id, regime = "NIS 2", perimeterId = incident.GetProperty("perimeterId").GetInt32() })).StatusCode);
        Assert.Empty((await Qualify(Manager, id, nis2: true, gdpr: true)).GetProperty("created").EnumerateArray());
    }

    [Fact]
    public async Task DRG_10_echeances_en_heures_calendaires()
    {
        var id = Id(await Json(await User.PostAsJsonAsync("/api/incidents", Incident("Compte administrateur compromis"))));
        var monday = new DateTime(2026, 10, 19, 8, 0, 0, DateTimeKind.Utc);   // lundi
        await Qualify(Manager, id, nis2: true, gdpr: true, awareAt: monday);
        var all = await DeclarationsOf(Manager, id);
        var nis2 = all.Single(d => Str(d, "regime") == "NIS 2");
        Assert.Equal(monday.AddDays(1), nis2.GetProperty("earlyWarningDueAt").GetDateTime());   // mardi, même heure
        Assert.Equal(monday.AddDays(3), nis2.GetProperty("notificationDueAt").GetDateTime());   // jeudi
        Assert.Equal(monday.AddDays(3).AddMonths(1), nis2.GetProperty("finalReportDueAt").GetDateTime());
        var gdpr = all.Single(d => Str(d, "regime") == "RGPD");
        Assert.Equal((JsonValueKind.Null, monday.AddHours(72)), (gdpr.GetProperty("earlyWarningDueAt").ValueKind, gdpr.GetProperty("notificationDueAt").GetDateTime()));
        // Échéance dépassée : signalée sur la fiche (DRG-11).
        Assert.True(nis2.GetProperty("steps")[0].GetProperty("late").GetBoolean());
    }

    [Fact]
    public async Task DRG_05_DRG_06_DRG_07_NIS2_alerte_precoce_puis_notification_contenu_fige()
    {
        var id = Id(await Json(await User.PostAsJsonAsync("/api/incidents", Incident("Exfiltration suspectée"))));
        var aware = DateTime.UtcNow.AddHours(-2);
        await Qualify(Manager, id, nis2: true, gdpr: false, awareAt: aware);
        var d = (await DeclarationsOf(Manager, id)).Single();
        var did = Id(d);
        // DRG-06 : pas de notification sans alerte précoce.
        Assert.Equal(HttpStatusCode.BadRequest, (await Manager.PutAsJsonAsync($"/api/regulatory-notifications/{did}",
            Update(d, new { status = "Notification envoyée", notificationSentAt = DateTime.UtcNow, summary = "s", impact = "i", measuresTaken = "m" }))).StatusCode);
        // DRG-07 : tout rôle complète le contenu ; seul un gestionnaire enregistre un envoi.
        d = await Json(await User.PutAsJsonAsync($"/api/regulatory-notifications/{did}",
            Update(d, new { summary = "Chiffrement des partages", suspectedMalicious = true, crossBorderImpact = false })));
        Assert.Equal(HttpStatusCode.BadRequest, (await User.PutAsJsonAsync($"/api/regulatory-notifications/{did}",
            Update(d, new { earlyWarningSentAt = DateTime.UtcNow }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await User.PutAsJsonAsync($"/api/regulatory-notifications/{did}",
            Update(d, new { status = "Alerte précoce envoyée" }))).StatusCode);
        // DRG-05 : ni avant la prise de connaissance, ni dans le futur.
        Assert.Equal(HttpStatusCode.BadRequest, (await Manager.PutAsJsonAsync($"/api/regulatory-notifications/{did}",
            Update(d, new { status = "Alerte précoce envoyée", earlyWarningSentAt = aware.AddHours(-1) }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Manager.PutAsJsonAsync($"/api/regulatory-notifications/{did}",
            Update(d, new { status = "Alerte précoce envoyée", earlyWarningSentAt = DateTime.UtcNow.AddHours(2) }))).StatusCode);
        var sent = DateTime.UtcNow.AddMinutes(-10);
        d = await Json(await Manager.PutAsJsonAsync($"/api/regulatory-notifications/{did}",
            Update(d, new { status = "Alerte précoce envoyée", earlyWarningSentAt = sent, authorityCaseRef = "ANSSI-2026-042" })));
        var version = Assert.Single(d.GetProperty("sentVersions").EnumerateArray());
        Assert.Equal(("Alerte précoce", "Chiffrement des partages"), (Str(version, "step"), Str(version, "summary")));
        Assert.Equal("Notification", Str(d, "nextStep"));
        // INC-43 : l'incident ne se clôt pas tant que la déclaration est en cours.
        var incident = await Json(await Manager.GetAsync($"/api/incidents/{id}"));
        incident = await Json(await Manager.PutAsJsonAsync($"/api/incidents/{id}",
            Update(incident, new { status = "Résolu", resolution = "Restauré", resolutionCode = "Correctif appliqué" })));
        var close = await Manager.PutAsJsonAsync($"/api/incidents/{id}", Update(incident, new { status = "Clos" }));
        Assert.Equal(HttpStatusCode.BadRequest, close.StatusCode);
        Assert.Contains(Str(d, "reference"), await Message(close));
    }

    [Fact]
    public async Task DRG_04_DRG_12_RGPD_hors_delai_motive_puis_close()
    {
        var id = Id(await Json(await User.PostAsJsonAsync("/api/incidents", Incident("Base clients exposée"))));
        await Qualify(Manager, id, nis2: false, gdpr: true, awareAt: DateTime.UtcNow.AddHours(-80));
        var d = (await DeclarationsOf(Manager, id)).Single();
        var content = new { summary = "Base exposée", impact = "Clients", measuresTaken = "Accès coupé",
            personalDataCategories = "Identité, coordonnées", dataSubjectsCount = 1200 };
        d = await Json(await User.PutAsJsonAsync($"/api/regulatory-notifications/{Id(d)}", Update(d, content)));
        // DRG-12 : après 72 h, la notification RGPD exige les motifs du retard.
        var late = await Manager.PutAsJsonAsync($"/api/regulatory-notifications/{Id(d)}",
            Update(d, new { status = "Notification envoyée", notificationSentAt = DateTime.UtcNow }));
        Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);
        Assert.Contains("retard", await Message(late));
        d = await Json(await Manager.PutAsJsonAsync($"/api/regulatory-notifications/{Id(d)}",
            Update(d, new { status = "Notification envoyée", notificationSentAt = DateTime.UtcNow, lateReason = "Périmètre établi au 3e jour" })));
        Assert.True(d.GetProperty("steps")[0].GetProperty("late").GetBoolean());
        // REP-44, DRG-20 : la notification hors délai compte dans le respect des délais.
        var report = await Json(await Manager.GetAsync("/api/reports/dashboard/compliance"));
        var gdprStats = report.GetProperty("regulatory").EnumerateArray().Single(r => Str(r, "regime") == "RGPD");
        var notification = gdprStats.GetProperty("steps").EnumerateArray().Single(x => Str(x, "step") == "Notification");
        Assert.True(notification.GetProperty("sent").GetInt32() > notification.GetProperty("onTime").GetInt32());
        d = await Json(await Manager.PutAsJsonAsync($"/api/regulatory-notifications/{Id(d)}", Update(d, new { status = "Close" })));
        // DRG-07 : close, la déclaration ne se modifie plus — SOC-15 l'interdit à un User (403),
        // DRG-07 à un gestionnaire (400).
        Assert.Equal(HttpStatusCode.Forbidden, (await User.PutAsJsonAsync($"/api/regulatory-notifications/{Id(d)}",
            Update(d, new { summary = "Autre" }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Manager.PutAsJsonAsync($"/api/regulatory-notifications/{Id(d)}",
            Update(d, new { summary = "Autre" }))).StatusCode);

        // DRG-04 : « non requise » se motive.
        var other = Id(await Json(await User.PostAsJsonAsync("/api/incidents", Incident("Hameçonnage signalé"))));
        await Qualify(Manager, other, nis2: true, gdpr: false);
        var n = (await DeclarationsOf(Manager, other)).Single();
        Assert.Equal(HttpStatusCode.BadRequest, (await Manager.PutAsJsonAsync($"/api/regulatory-notifications/{Id(n)}",
            Update(n, new { status = "Non requise" }))).StatusCode);
    }

    [Fact]
    public async Task DRG_11_DRG_13_echeance_proche_dans_l_urgent_et_notifiee()
    {
        var id = Id(await Json(await User.PostAsJsonAsync("/api/incidents", Incident("Intrusion sur le VPN"))));
        await Qualify(Manager, id, nis2: true, gdpr: false, awareAt: DateTime.UtcNow.AddHours(-19));   // alerte due dans 5 h
        var d = (await DeclarationsOf(Manager, id)).Single();
        var regulatory = (await Json(await Manager.GetAsync("/api/console"))).GetProperty("management").GetProperty("regulatory").EnumerateArray();
        Assert.Contains(regulatory, r => Str(r, "type") == "notification" && Id(r) == Id(d) && Str(r, "status").StartsWith("Alerte précoce : 4 h"));

        await Json(await api.Client("Admin", "admin.drg").PostAsync("/api/notifications/run", null));
        await using var db = api.Database();
        var events = await db.Notifications.Where(n => n.TargetType == "notification" && n.TargetId == Id(d)).Select(n => n.Event).ToListAsync();
        Assert.True(events.Any(e => e.StartsWith("Alerte précoce : échéance dans moins de 6 h")), string.Join(" | ", events));
        Assert.True(await db.Notifications.AnyAsync(n => n.TargetType == "notification" && n.TargetId == Id(d) && n.Event == "Déclaration réglementaire créée"));
    }
}
