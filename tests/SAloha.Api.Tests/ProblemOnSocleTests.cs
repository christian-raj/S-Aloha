using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SAloha.Api.Tests;

/// <summary>Problèmes sur le socle commun : gestionnaire du problème (PRB-21), droits et registre du socle.</summary>
[Collection("api")]
public class ProblemOnSocleTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();
    private static string Ref(JsonElement e) => e.GetProperty("reference").GetString()!;

    private static object Problem(string title, string? status = null, string? ownerType = null, string? ownerId = null) => new
    {
        title, description = "d", status, impact = "Moyen", urgency = "Moyenne", category = "c", affectedService = "s",
        knownErrorWorkaround = (string?)null, rootCause = (string?)null, ownerType, ownerId, ownerDisplayName = ownerId
    };

    [Fact]
    public async Task PRB_21_le_gestionnaire_qui_qualifie_devient_gestionnaire_du_probleme()
    {
        var user = api.Client("User", "declarant.prb21");
        var m = api.Client("Manager", "gestionnaire.prb21");
        var p = await Json(await user.PostAsJsonAsync("/api/problems", Problem("PRB-21")));
        Assert.Equal(JsonValueKind.Null, p.GetProperty("ownerId").ValueKind);

        // Un User déclare mais ne qualifie pas (tableau des droits).
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PutAsJsonAsync($"/api/problems/{Id(p)}", Problem("PRB-21", "En analyse"))).StatusCode);

        var qualified = await Json(await m.PutAsJsonAsync($"/api/problems/{Id(p)}", Problem("PRB-21", "En analyse")));
        Assert.Equal("gestionnaire.prb21", qualified.GetProperty("ownerId").GetString());
        var console = await Json(await m.GetAsync("/api/console"));
        Assert.Contains(console.GetProperty("myAssignments").EnumerateArray(), x => x.GetProperty("reference").GetString() == Ref(p));
    }

    [Fact]
    public async Task PRB_21_un_groupe_gestionnaire_et_le_filtre_des_miens()
    {
        var m = api.Client("Manager", "gestionnaire.prb21b");
        var membre = api.Client("User", "membre.prb21b", "GRP-PROBLEMES-PRB21");
        var p = await Json(await m.PostAsJsonAsync("/api/problems", Problem("PRB-21 groupe")));
        await Json(await m.PutAsJsonAsync($"/api/problems/{Id(p)}", Problem("PRB-21 groupe", "En analyse", "Group", "GRP-PROBLEMES-PRB21")));
        var mine = await Json(await membre.GetAsync("/api/problems?owner=me"));
        Assert.Contains(mine.EnumerateArray(), x => Ref(x) == Ref(p));
    }

    [Fact]
    public async Task La_transition_forcee_du_socle_vaut_pour_les_problemes()
    {
        var admin = api.Client();
        var p = await Json(await admin.PostAsJsonAsync("/api/problems", Problem("Forçage")));
        var forced = await Json(await admin.PutAsJsonAsync($"/api/problems/{Id(p)}?force=true&reason=Saisie%20tardive",
            new
            {
                title = "Forçage", description = "d", status = "Erreur connue", impact = "Moyen", urgency = "Moyenne", category = "c",
                affectedService = "s", knownErrorWorkaround = "Relancer", rootCause = (string?)null
            }));
        Assert.Equal("Erreur connue", forced.GetProperty("status").GetString());
        var journal = await Json(await admin.GetAsync($"/api/audit?type=problem&id={Id(p)}"));
        Assert.Contains(journal.EnumerateArray(), e => e.GetProperty("action").GetString() == "Transition forcée"
            && e.GetProperty("reason").GetString() == "Saisie tardive");
    }
}
