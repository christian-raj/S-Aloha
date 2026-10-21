using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SAloha.Api.Tests;

/// <summary>Problèmes, sortie du MVP : motif de réouverture (PRB-10), approbation par l'A (PRB-25), changement depuis une action (PRB-27).</summary>
[Collection("api")]
public class ProblemExitMvpTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();

    private static object Problem(string title, string status, string? closureCode = null, string? reopenReason = null) => new
    {
        title, description = "d", status, impact = "Moyen", urgency = "Moyenne", category = "Test", affectedService = "Test",
        knownErrorWorkaround = (string?)null, rootCause = "Cause", closureCode, reopenReason, source = "Proactif"
    };

    private static object Action(string title, string? status, string r, string a) => new
    {
        title, description = "Corriger la configuration", status, dueDate = (DateTime?)null,
        raci = new[]
        {
            new { role = "R", assigneeType = "User", assigneeId = r, assigneeDisplayName = r },
            new { role = "A", assigneeType = "User", assigneeId = a, assigneeDisplayName = a }
        }
    };

    private static async Task<List<string>> Events(HttpClient c, string reference) =>
        (await Json(await c.GetAsync("/api/notifications"))).GetProperty("items").EnumerateArray()
            .Where(n => n.GetProperty("reference").GetString() == reference)
            .Select(n => n.GetProperty("event").GetString()!).ToList();

    [Fact]
    public async Task PRB_10_rouvrir_exige_un_motif_efface_a_la_resolution_suivante()
    {
        var m = api.Client("Manager", "prb10.manager");
        var p = await Json(await m.PostAsJsonAsync("/api/problems", Problem("PRB-10", "Nouveau")));
        var url = $"/api/problems/{Id(p)}";
        await Json(await m.PutAsJsonAsync(url, Problem("PRB-10", "En analyse")));
        await Json(await m.PutAsJsonAsync(url, Problem("PRB-10", "Résolu")));

        var refused = await m.PutAsJsonAsync(url, Problem("PRB-10", "En analyse"));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("Motiver", await refused.Content.ReadAsStringAsync());

        var reopened = await Json(await m.PutAsJsonAsync(url, Problem("PRB-10", "En analyse", reopenReason: "Récidive constatée")));
        Assert.Equal("Récidive constatée", reopened.GetProperty("reopenReason").GetString());

        // Résolu à nouveau : le motif est effacé, une nouvelle réouverture en exige un.
        var resolved = await Json(await m.PutAsJsonAsync(url, Problem("PRB-10", "Résolu", reopenReason: "Récidive constatée")));
        Assert.Equal(JsonValueKind.Null, resolved.GetProperty("reopenReason").ValueKind);
        await Json(await m.PutAsJsonAsync(url, Problem("PRB-10", "Clos", "Corrigé")));
        Assert.Equal(HttpStatusCode.BadRequest, (await m.PutAsJsonAsync(url, Problem("PRB-10", "En analyse", "Corrigé"))).StatusCode);
        await Json(await m.PutAsJsonAsync(url, Problem("PRB-10", "En analyse", reopenReason: "Correctif inefficace")));
    }

    [Fact]
    public async Task PRB_25_seul_l_A_ou_un_gestionnaire_termine_une_action()
    {
        var m = api.Client("Manager", "prb25.manager");
        var r = api.Client("User", "prb25.r");
        var a = api.Client("User", "prb25.a");
        var p = await Json(await m.PostAsJsonAsync("/api/problems", Problem("PRB-25", "Nouveau")));
        var action = await Json(await m.PostAsJsonAsync($"/api/problems/{Id(p)}/actions", Action("PRB-25 corriger", null, "prb25.r", "prb25.a")));
        var url = $"/api/actions/{Id(action)}";

        // Approbation demandée sur une action À faire : refusée.
        Assert.Equal(HttpStatusCode.BadRequest, (await r.PostAsync(url + "/request-approval", null)).StatusCode);
        await Json(await r.PutAsJsonAsync(url, Action("PRB-25 corriger", "En cours", "prb25.r", "prb25.a")));
        // Le R ne termine pas lui-même.
        Assert.Equal(HttpStatusCode.Forbidden, (await r.PutAsJsonAsync(url, Action("PRB-25 corriger", "Terminée", "prb25.r", "prb25.a"))).StatusCode);
        // Seul le R (ou un gestionnaire) demande l'approbation.
        Assert.Equal(HttpStatusCode.Forbidden, (await a.PostAsync(url + "/request-approval", null)).StatusCode);
        var asked = await Json(await r.PostAsync(url + "/request-approval", null));
        Assert.NotEqual(JsonValueKind.Null, asked.GetProperty("approvalRequestedAt").ValueKind);
        Assert.Contains("Approbation demandée : PRB-25 corriger", await Events(a, p.GetProperty("reference").GetString()!));

        var done = await Json(await a.PutAsJsonAsync(url, Action("PRB-25 corriger", "Terminée", "prb25.r", "prb25.a")));
        Assert.Equal("Terminée", done.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, done.GetProperty("approvalRequestedAt").ValueKind);
    }

    [Fact]
    public async Task PRB_27_changement_cree_depuis_une_action_relie_au_probleme()
    {
        var m = api.Client("Manager", "prb27.manager");
        var r = api.Client("User", "prb27.r");
        var other = api.Client("User", "prb27.autre");
        var p = await Json(await m.PostAsJsonAsync("/api/problems", Problem("PRB-27", "Nouveau")));
        var action = await Json(await m.PostAsJsonAsync($"/api/problems/{Id(p)}/actions", Action("PRB-27 corriger", null, "prb27.r", "prb27.manager")));
        var change = await Json(await r.PostAsJsonAsync("/api/changes", new { title = "PRB-27 corriger", description = "Corriger la configuration", changeType = "Normal", risk = "Moyen" }));
        var reference = change.GetProperty("reference").GetString();
        var url = $"/api/actions/{Id(action)}/change";

        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync(url, new { reference })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await r.PutAsJsonAsync(url, new { reference = "CHG-1999-9999" })).StatusCode);
        var linked = await Json(await r.PutAsJsonAsync(url, new { reference }));
        Assert.Equal(reference, linked.GetProperty("changeReference").GetString());
        Assert.Equal(Id(change), linked.GetProperty("changeId").GetInt32());

        var links = await Json(await m.GetAsync($"/api/links?type=problem&id={Id(p)}"));
        Assert.Single(links.EnumerateArray(), l => l.GetProperty("reference").GetString() == reference);
        // Rattacher à nouveau ne duplique pas le lien.
        await Json(await r.PutAsJsonAsync(url, new { reference }));
        links = await Json(await m.GetAsync($"/api/links?type=problem&id={Id(p)}"));
        Assert.Single(links.EnumerateArray(), l => l.GetProperty("reference").GetString() == reference);
    }
}
