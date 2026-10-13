using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SAloha.Api.Tests;

/// <summary>SOC-21, événements datés : SLA, J-1, actions en retard, revues échues (INC-18, INC-24, CHG-24, PRB-31, KB-20, SLM-24).</summary>
[Collection("api")]
public class DatedEventsTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();
    private static string Ref(JsonElement e) => e.GetProperty("reference").GetString()!;

    private static async Task<List<string>> Events(HttpClient c, string reference) =>
        (await Json(await c.GetAsync("/api/notifications"))).GetProperty("items").EnumerateArray()
            .Where(n => n.GetProperty("reference").GetString() == reference)
            .Select(n => n.GetProperty("event").GetString()!).ToList();

    private async Task Run() => await Json(await api.Client().PostAsync("/api/notifications/run", null));

    [Fact]
    public async Task Incident_SLA_depasse_et_en_risque()
    {
        var m = api.Client("Manager", "chef.dated");
        var tech = api.Client("User", "tech.dated");
        object Inc(string title) => new { title, impact = "Faible", urgency = "Faible", ownerType = "User", ownerId = "tech.dated", ownerDisplayName = "Tech" };
        var late = await Json(await m.PostAsJsonAsync("/api/incidents", Inc("SLA dépassé")));
        var risk = await Json(await m.PostAsJsonAsync("/api/incidents", Inc("SLA en risque")));
        await using (var db = api.Database())
        {
            var now = DateTime.UtcNow;
            var l = await db.Incidents.FirstAsync(x => x.Id == Id(late));
            l.CreatedAt = now.AddHours(-10); l.ResolutionDueAt = now.AddHours(-1);
            var r = await db.Incidents.FirstAsync(x => x.Id == Id(risk));
            r.CreatedAt = now.AddHours(-9); r.ResolutionDueAt = now.AddHours(1);   // 10 % du délai restant
            await db.SaveChangesAsync();
        }
        await Run();
        await Run();   // pas de doublon
        Assert.Equal(["SLA dépassé"], (await Events(tech, Ref(late))).Where(e => e != "Confié à vous"));
        Assert.Contains("SLA dépassé", await Events(api.Client("Manager", "autre.chef.dated"), Ref(late)));   // INC-18 : escalade
        Assert.Equal(["SLA en risque"], (await Events(tech, Ref(risk))).Where(e => e != "Confié à vous"));
    }

    [Fact]
    public async Task Changement_la_veille_action_en_retard_revue_echue()
    {
        var m = api.Client("Manager", "chef.dated2");
        var owner = api.Client("User", "owner.dated2");
        // CHG-24 : mise en œuvre dans moins de 24 h.
        var change = await Json(await m.PostAsJsonAsync("/api/changes", new
        {
            title = "Bascule DNS", changeType = "Normal", risk = "Faible", ownerType = "User", ownerId = "owner.dated2", ownerDisplayName = "Owner",
            plannedStart = DateTime.UtcNow.AddHours(12), plannedEnd = DateTime.UtcNow.AddHours(14), implementationPlan = "Plan", backoutPlan = "Retour"
        }));
        // KB-20 : article publié dont la revue est passée.
        var article = await Json(await m.PostAsJsonAsync("/api/knowledge", new
        {
            title = "Procédure DNS", articleType = "Procédure", content = "Étapes", ownerType = "User", ownerId = "owner.dated2", ownerDisplayName = "Owner"
        }));
        // PRB-31 : action en retard.
        var p = await Json(await m.PostAsJsonAsync("/api/problems", new
        {
            title = "Résolution DNS lente", description = "d", impact = "Moyen", urgency = "Moyenne", category = "c", affectedService = "s"
        }));
        await Json(await m.PostAsJsonAsync($"/api/problems/{Id(p)}/actions", new
        {
            title = "Changer de résolveur", description = "", dueDate = DateTime.UtcNow.Date.AddDays(-2),
            raci = new[]
            {
                new { role = "R", assigneeType = "User", assigneeId = "owner.dated2", assigneeDisplayName = "Owner" },
                new { role = "A", assigneeType = "User", assigneeId = "chef.dated2", assigneeDisplayName = "Chef" }
            }
        }));
        await using (var db = api.Database())
        {
            (await db.Changes.FirstAsync(x => x.Id == Id(change))).Status = "Planifié";
            var a = await db.KnowledgeArticles.FirstAsync(x => x.Id == Id(article));
            a.Status = "Publié"; a.ReviewDate = DateTime.UtcNow.Date.AddDays(-1);
            await db.SaveChangesAsync();
        }
        await Run();
        await Run();
        Assert.Single(await Events(owner, Ref(change)), e => e.StartsWith("Mise en œuvre le"));
        Assert.Single(await Events(owner, Ref(article)), e => e.StartsWith("Revue de l'article échue"));
        Assert.Single(await Events(owner, Ref(p)), e => e.StartsWith("Action en retard : Changer de résolveur"));
        // Les gestionnaires aussi, même s'ils ne sont pas l'A.
        Assert.Contains(await Events(api.Client("Manager", "autre.chef.dated2"), Ref(p)), e => e.StartsWith("Action en retard"));
        // A de l'action ET gestionnaire : une seule notification affichée, lue d'un coup.
        var chef = api.Client("Manager", "chef.dated2");
        Assert.Single(await Events(chef, Ref(p)), e => e.StartsWith("Action en retard"));
        var n = (await Json(await chef.GetAsync("/api/notifications"))).GetProperty("items").EnumerateArray()
            .First(x => x.GetProperty("reference").GetString() == Ref(p) && x.GetProperty("event").GetString()!.StartsWith("Action en retard"));
        await chef.PostAsync($"/api/notifications/{n.GetProperty("id").GetInt64()}/read", null);
        Assert.True((await Json(await chef.GetAsync("/api/notifications"))).GetProperty("items").EnumerateArray()
            .Where(x => x.GetProperty("reference").GetString() == Ref(p)).All(x => x.GetProperty("read").GetBoolean()));
    }

    [Fact]
    public async Task Seul_l_Admin_declenche_le_releve()
    {
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await api.Client("Manager").PostAsync("/api/notifications/run", null)).StatusCode);
    }
}
