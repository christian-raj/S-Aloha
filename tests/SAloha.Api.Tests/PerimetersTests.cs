using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SAloha.Api.Tests;

/// <summary>Étape 6 du lot 1, modèle et administration des périmètres (PER-01, PER-02, PER-10 à PER-12).</summary>
[Collection("api")]
public class PerimetersTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return body == "" ? default : JsonDocument.Parse(body).RootElement;
    }

    private static async Task<string> Refused(HttpResponseMessage res, HttpStatusCode code)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == code, $"Attendu {(int)code}, reçu {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("message").GetString()!;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();

    [Fact]
    public async Task PER_10_les_enregistrements_sont_dans_Organisation_par_defaut()
    {
        var u = api.Client("User", "user.per10");
        var inc = await Json(await u.PostAsJsonAsync("/api/incidents", new { title = "PER-10", impact = "Faible", urgency = "Faible" }));
        Assert.Equal(1, inc.GetProperty("perimeterId").GetInt32());
        var visible = await Json(await u.GetAsync("/api/perimeters"));
        Assert.Contains(visible.EnumerateArray(), p => p.GetProperty("name").GetString() == "Organisation");

        var admin = api.Client();
        var org = await Json(await admin.GetAsync("/api/admin/perimeters/1"));
        Assert.True(org.GetProperty("isDefault").GetBoolean());
        Assert.True(org.GetProperty("counts").EnumerateArray().Single(c => c.GetProperty("practice").GetString() == "Incidents")
            .GetProperty("count").GetInt32() >= 1);                                                    // PER-12
        await Refused(await admin.DeleteAsync("/api/admin/perimeters/1"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PER_01_gestion_des_perimetres_par_l_Admin()
    {
        var admin = api.Client();
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client("Manager").PostAsJsonAsync("/api/admin/perimeters", new { name = "Interdit" })).StatusCode);

        var siege = await Json(await admin.PostAsJsonAsync("/api/admin/perimeters", new { name = "Siège PER-01", description = "Direction générale" }));
        Assert.Contains("existe déjà", await Refused(await admin.PostAsJsonAsync("/api/admin/perimeters", new { name = "siège per-01" }), HttpStatusCode.Conflict));
        await Refused(await admin.PostAsJsonAsync("/api/admin/perimeters", new { name = "  " }), HttpStatusCode.BadRequest);
        var renamed = await Json(await admin.PutAsJsonAsync($"/api/admin/perimeters/{Id(siege)}", new { name = "Siège PER-01 (Antananarivo)", description = "DG" }));
        Assert.Equal("Siège PER-01 (Antananarivo)", renamed.GetProperty("name").GetString());

        // Suppression douce : hors des sélecteurs, restaurable ; un homonyme est refusé.
        await Json(await admin.DeleteAsync($"/api/admin/perimeters/{Id(siege)}"));
        var visible = await Json(await api.Client("User").GetAsync("/api/perimeters"));
        Assert.DoesNotContain(visible.EnumerateArray(), p => Id(p) == Id(siege));
        Assert.Contains("restaurez", await Refused(await admin.PostAsJsonAsync("/api/admin/perimeters", new { name = "Siège PER-01 (Antananarivo)" }), HttpStatusCode.Conflict));
        var restored = await Json(await admin.PostAsync($"/api/admin/perimeters/{Id(siege)}/restore", null));
        Assert.Equal(JsonValueKind.Null, restored.GetProperty("deletedAt").ValueKind);

        // Suppression définitive : refusée tant qu'une affectation reste (409, compteurs).
        var a = await Json(await admin.PostAsJsonAsync($"/api/admin/perimeters/{Id(siege)}/assignments",
            new { principalType = "User", principalId = "lova", principalDisplayName = "Lova", role = "Manager" }));
        var conflict = await admin.DeleteAsync($"/api/admin/perimeters/{Id(siege)}/purge");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(1, JsonDocument.Parse(await conflict.Content.ReadAsStringAsync()).RootElement.GetProperty("assignments").GetInt32());
        await Json(await admin.DeleteAsync($"/api/admin/assignments/{Id(a)}"));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/perimeters/{Id(siege)}/purge")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/perimeters/{Id(siege)}")).StatusCode);
    }

    [Fact]
    public async Task PER_02_affectations_utilisateurs_et_groupes_jamais_d_Admin()
    {
        var admin = api.Client();
        var usine = await Json(await admin.PostAsJsonAsync("/api/admin/perimeters", new { name = "Usine PER-02" }));
        var url = $"/api/admin/perimeters/{Id(usine)}/assignments";

        await Json(await admin.PostAsJsonAsync(url, new { principalType = "Group", principalId = "GRP-USINE", principalDisplayName = "Équipe usine", role = "User" }));
        await Json(await admin.PostAsJsonAsync(url, new { principalType = "User", principalId = "lova", principalDisplayName = "Lova", role = "Manager" }));
        await Json(await admin.PostAsJsonAsync(url, new { principalType = "User", principalId = "lova", principalDisplayName = "Lova", role = "User" }));
        await Refused(await admin.PostAsJsonAsync(url, new { principalType = "User", principalId = "LOVA", role = "Manager" }), HttpStatusCode.Conflict);
        Assert.Contains("portée globale", await Refused(await admin.PostAsJsonAsync(url,
            new { principalType = "User", principalId = "admin.x", role = "Admin" }), HttpStatusCode.BadRequest));
        await Refused(await admin.PostAsJsonAsync(url, new { principalType = "Machine", principalId = "x", role = "User" }), HttpStatusCode.BadRequest);

        var list = await Json(await admin.GetAsync(url));
        Assert.Equal(3, list.GetArrayLength());
        var detail = await Json(await admin.GetAsync($"/api/admin/perimeters/{Id(usine)}"));
        Assert.Equal(3, detail.GetProperty("assignments").GetArrayLength());
    }

    [Fact]
    public async Task PER_11_perimetres_et_affectations_traces_au_journal()
    {
        var admin = api.Client();
        var p = await Json(await admin.PostAsJsonAsync("/api/admin/perimeters", new { name = "Site PER-11" }));
        await Json(await admin.PutAsJsonAsync($"/api/admin/perimeters/{Id(p)}", new { name = "Site PER-11 bis" }));
        await Json(await admin.PostAsJsonAsync($"/api/admin/perimeters/{Id(p)}/assignments",
            new { principalType = "Group", principalId = "GRP-SITE", principalDisplayName = "Équipe site", role = "User" }));
        await Json(await admin.DeleteAsync($"/api/admin/perimeters/{Id(p)}"));

        var journal = (await Json(await admin.GetAsync($"/api/audit?type=perimeter&id={Id(p)}"))).EnumerateArray()
            .Select(e => (Action: e.GetProperty("action").GetString(), Field: e.TryGetProperty("field", out var f) ? f.GetString() : null)).ToList();
        Assert.Contains(journal, e => e.Action == "Création" && e.Field == null);
        Assert.Contains(journal, e => e.Action == "Modification" && e.Field == "name");
        Assert.Contains(journal, e => e.Action == "Création" && e.Field!.StartsWith("affectation User de Équipe site"));
        Assert.Contains(journal, e => e.Action == "Suppression" && e.Field == "douce");
    }
}
