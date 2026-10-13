using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SAloha.Api.Tests;

/// <summary>SOC-24 : commentaires publics et notes de travail.</summary>
[Collection("api")]
public class CommentsTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();
    private static List<string> Bodies(JsonElement list) =>
        list.GetProperty("comments").EnumerateArray().Select(c => c.GetProperty("body").GetString()!).ToList();

    [Fact]
    public async Task SOC_24_le_demandeur_voit_le_public_pas_les_notes_de_travail()
    {
        var demandeur = api.Client("User", "demandeur.soc24");
        var tech = api.Client("User", "tech.soc24");
        var m = api.Client("Manager", "gestionnaire.soc24");
        var req = await Json(await demandeur.PostAsJsonAsync("/api/requests", new { title = "Accès VPN", requestedItem = "VPN", status = (string?)null }));
        // Le technicien en devient responsable.
        await Json(await m.PutAsJsonAsync($"/api/requests/{Id(req)}", new
        {
            title = "Accès VPN", requestedItem = "VPN", status = (string?)null,
            ownerType = "User", ownerId = "tech.soc24", ownerDisplayName = "Tech"
        }));
        string url(int id) => $"/api/comments?type=request&id={id}";

        await Json(await demandeur.PostAsJsonAsync("/api/comments", new { type = "request", id = Id(req), body = "C'est urgent." }));
        await Json(await tech.PostAsJsonAsync("/api/comments", new { type = "request", id = Id(req), visibility = "Note", body = "Licence à commander." }));
        await Json(await tech.PostAsJsonAsync("/api/comments", new { type = "request", id = Id(req), visibility = "Public", body = "Pris en charge." }));

        var seenByRequester = await Json(await demandeur.GetAsync(url(Id(req))));
        Assert.Equal(["C'est urgent.", "Pris en charge."], Bodies(seenByRequester));
        Assert.False(seenByRequester.GetProperty("canWriteNotes").GetBoolean());
        // Le demandeur n'écrit pas de note de travail.
        Assert.Equal(HttpStatusCode.Forbidden, (await demandeur.PostAsJsonAsync("/api/comments",
            new { type = "request", id = Id(req), visibility = "Note", body = "?" })).StatusCode);

        var seenByTech = await Json(await tech.GetAsync(url(Id(req))));
        Assert.Equal(3, Bodies(seenByTech).Count);
        Assert.True(seenByTech.GetProperty("canWriteNotes").GetBoolean());
        Assert.Equal(3, Bodies(await Json(await m.GetAsync(url(Id(req))))).Count);
    }

    [Fact]
    public async Task SOC_24_controles_et_suppression()
    {
        var auteur = api.Client("User", "auteur.soc24");
        var autre = api.Client("User", "autre.soc24");
        var inc = await Json(await auteur.PostAsJsonAsync("/api/incidents", new { title = "Écran noir", impact = "Faible", urgency = "Faible" }));
        Assert.Equal(HttpStatusCode.BadRequest, (await auteur.PostAsJsonAsync("/api/comments", new { type = "incident", id = Id(inc), body = "  " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await auteur.PostAsJsonAsync("/api/comments",
            new { type = "incident", id = Id(inc), body = new string('x', 4001) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await auteur.PostAsJsonAsync("/api/comments",
            new { type = "incident", id = Id(inc), visibility = "Secret", body = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await auteur.GetAsync("/api/comments?type=inconnu&id=1")).StatusCode);

        var c = await Json(await auteur.PostAsJsonAsync("/api/comments", new { type = "incident", id = Id(inc), body = "Redémarré sans effet." }));
        Assert.Equal(HttpStatusCode.Forbidden, (await autre.DeleteAsync($"/api/comments/{Id(c)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await auteur.DeleteAsync($"/api/comments/{Id(c)}")).StatusCode);

        // Supprimer l'enregistrement emporte ses commentaires.
        await Json(await auteur.PostAsJsonAsync("/api/comments", new { type = "incident", id = Id(inc), body = "Encore." }));
        Assert.Equal(HttpStatusCode.NoContent, (await api.Client().DeleteAsync($"/api/incidents/{Id(inc)}")).StatusCode);
        await using var db = api.Database();
        Assert.False(db.Comments.Any(x => x.TargetType == "incident" && x.TargetId == Id(inc)));
    }

    [Fact]
    public async Task SOC_24_hors_de_ses_perimetres_rien()
    {
        var admin = api.Client();
        var usine = Id(await Json(await admin.PostAsJsonAsync("/api/admin/perimeters", new { name = "Usine SOC-24" })));
        await Json(await admin.PostAsJsonAsync($"/api/admin/perimeters/{usine}/assignments",
            new { principalType = "User", principalId = "usine.soc24", principalDisplayName = "U", role = "User" }));
        var inc = await Json(await admin.PostAsJsonAsync("/api/incidents", new { title = "Presse", impact = "Faible", urgency = "Faible", perimeterId = usine }));
        var c = await Json(await admin.PostAsJsonAsync("/api/comments", new { type = "incident", id = Id(inc), body = "Visible de l'Usine seulement." }));
        var dehors = api.Client("User", "siege.soc24");   // sans affectation : Organisation seulement
        Assert.Equal(HttpStatusCode.NotFound, (await dehors.GetAsync($"/api/comments?type=incident&id={Id(inc)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await dehors.DeleteAsync($"/api/comments/{Id(c)}")).StatusCode);
        Assert.Single(Bodies(await Json(await api.Client("User", "usine.soc24").GetAsync($"/api/comments?type=incident&id={Id(inc)}"))));
    }
}
