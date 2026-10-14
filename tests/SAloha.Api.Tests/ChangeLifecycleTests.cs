using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SAloha.Api.Tests;

/// <summary>Changements, sortie du MVP : dates réelles (CHG-14), revue post-implémentation (CHG-15), CI reliés (CHG-16), séparation des tâches (CHG-21).</summary>
[Collection("api")]
public class ChangeLifecycleTests(ApiFixture api)
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
    private static readonly DateTime Slot = DateTime.UtcNow.Date.AddDays(60).AddHours(20);

    private static object Change(string type, string? status, DateTime? actualStart = null, DateTime? actualEnd = null,
        string? outcome = null, string? pir = null) => new
    {
        title = "CHG cycle", changeType = type, risk = "Moyen", status, outcome, pirNotes = pir,
        plannedStart = Slot, plannedEnd = Slot.AddHours(2), implementationPlan = "Appliquer", backoutPlan = "Restaurer",
        actualStart, actualEnd
    };

    /// <summary>Changement Normal ou Urgent conduit jusqu'à Planifié : demandeur, CI relié, autorisé par un autre gestionnaire.</summary>
    private async Task<(HttpClient Requester, string Url)> Planned(string type, string tag)
    {
        var requester = api.Client("Manager", $"demandeur.{tag}");
        var c = await Json(await requester.PostAsJsonAsync("/api/changes", Change(type, null)));
        var url = $"/api/changes/{Id(c)}";
        await ChangeTestData.LinkCiAsync(requester, Id(c));
        await Json(await requester.PutAsJsonAsync(url, Change(type, "Évalué")));
        await Json(await api.Client("Manager", $"autorite.{tag}").PutAsJsonAsync(url, Change(type, "Autorisé")));
        await Json(await requester.PutAsJsonAsync(url, Change(type, "Planifié")));
        return (requester, url);
    }

    [Fact]
    public async Task CHG_16_un_changement_normal_s_evalue_sur_ses_CI()
    {
        var m = api.Client("Manager", "chg16.manager");
        var c = await Json(await m.PostAsJsonAsync("/api/changes", Change("Normal", null)));
        Assert.Contains("CI", await Refused(await m.PutAsJsonAsync($"/api/changes/{Id(c)}", Change("Normal", "Évalué"))));
        await ChangeTestData.LinkCiAsync(m, Id(c));
        await Json(await m.PutAsJsonAsync($"/api/changes/{Id(c)}", Change("Normal", "Évalué")));
    }

    [Fact]
    public async Task CHG_21_le_demandeur_n_autorise_pas_son_propre_changement()
    {
        var m = api.Client("Manager", "chg21.manager");
        var c = await Json(await m.PostAsJsonAsync("/api/changes", Change("Normal", null)));
        var url = $"/api/changes/{Id(c)}";
        await ChangeTestData.LinkCiAsync(m, Id(c));
        await Json(await m.PutAsJsonAsync(url, Change("Normal", "Évalué")));
        Assert.Contains("ne peut pas l'autoriser lui-même", await Refused(await m.PutAsJsonAsync(url, Change("Normal", "Autorisé"))));
        // L'Admin, lui, le peut (sa demande et son autorisation restent tracées).
        var admin = api.Client("Admin", "chg21.admin");
        var own = await Json(await admin.PostAsJsonAsync("/api/changes", Change("Normal", null)));
        await ChangeTestData.LinkCiAsync(admin, Id(own));
        await Json(await admin.PutAsJsonAsync($"/api/changes/{Id(own)}", Change("Normal", "Évalué")));
        await Json(await admin.PutAsJsonAsync($"/api/changes/{Id(own)}", Change("Normal", "Autorisé")));
    }

    [Fact]
    public async Task CHG_14_dates_reelles_et_depassement()
    {
        var (r, url) = await Planned("Normal", "chg14");
        Assert.Contains("début réel", await Refused(await r.PutAsJsonAsync(url, Change("Normal", "Mis en œuvre"))));
        Assert.Contains("fin réelle précède", await Refused(await r.PutAsJsonAsync(url, Change("Normal", "Mis en œuvre", Slot, Slot.AddHours(-1)))));
        await Json(await r.PutAsJsonAsync(url, Change("Normal", "Mis en œuvre", Slot)));
        Assert.Contains("fin réelle", await Refused(await r.PutAsJsonAsync(url, Change("Normal", "Clos", Slot, outcome: "Réussi"))));
        // Fini une heure après la fin planifiée : dépassement signalé.
        var closed = await Json(await r.PutAsJsonAsync(url, Change("Normal", "Clos", Slot, Slot.AddHours(3), "Réussi")));
        Assert.True(closed.GetProperty("overrun").GetBoolean());
    }

    [Fact]
    public async Task CHG_15_revue_post_implementation_d_un_changement_urgent_ou_echoue()
    {
        var (r, url) = await Planned("Normal", "chg15");
        await Json(await r.PutAsJsonAsync(url, Change("Normal", "Mis en œuvre", Slot)));
        Assert.Contains("revue post-implémentation", await Refused(await r.PutAsJsonAsync(url, Change("Normal", "Clos", Slot, Slot.AddHours(1), "Échoué"))));
        var closed = await Json(await r.PutAsJsonAsync(url, Change("Normal", "Clos", Slot, Slot.AddHours(1), "Échoué", "Retour arrière appliqué ; test de charge manquant.")));
        Assert.False(closed.GetProperty("overrun").GetBoolean());

        var (u, urgent) = await Planned("Urgent", "chg15u");
        await Json(await u.PutAsJsonAsync(urgent, Change("Urgent", "Mis en œuvre", Slot)));
        Assert.Contains("revue post-implémentation", await Refused(await u.PutAsJsonAsync(urgent, Change("Urgent", "Clos", Slot, Slot.AddHours(1), "Réussi"))));
    }
}
