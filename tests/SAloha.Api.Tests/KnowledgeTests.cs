using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SAloha.Api.Tests;

/// <summary>Connaissances au-delà du MVP : KB-11, KB-12, KB-14, KB-17, KB-18, KB-21.</summary>
[Collection("api")]
public class KnowledgeTests(ApiFixture api)
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
        Assert.True(res.StatusCode == HttpStatusCode.BadRequest, $"Attendu 400, reçu {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("message").GetString()!;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();
    private static string Ref(JsonElement e) => e.GetProperty("reference").GetString()!;
    private static bool IsNull(JsonElement e, string p) => e.GetProperty(p).ValueKind == JsonValueKind.Null;

    private static object Article(string title, string? status = null, string content = "## Étapes\n1. Redémarrer",
        string type = "Solution", DateTime? review = null, string? owner = null) => new
    {
        title, description = "Résumé", status, articleType = type, content, keywords = "test", reviewDate = review,
        ownerType = owner is null ? null : "User", ownerId = owner, ownerDisplayName = owner
    };

    private static IEnumerable<(string Ref, string Status)> Lines(JsonElement console, string group) =>
        console.GetProperty("management").GetProperty(group).EnumerateArray()
            .Select(x => (x.GetProperty("reference").GetString()!, x.GetProperty("status").GetString()!));

    [Fact]
    public async Task KB_11_sans_date_de_revue_la_publication_la_fixe_a_un_an()
    {
        var m = api.Client("Manager");
        var a = await Json(await m.PostAsJsonAsync("/api/knowledge", Article("KB-11")));
        var published = await Json(await m.PutAsJsonAsync($"/api/knowledge/{Id(a)}", Article("KB-11", "Publié")));
        Assert.Equal(DateTime.UtcNow.Date.AddMonths(12), published.GetProperty("reviewDate").GetDateTime().Date);

        var fixedDate = DateTime.UtcNow.Date.AddMonths(3);
        var b = await Json(await m.PostAsJsonAsync("/api/knowledge", Article("KB-11 bis", review: fixedDate)));
        var kept = await Json(await m.PutAsJsonAsync($"/api/knowledge/{Id(b)}", Article("KB-11 bis", "Publié", review: fixedDate)));
        Assert.Equal(fixedDate, kept.GetProperty("reviewDate").GetDateTime().Date);
    }

    [Fact]
    public async Task KB_18_archiver_et_reactiver_sont_des_decisions_de_gestionnaire()
    {
        var u = api.Client("User", "redacteur.kb18");
        var m = api.Client("Manager");
        var a = await Json(await u.PostAsJsonAsync("/api/knowledge", Article("KB-18")));
        var url = $"/api/knowledge/{Id(a)}";

        Assert.Equal(HttpStatusCode.Forbidden, (await u.PutAsJsonAsync(url, Article("KB-18", "Archivé"))).StatusCode);
        await Json(await m.PutAsJsonAsync(url, Article("KB-18", "Archivé")));
        Assert.Equal(HttpStatusCode.Forbidden, (await u.PutAsJsonAsync(url, Article("KB-18", "Brouillon"))).StatusCode);
        Assert.Equal("Brouillon", (await Json(await m.PutAsJsonAsync(url, Article("KB-18", "Brouillon")))).GetProperty("status").GetString());

        // La retouche d'un article publié par un User le ramène toujours en brouillon (KB-03).
        await Json(await m.PutAsJsonAsync(url, Article("KB-18", "Publié")));
        var demoted = await Json(await u.PutAsJsonAsync(url, Article("KB-18 retouché", "Publié")));
        Assert.Equal("Brouillon", demoted.GetProperty("status").GetString());
    }

    [Fact]
    public async Task KB_21_un_brouillon_soumis_remonte_dans_les_decisions()
    {
        var u = api.Client("User", "redacteur.kb21");
        var m = api.Client("Manager");
        var empty = await Json(await u.PostAsJsonAsync("/api/knowledge", Article("KB-21 vide", content: "")));
        Assert.Contains("contenu", await Refused(await u.PostAsync($"/api/knowledge/{Id(empty)}/submit", null)));

        var a = await Json(await u.PostAsJsonAsync("/api/knowledge", Article("KB-21")));
        var submitted = await Json(await u.PostAsync($"/api/knowledge/{Id(a)}/submit", null));
        Assert.False(IsNull(submitted, "submittedAt"));
        Assert.Contains((Ref(a), "À publier"), Lines(await Json(await m.GetAsync("/api/console")), "decisions"));

        var published = await Json(await m.PutAsJsonAsync($"/api/knowledge/{Id(a)}", Article("KB-21", "Publié")));
        Assert.True(IsNull(published, "submittedAt"));
        Assert.DoesNotContain(Ref(a), Lines(await Json(await m.GetAsync("/api/console")), "decisions").Select(l => l.Ref));
    }

    [Fact]
    public async Task KB_17_revue_confirmee_par_le_responsable_ou_archivage_demande()
    {
        var owner = api.Client("User", "responsable.kb17");
        var other = api.Client("User", "autre.kb17");
        var m = api.Client("Manager");
        var a = await Json(await m.PostAsJsonAsync("/api/knowledge", Article("KB-17", owner: "responsable.kb17")));
        var url = $"/api/knowledge/{Id(a)}";
        Assert.Contains("publié", await Refused(await owner.PostAsJsonAsync($"{url}/review", new { nextReviewDate = (DateTime?)null })));
        await Json(await m.PutAsJsonAsync(url, Article("KB-17", "Publié", owner: "responsable.kb17")));

        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync($"{url}/review", new { nextReviewDate = (DateTime?)null })).StatusCode);
        await Refused(await owner.PostAsJsonAsync($"{url}/review", new { nextReviewDate = DateTime.UtcNow.AddDays(-1) }));
        var next = DateTime.UtcNow.Date.AddMonths(6);
        var reviewed = await Json(await owner.PostAsJsonAsync($"{url}/review", new { nextReviewDate = next }));
        Assert.Equal("Publié", reviewed.GetProperty("status").GetString());
        Assert.Equal(next, reviewed.GetProperty("reviewDate").GetDateTime().Date);
        Assert.Equal("responsable.kb17", reviewed.GetProperty("lastReviewedBy").GetString());

        Assert.Contains("Motiver", await Refused(await owner.PostAsJsonAsync($"{url}/archive-request", new { reason = " " })));
        await Json(await owner.PostAsJsonAsync($"{url}/archive-request", new { reason = "Procédure remplacée par KB-xxx" }));
        Assert.Contains((Ref(a), "Archivage demandé"), Lines(await Json(await m.GetAsync("/api/console")), "decisions"));
        var archived = await Json(await m.PutAsJsonAsync(url, Article("KB-17", "Archivé", owner: "responsable.kb17")));
        Assert.True(IsNull(archived, "archiveRequestedAt"));
    }

    [Fact]
    public async Task KB_14_et_KB_12_utilisation_et_article_d_erreur_connue_a_archiver()
    {
        var m = api.Client("Manager");
        var a = await Json(await m.PostAsJsonAsync("/api/knowledge", Article("KB-12 erreur connue", type: "Erreur connue")));
        await Json(await m.PutAsJsonAsync($"/api/knowledge/{Id(a)}", Article("KB-12 erreur connue", "Publié", type: "Erreur connue")));

        // KB-14 : un incident résolu grâce à l'article.
        var inc = await Json(await m.PostAsJsonAsync("/api/incidents", new { title = "KB-14", impact = "Faible", urgency = "Faible" }));
        await Json(await m.PostAsJsonAsync("/api/links", new { fromType = "incident", fromId = Id(inc), toReference = Ref(a) }));
        var usage = await Json(await m.GetAsync($"/api/knowledge/{Id(a)}/usage"));
        Assert.Equal(1, usage.GetProperty("incidents").GetInt32());
        Assert.Equal(0, usage.GetProperty("requests").GetInt32());

        // KB-12 : le problème relié est clos « Corrigé » → l'article est proposé à l'archivage.
        object P(string? status, string? closure = null) => new
        {
            title = "KB-12", description = "d", status, impact = "Moyen", urgency = "Moyenne", category = "c", affectedService = "s",
            knownErrorWorkaround = "Relancer", rootCause = status is "Résolu" or "Clos" ? "Fuite" : null, closureCode = closure,
            source = "Proactif"
        };
        var p = await Json(await m.PostAsJsonAsync("/api/problems", P(null)));
        var url = $"/api/problems/{Id(p)}";
        await Json(await m.PostAsJsonAsync("/api/links", new { fromType = "problem", fromId = Id(p), toReference = Ref(a) }));
        Assert.DoesNotContain(Ref(a), Lines(await Json(await m.GetAsync("/api/console")), "reviewsDue").Select(l => l.Ref));
        await Json(await m.PutAsJsonAsync(url, P("En analyse")));
        await Json(await m.PutAsJsonAsync(url, P("Erreur connue")));
        var action = await Json(await m.PostAsJsonAsync($"{url}/actions", new
        {
            title = "Corriger", description = "", dueDate = (DateTime?)null,
            raci = new[]
            {
                new { role = "R", assigneeType = "User", assigneeId = "fara.rasoa", assigneeDisplayName = "Fara" },
                new { role = "A", assigneeType = "User", assigneeId = "lova.rabe", assigneeDisplayName = "Lova" }
            }
        }));
        await Json(await m.PutAsJsonAsync($"/api/actions/{Id(action)}", new
        {
            title = "Corriger", description = "", status = "Terminée", dueDate = (DateTime?)null,
            raci = new[]
            {
                new { role = "R", assigneeType = "User", assigneeId = "fara.rasoa", assigneeDisplayName = "Fara" },
                new { role = "A", assigneeType = "User", assigneeId = "lova.rabe", assigneeDisplayName = "Lova" }
            }
        }));
        await Json(await m.PutAsJsonAsync(url, P("Résolu")));
        await Json(await m.PutAsJsonAsync(url, P("Clos", "Corrigé")));
        Assert.Contains((Ref(a), "À archiver : problème corrigé"), Lines(await Json(await m.GetAsync("/api/console")), "reviewsDue"));
    }
}
