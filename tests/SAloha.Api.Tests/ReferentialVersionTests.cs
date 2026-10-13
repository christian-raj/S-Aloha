using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Modules.ComplianceAssessment;

namespace SAloha.Api.Tests;

/// <summary>NIS-18 : version du référentiel évaluée, évaluation validée figée face à une nouvelle version embarquée.</summary>
[Collection("api")]
public class ReferentialVersionTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static List<JsonElement> Requirements(JsonElement q) =>
        q.GetProperty("objectives").EnumerateArray().SelectMany(o => o.GetProperty("themes").EnumerateArray())
            .SelectMany(t => t.GetProperty("requirements").EnumerateArray()).ToList();

    [Fact]
    public async Task NIS_18_une_evaluation_validee_ne_change_pas_avec_une_nouvelle_version()
    {
        var m = api.Client("Manager", "lova.nis18");
        object Dto(string? status) => new { title = "NIS-18", description = "SI", status, entityCategory = Assessment.Important };
        var a = await Json(await m.PostAsJsonAsync("/api/assessments", Dto(null)));
        var id = a.GetProperty("id").GetInt32();
        Assert.Equal(ReferentialStore.Version, a.GetProperty("referentialVersion").GetString());

        // Tout noter 3, puis valider.
        var q = Requirements(await Json(await m.GetAsync($"/api/assessments/{id}/questionnaire")));
        foreach (var r in q)
            await Json(await m.PutAsJsonAsync($"/api/assessments/{id}/responses/{r.GetProperty("id").GetInt32()}",
                new { score = 3, notApplicable = false, justification = "" }));
        await Json(await m.PutAsJsonAsync($"/api/assessments/{id}", Dto("Validée")));
        var before = await Json(await m.GetAsync($"/api/assessments/{id}/score"));
        Assert.Equal(100.0, before.GetProperty("percent").GetDouble());

        // « Nouvelle version embarquée » : une exigence réservée aux entités essentielles
        // devient applicable aux importantes, et une exigence évaluée change d'objectif.
        int added, moved, movedObjective;
        await using (var db = api.Database())
        {
            var extra = await db.SecurityRequirements.FirstAsync(r => !r.ForImportant && r.ForEssential);
            extra.ForImportant = true; added = extra.Id;
            var evaluated = await db.SecurityRequirements.FirstAsync(r => r.Id == q[0].GetProperty("id").GetInt32());
            movedObjective = evaluated.ObjectiveId;
            evaluated.ObjectiveId = evaluated.ObjectiveId == 1 ? 2 : 1; moved = evaluated.Id;
            await db.SaveChangesAsync();
        }
        try
        {
            // L'évaluation validée reste telle qu'évaluée : même score, même questionnaire.
            var after = await Json(await m.GetAsync($"/api/assessments/{id}/score"));
            Assert.Equal(100.0, after.GetProperty("percent").GetDouble());
            Assert.Equal(before.GetProperty("applicable").GetInt32(), after.GetProperty("applicable").GetInt32());
            var frozen = await Json(await m.GetAsync($"/api/assessments/{id}/questionnaire"));
            Assert.True(frozen.GetProperty("frozen").GetBoolean());
            Assert.DoesNotContain(Requirements(frozen), r => r.GetProperty("id").GetInt32() == added);
            var movedObj = frozen.GetProperty("objectives").EnumerateArray()
                .Single(o => o.GetProperty("themes").EnumerateArray().SelectMany(t => t.GetProperty("requirements").EnumerateArray())
                    .Any(r => r.GetProperty("id").GetInt32() == moved));
            Assert.Equal(movedObjective, movedObj.GetProperty("id").GetInt32());

            // Rouverte, elle se réaligne : l'exigence ajoutée est à évaluer.
            await Json(await m.PutAsJsonAsync($"/api/assessments/{id}", Dto("En cours")));
            var reopened = await Json(await m.GetAsync($"/api/assessments/{id}/score"));
            Assert.Equal(before.GetProperty("applicable").GetInt32() + 1, reopened.GetProperty("applicable").GetInt32());
            Assert.False((await Json(await m.GetAsync($"/api/assessments/{id}/questionnaire"))).GetProperty("frozen").GetBoolean());
        }
        finally
        {
            // Rendre au référentiel sa structure pour les autres tests.
            await using var db = api.Database();
            (await db.SecurityRequirements.FirstAsync(r => r.Id == added)).ForImportant = false;
            (await db.SecurityRequirements.FirstAsync(r => r.Id == moved)).ObjectiveId = movedObjective;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task NIS_18_une_evaluation_validee_avant_la_regle_est_figee_au_demarrage()
    {
        var m = api.Client("Manager", "lova.nis18b");
        var a = await Json(await m.PostAsJsonAsync("/api/assessments",
            new { title = "NIS-18 ancienne", description = "SI", status = (string?)null, entityCategory = Assessment.Important }));
        var id = a.GetProperty("id").GetInt32();
        await using var db = api.Database();
        // Comme une évaluation validée avant NIS-18 : validée, rien de figé.
        var e = await db.Assessments.FirstAsync(x => x.Id == id);
        e.Status = "Validée"; e.FrozenRequirementsJson = null;
        await db.SaveChangesAsync();

        AssessmentReferential.FreezeValidated(db);
        var frozen = await db.Assessments.AsNoTracking().FirstAsync(x => x.Id == id);
        Assert.NotNull(frozen.FrozenRequirementsJson);
    }
}
