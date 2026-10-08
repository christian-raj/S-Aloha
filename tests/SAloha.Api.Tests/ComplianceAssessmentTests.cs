using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SAloha.Api.Modules.ComplianceAssessment;

namespace SAloha.Api.Tests;

/// <summary>Conformité NIS 2 — règles NIS-xx de docs/reference/processus/conformite-nis2.md.</summary>
[Collection("api")]
public class ComplianceAssessmentTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static object NewAssessment(string title, string category) => new
    {
        title, description = "Périmètre : SI de production", entityCategory = category
    };

    private static IEnumerable<JsonElement> Requirements(JsonElement questionnaire) =>
        questionnaire.GetProperty("objectives").EnumerateArray()
            .SelectMany(o => o.GetProperty("themes").EnumerateArray())
            .SelectMany(t => t.GetProperty("requirements").EnumerateArray());

    [Fact]
    public void Score_et_maturite_suivent_les_regles()
    {
        // NIS-10 : Σ / (3 × notées), non notées exclues ; null sans réponse.
        Assert.Null(Scoring.Percent([null, null]));
        Assert.Equal(50.0, Scoring.Percent([3, 0, null]));
        Assert.Equal(66.7, Scoring.Percent([2, 2, 2]));
        // NIS-12 : seuils 25 / 50 / 75 / 90.
        Assert.Equal(1, Scoring.MaturityOf(24.9).Level);
        Assert.Equal(2, Scoring.MaturityOf(25).Level);
        Assert.Equal(3, Scoring.MaturityOf(50).Level);
        Assert.Equal(4, Scoring.MaturityOf(75).Level);
        Assert.Equal(5, Scoring.MaturityOf(90).Level);
        Assert.Equal(2, Scoring.Gap(1));
    }

    [Fact]
    public async Task La_structure_du_referentiel_est_chargee_sans_texte()
    {
        var r = await Json(await api.Client("User", "tiana.ravelo").GetAsync("/api/compliance/referential"));
        Assert.Equal(152, r.GetProperty("requirements").GetInt32());
        Assert.Equal(20, r.GetProperty("objectives").GetArrayLength());
        // Corrections de la v2.5 sur les cibles.
        var all = r.GetProperty("objectives").EnumerateArray().SelectMany(o => o.GetProperty("requirements").EnumerateArray()).ToList();
        Assert.Contains(all, x => x.GetProperty("code").GetString() == "13.5-EI/EE");
        Assert.Contains(all, x => x.GetProperty("code").GetString() == "14.5-EE");
        // Aucun intitulé ISO, seulement des numéros (ADR-0012).
        Assert.All(all, x => Assert.DoesNotMatch(@"[A-Za-zÀ-ÿ]{4,}", x.GetProperty("isoControls").GetString()!.Replace("27002:2022", "")));
    }

    [Fact]
    public async Task Import_du_texte_reserve_a_l_admin_et_compte_rendu()
    {
        const string csv = "﻿\"Référence\";\"Contenu\";\"Objectif\"\n"
            + "\"1.1-EI/EE\";\"L'entité liste ses activités.\nSur deux lignes ; avec \"\"guillemets\"\".\";\"x\"\n"
            + "\"99.9-EE\";\"Inconnue\";\"x\"\n";
        var denied = await api.Client("Manager", "lova.rabe").PostAsJsonAsync("/api/compliance/referential/import", new { csv });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var report = await Json(await api.Client().PostAsJsonAsync("/api/compliance/referential/import", new { csv }));
        Assert.Equal(1, report.GetProperty("updated").GetInt32());
        Assert.Equal("99.9-EE", report.GetProperty("unknown")[0].GetString());

        var r = await Json(await api.Client().GetAsync("/api/compliance/referential"));
        var text = r.GetProperty("objectives")[0].GetProperty("requirements")[0].GetProperty("text").GetString();
        Assert.Equal("L'entité liste ses activités.\nSur deux lignes ; avec \"guillemets\".", text);

        var bad = await api.Client().PostAsJsonAsync("/api/compliance/referential/import", new { csv = "a;b\n1;2" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Evaluation_de_bout_en_bout()
    {
        var manager = api.Client("Manager", "lova.rabe");
        var user = api.Client("User", "tiana.ravelo");

        // NIS-02 : création réservée aux gestionnaires.
        var forbidden = await user.PostAsJsonAsync("/api/assessments", NewAssessment("NIS2 user", Assessment.Important));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var created = await Json(await manager.PostAsJsonAsync("/api/assessments", NewAssessment("NIS2 EI", Assessment.Important)));
        var id = created.GetProperty("id").GetInt32();
        Assert.StartsWith("EVA-", created.GetProperty("reference").GetString());
        Assert.Equal("Brouillon", created.GetProperty("status").GetString());

        // NIS-01 : une entité importante ne voit pas les exigences réservées aux essentielles.
        var q = Requirements(await Json(await user.GetAsync($"/api/assessments/{id}/questionnaire"))).ToList();
        Assert.DoesNotContain(q, r => r.GetProperty("code").GetString()!.EndsWith("-EE") && !r.GetProperty("code").GetString()!.EndsWith("EI/EE"));
        var referential = await Json(await user.GetAsync("/api/compliance/referential"));
        var forImportant = referential.GetProperty("objectives").EnumerateArray()
            .SelectMany(o => o.GetProperty("requirements").EnumerateArray()).Count(r => r.GetProperty("forImportant").GetBoolean());
        var applicable = q.Count;
        Assert.Equal(forImportant, applicable);
        Assert.True(applicable < 152);

        // NIS-03/04 : tout rôle répond ; la première réponse passe l'évaluation « En cours ».
        var first = q[0].GetProperty("id").GetInt32();
        var answered = await Json(await user.PutAsJsonAsync($"/api/assessments/{id}/responses/{first}", new { score = 1, notApplicable = false, justification = "Inventaire partiel" }));
        Assert.Equal("En cours", answered.GetProperty("assessmentStatus").GetString());
        var outOfRange = await user.PutAsJsonAsync($"/api/assessments/{id}/responses/{first}", new { score = 4, notApplicable = false });
        Assert.Equal(HttpStatusCode.BadRequest, outOfRange.StatusCode);

        // NIS-05 : validation refusée tant qu'il manque des réponses.
        object Update(string status) => new { title = "NIS2 EI", description = "Périmètre : SI de production", status, entityCategory = Assessment.Important };
        var early = await manager.PutAsJsonAsync($"/api/assessments/{id}", Update("Validée"));
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);

        // Tout répondre : 3 partout sauf la première (1) et une non applicable sans justification.
        var na = q[1].GetProperty("id").GetInt32();
        foreach (var r in q.Skip(1))
        {
            var rid = r.GetProperty("id").GetInt32();
            var body = rid == na ? (object)new { score = (int?)null, notApplicable = true, justification = "" } : new { score = 3, notApplicable = false, justification = "" };
            await Json(await user.PutAsJsonAsync($"/api/assessments/{id}/responses/{rid}", body));
        }
        var unjustified = await manager.PutAsJsonAsync($"/api/assessments/{id}", Update("Validée"));
        Assert.Contains("non applicable", await unjustified.Content.ReadAsStringAsync());
        await Json(await user.PutAsJsonAsync($"/api/assessments/{id}/responses/{na}", new { score = (int?)null, notApplicable = true, justification = "Pas d'accès distant" }));

        // NIS-10/12 : un seul écart de 2 points sur (applicable − 1) notées.
        var score = await Json(await user.GetAsync($"/api/assessments/{id}/score"));
        var rated = applicable - 1;
        Assert.Equal(Math.Round((3.0 * rated - 2) * 100 / (3 * rated), 1), score.GetProperty("percent").GetDouble());
        Assert.Equal(5, score.GetProperty("maturity").GetProperty("level").GetInt32());
        // Un bloc par objectif de sécurité, pas un par exigence.
        var objectiveIds = score.GetProperty("objectives").EnumerateArray().Select(o => o.GetProperty("id").GetInt32()).ToList();
        Assert.Equal(objectiveIds.Distinct().Count(), objectiveIds.Count);
        Assert.True(objectiveIds.Count <= 20);
        var gap = score.GetProperty("gaps")[0];
        Assert.Equal(2, gap.GetProperty("gap").GetInt32());

        // NIS-15 : l'écart devient une action d'amélioration reliée, une seule fois.
        var code = gap.GetProperty("code").GetString();
        var improvement = await Json(await user.PostAsJsonAsync($"/api/assessments/{id}/improvements", new { code }));
        Assert.StartsWith("AMI-", improvement.GetProperty("reference").GetString());
        var again = await user.PostAsJsonAsync($"/api/assessments/{id}/improvements", new { code });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var links = await Json(await user.GetAsync($"/api/links?type=assessment&id={id}"));
        Assert.Contains(links.EnumerateArray(), l => l.GetProperty("reference").GetString() == improvement.GetProperty("reference").GetString());

        // NIS-06/07 : validée, l'évaluation est figée ; un gestionnaire la rouvre.
        var validated = await Json(await manager.PutAsJsonAsync($"/api/assessments/{id}", Update("Validée")));
        Assert.Equal("Validée", validated.GetProperty("status").GetString());
        var frozen = await user.PutAsJsonAsync($"/api/assessments/{id}/responses/{first}", new { score = 2, notApplicable = false });
        Assert.Equal(HttpStatusCode.BadRequest, frozen.StatusCode);
        var renamed = await manager.PutAsJsonAsync($"/api/assessments/{id}",
            new { title = "Renommée", description = "Périmètre : SI de production", status = "Validée", entityCategory = Assessment.Important });
        Assert.Equal(HttpStatusCode.BadRequest, renamed.StatusCode);
        var reopened = await Json(await manager.PutAsJsonAsync($"/api/assessments/{id}", Update("En cours")));
        Assert.Equal(JsonValueKind.Null, reopened.GetProperty("validatedAt").ValueKind);
    }
}
