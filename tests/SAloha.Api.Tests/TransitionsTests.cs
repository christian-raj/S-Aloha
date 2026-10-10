using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SAloha.Api.Tests;

/// <summary>Transitions contraintes (SOC-05) : seules celles du tableau § 4 de chaque pratique.</summary>
[Collection("api")]
public class TransitionsTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static async Task<string> Refused(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.BadRequest, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("message").GetString()!;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();

    private static object Incident(string? status = null) => new
    {
        title = "Transition", impact = "Moyen", urgency = "Moyenne", status, resolution = "Corrigé",
        resolutionCode = "Correctif appliqué", ownerType = "User", ownerId = "hery.rakoto", ownerDisplayName = "Hery Rakoto"
    };

    [Fact]
    public async Task Une_transition_non_listee_est_refusee_avec_les_statuts_accessibles()
    {
        var c = api.Client("Manager");
        var i = await Json(await c.PostAsJsonAsync("/api/incidents", Incident()));
        var message = await Refused(await c.PutAsJsonAsync($"/api/incidents/{Id(i)}", Incident("En attente")));
        Assert.Equal("Transition de « Nouveau » vers « En attente » non permise. Depuis « Nouveau » : « En cours », « Résolu ».", message);
    }

    [Fact]
    public async Task Un_statut_final_n_a_plus_de_transition()
    {
        var c = api.Client("Manager");
        var i = await Json(await c.PostAsJsonAsync("/api/incidents", Incident()));
        foreach (var status in new[] { "Résolu", "Clos" })
            await Json(await c.PutAsJsonAsync($"/api/incidents/{Id(i)}", Incident(status)));
        var message = await Refused(await c.PutAsJsonAsync($"/api/incidents/{Id(i)}", Incident("En cours")));
        Assert.Contains("« Clos » est un statut final", message);
    }

    [Fact]
    public async Task Le_gestionnaire_est_lui_aussi_tenu_par_les_transitions()
    {
        // Demandé → Autorisé saute l'évaluation (CHG-11) : refusé même à un Manager.
        var m = api.Client("Manager");
        var dto = new { title = "Sans évaluation", changeType = "Normal", risk = "Moyen", status = (string?)null,
            implementationPlan = "p", backoutPlan = "b" };
        var c = await Json(await m.PostAsJsonAsync("/api/changes", dto));
        await Refused(await m.PutAsJsonAsync($"/api/changes/{Id(c)}", dto with { status = "Autorisé" }));
        await Json(await m.PutAsJsonAsync($"/api/changes/{Id(c)}", dto with { status = "Évalué" }));
        await Json(await m.PutAsJsonAsync($"/api/changes/{Id(c)}", dto with { status = "Autorisé" }));
    }

    [Fact]
    public async Task Un_article_publie_ne_se_depublie_pas_a_la_main_mais_s_archive()
    {
        var m = api.Client("Manager");
        object Dto(string? status) => new { title = "Dépublier", articleType = "FAQ", content = "c", status };
        var a = await Json(await m.PostAsJsonAsync("/api/knowledge", Dto(null)));
        await Json(await m.PutAsJsonAsync($"/api/knowledge/{Id(a)}", Dto("Publié")));
        await Refused(await m.PutAsJsonAsync($"/api/knowledge/{Id(a)}", Dto("Brouillon")));
        await Json(await m.PutAsJsonAsync($"/api/knowledge/{Id(a)}", Dto("Archivé")));
        await Json(await m.PutAsJsonAsync($"/api/knowledge/{Id(a)}", Dto("Brouillon")));
    }

    [Fact]
    public async Task Probleme_resolu_seulement_apres_analyse_et_rouvrable_une_fois_clos()
    {
        var m = api.Client("Manager");
        object Dto(string? status) => new
        {
            title = "Transitions PRB", description = "d", status, impact = "Moyen", urgency = "Moyenne",
            category = "c", affectedService = "s", knownErrorWorkaround = (string?)null, rootCause = "Cause établie",
            closureCode = "Corrigé"
        };
        var p = await Json(await m.PostAsJsonAsync("/api/problems", Dto(null)));
        await Refused(await m.PutAsJsonAsync($"/api/problems/{Id(p)}", Dto("Résolu")));
        foreach (var status in new[] { "En analyse", "Résolu", "Clos", "En analyse" })
            await Json(await m.PutAsJsonAsync($"/api/problems/{Id(p)}", Dto(status)));
    }

    [Theory]
    [InlineData("/api/incidents", "Clos")]
    [InlineData("/api/requests", "Close")]
    [InlineData("/api/changes", "Clos")]
    [InlineData("/api/configuration-items", "Retiré")]
    [InlineData("/api/services", "Retiré")]
    [InlineData("/api/agreements", "Expiré")]
    [InlineData("/api/improvements", "Réalisée")]
    [InlineData("/api/problems", null)]
    public async Task Chaque_pratique_expose_son_graphe_de_transitions(string path, string? final)
    {
        var graph = await Json(await api.Client("User").GetAsync($"{path}/transitions"));
        Assert.True(graph.EnumerateObject().Count() >= 3);
        if (final is not null) Assert.Empty(graph.GetProperty(final).EnumerateArray());
    }
}
