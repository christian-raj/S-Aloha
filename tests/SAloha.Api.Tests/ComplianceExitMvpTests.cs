using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SAloha.Api.Modules.ComplianceAssessment;

namespace SAloha.Api.Tests;

/// <summary>Conformité NIS 2 : plan de traitement des écarts (NIS-14), revue périodique (NIS-16), export (NIS-17).</summary>
[Collection("api")]
public class ComplianceExitMvpTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static object Dto(string title, string? status, DateTime? next = null, string owner = "nis.pilote") => new
    {
        title, description = "Périmètre", status, entityCategory = Assessment.Important,
        ownerType = "User", ownerId = owner, ownerDisplayName = owner, nextAssessmentDate = next
    };

    /// <summary>Évaluation entièrement répondue : 3 partout sauf la première exigence (1, « Inventaire partiel »).</summary>
    private async Task<(int Id, string Reference, string GapCode)> Answered(HttpClient manager, string title)
    {
        var created = await Json(await manager.PostAsJsonAsync("/api/assessments", Dto(title, null)));
        var id = created.GetProperty("id").GetInt32();
        var q = (await Json(await manager.GetAsync($"/api/assessments/{id}/questionnaire"))).GetProperty("objectives").EnumerateArray()
            .SelectMany(o => o.GetProperty("themes").EnumerateArray())
            .SelectMany(t => t.GetProperty("requirements").EnumerateArray()).ToList();
        foreach (var (r, i) in q.Select((r, i) => (r, i)))
            await Json(await manager.PutAsJsonAsync($"/api/assessments/{id}/responses/{r.GetProperty("id").GetInt32()}",
                new { score = i == 0 ? 1 : 3, notApplicable = false, justification = i == 0 ? "Inventaire partiel; à compléter" : "" }));
        return (id, created.GetProperty("reference").GetString()!, q[0].GetProperty("code").GetString()!);
    }

    [Fact]
    public async Task NIS_14_17_ecart_avec_son_action_et_export()
    {
        var m = api.Client("Manager", "nis14.manager");
        var (id, _, code) = await Answered(m, "NIS-14");

        var gap = (await Json(await m.GetAsync($"/api/assessments/{id}/score"))).GetProperty("gaps")[0];
        Assert.Equal(code, gap.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, gap.GetProperty("action").ValueKind);

        var improvement = await Json(await m.PostAsJsonAsync($"/api/assessments/{id}/improvements", new { code }));
        gap = (await Json(await m.GetAsync($"/api/assessments/{id}/score"))).GetProperty("gaps")[0];
        Assert.Equal(improvement.GetProperty("reference").GetString(), gap.GetProperty("action").GetProperty("reference").GetString());
        Assert.Equal("Proposée", gap.GetProperty("action").GetProperty("status").GetString());

        // NIS-17 : une ligne par exigence évaluée, écart et action liée compris ; lisible de tous.
        var export = await Json(await api.Client("User", "nis14.auditeur").GetAsync($"/api/assessments/{id}/export"));
        var rows = export.GetProperty("rows").EnumerateArray().ToList();
        var row = rows.Single(r => r.GetProperty("code").GetString() == code);
        Assert.Equal(1, row.GetProperty("score").GetInt32());
        Assert.Equal(2, row.GetProperty("gap").GetInt32());
        Assert.Equal("Inventaire partiel; à compléter", row.GetProperty("justification").GetString());
        Assert.Equal(improvement.GetProperty("reference").GetString(), row.GetProperty("action").GetString());
        Assert.Equal(JsonValueKind.Null, rows.First(r => r.GetProperty("code").GetString() != code).GetProperty("action").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await m.GetAsync("/api/assessments/999999/export")).StatusCode);
    }

    [Fact]
    public async Task NIS_16_prochaine_evaluation_et_relance()
    {
        var m = api.Client("Manager", "nis16.manager");
        var (id, reference, _) = await Answered(m, "NIS-16");
        var url = $"/api/assessments/{id}";

        // Validée sans date : 12 mois après la validation.
        var validated = await Json(await m.PutAsJsonAsync(url, Dto("NIS-16", "Validée")));
        var next = validated.GetProperty("nextAssessmentDate").GetDateTime();
        Assert.Equal(DateTime.UtcNow.Date.AddMonths(12), next.Date);

        // Figée, l'évaluation garde sa date modifiable ; le reste ne l'est pas.
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        await Json(await m.PutAsJsonAsync(url, Dto("NIS-16", "Validée", yesterday)));
        Assert.Equal(HttpStatusCode.BadRequest, (await m.PutAsJsonAsync(url, Dto("NIS-16 renommée", "Validée", yesterday))).StatusCode);

        // Échéance atteinte : relance au pilote, une seule fois.
        await Json(await api.Client().PostAsync("/api/notifications/run", null));
        await Json(await api.Client().PostAsync("/api/notifications/run", null));
        var events = (await Json(await api.Client("User", "nis.pilote").GetAsync("/api/notifications"))).GetProperty("items").EnumerateArray()
            .Where(n => n.GetProperty("reference").GetString() == reference).Select(n => n.GetProperty("event").GetString()).ToList();
        Assert.Single(events, e => e!.StartsWith("Évaluation à renouveler"));
        // Console du gestionnaire : relance « À renouveler » (Revues échues).
        var due = (await Json(await m.GetAsync("/api/console"))).GetProperty("management").GetProperty("reviewsDue").EnumerateArray();
        Assert.Contains(due, d => d.GetProperty("reference").GetString() == reference && d.GetProperty("status").GetString() == "À renouveler");
    }
}
