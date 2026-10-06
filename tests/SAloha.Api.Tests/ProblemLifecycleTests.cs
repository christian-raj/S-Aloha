using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SAloha.Api.Tests;

/// <summary>Constats M2, M4, M5 et M6 de la revue du 2026-10-06.</summary>
[Collection("api")]
public class ProblemLifecycleTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static async Task<int> CreateProblem(HttpClient client, string title) =>
        (await Json(await client.PostAsJsonAsync("/api/problems", new
        {
            title, description = "d", impact = "Moyen", urgency = "Moyenne", category = "Test", affectedService = "Test"
        }))).GetProperty("id").GetInt32();

    private static object Raci(string responsible) => new[]
    {
        new { role = "R", assigneeType = "User", assigneeId = responsible, assigneeDisplayName = responsible },
        new { role = "A", assigneeType = "User", assigneeId = "lova.rabe", assigneeDisplayName = "Lova Rabe" }
    };

    private static async Task CreateAction(HttpClient client, int problemId, string title, string responsible, DateTime? due) =>
        await Json(await client.PostAsJsonAsync($"/api/problems/{problemId}/actions",
            new { title, description = "", dueDate = due, raci = Raci(responsible) }));

    private static async Task<JsonElement[]> MyActions(HttpClient client) =>
        (await Json(await client.GetAsync("/api/console"))).GetProperty("myActions").EnumerateArray().ToArray();

    [Fact]
    public async Task M2_mes_actions_ne_dependent_pas_de_la_casse_de_l_identifiant()
    {
        var admin = api.Client();
        var id = await CreateProblem(admin, "M2 casse");
        await CreateAction(admin, id, "M2 pour Tiana", "tiana.ravelo", null);

        // Données enregistrées en minuscules, utilisateur connecté avec une
        // autre casse : avant correction, « Mes actions » était vide.
        var mine = await MyActions(api.Client("User", "Tiana.Ravelo"));
        Assert.Contains(mine, a => a.GetProperty("title").GetString() == "M2 pour Tiana");

        var filtered = await Json(await api.Client("User", "Tiana.Ravelo").GetAsync("/api/actions?assignee=Tiana.Ravelo"));
        Assert.Contains(filtered.EnumerateArray(), a => a.GetProperty("title").GetString() == "M2 pour Tiana");
    }

    [Fact]
    public async Task M4_un_probleme_rouvert_n_est_plus_clos()
    {
        var manager = api.Client("Manager", "lova.rabe");
        var id = await CreateProblem(manager, "M4 réouverture");
        var problem = await Json(await manager.GetAsync($"/api/problems/{id}"));
        object Update(string status) => new
        {
            title = "M4 réouverture", description = "d", status, impact = "Moyen", urgency = "Moyenne",
            category = "Test", affectedService = "Test", knownErrorWorkaround = (string?)null, rootCause = (string?)null
        };

        var closed = await Json(await manager.PutAsJsonAsync($"/api/problems/{id}", Update("Clos")));
        Assert.NotEqual(JsonValueKind.Null, closed.GetProperty("closedAt").ValueKind);

        var reopened = await Json(await manager.PutAsJsonAsync($"/api/problems/{id}", Update("En analyse")));
        Assert.Equal(JsonValueKind.Null, reopened.GetProperty("closedAt").ValueKind);
    }

    [Fact]
    public async Task M5_la_recherche_ignore_la_casse()
    {
        var client = api.Client();
        await CreateProblem(client, "M5 Déconnexions VPN récurrentes");
        foreach (var q in new[] { "vpn", "VPN", "m5 déconnexions" })
        {
            var found = await Json(await client.GetAsync($"/api/problems?q={Uri.EscapeDataString(q)}"));
            Assert.True(found.GetArrayLength() >= 1, $"aucun résultat pour « {q} »");
        }
    }

    [Fact]
    public async Task M6_une_action_n_est_en_retard_qu_apres_son_echeance()
    {
        var admin = api.Client();
        var id = await CreateProblem(admin, "M6 échéances");
        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);  // comme l'interface : date seule, minuit UTC
        await CreateAction(admin, id, "M6 due aujourd'hui", "rado.m6", today);
        await CreateAction(admin, id, "M6 due hier", "rado.m6", today.AddDays(-1));

        var mine = await MyActions(api.Client("User", "rado.m6"));
        bool Overdue(string title) => mine.Single(a => a.GetProperty("title").GetString() == title).GetProperty("overdue").GetBoolean();
        Assert.False(Overdue("M6 due aujourd'hui"));
        Assert.True(Overdue("M6 due hier"));
    }
}
