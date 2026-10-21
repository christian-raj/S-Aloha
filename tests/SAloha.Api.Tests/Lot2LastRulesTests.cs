using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SAloha.Api.Tests;

/// <summary>Dernières règles du lot 2 : SOC-15, CSI-22, NIS-23, CFG-18.</summary>
[Collection("api")]
public class Lot2LastRulesTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();
    private static string? Str(JsonElement e, string p) => e.GetProperty(p).GetString();

    private static object Improvement(string title, string? status = null, string? abandon = null) => new
    {
        title, description = "d", status, step = 1, priority = "Moyenne", benefit = "b", baseline = "x", target = "y", abandonReason = abandon
    };

    [Fact]
    public async Task SOC_15_un_enregistrement_termine_se_modifie_par_un_gestionnaire_un_User_le_rouvre()
    {
        var user = api.Client("User", "agent.soc15");
        var manager = api.Client("Manager", "chef.soc15");
        var i = await Json(await user.PostAsJsonAsync("/api/improvements", Improvement("Réduire les ressaisies")));
        var url = $"/api/improvements/{Id(i)}";
        await Json(await manager.PutAsJsonAsync(url, Improvement("Réduire les ressaisies", "Abandonnée", "Hors budget")));
        // Terminée : un User ne la modifie pas…
        var refused = await user.PutAsJsonAsync(url, Improvement("Réduire les ressaisies (précisé)", "Abandonnée", "Hors budget"));
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Contains("seul un gestionnaire", await refused.Content.ReadAsStringAsync());
        // … un gestionnaire, si ;
        await Json(await manager.PutAsJsonAsync(url, Improvement("Réduire les ressaisies (précisé)", "Abandonnée", "Hors budget")));
        // … et un User la reprend par la transition permise (Abandonnée → Proposée).
        Assert.Equal("Proposée", Str(await Json(await user.PutAsJsonAsync(url, Improvement("Réduire les ressaisies", "Proposée"))), "status"));
    }

    [Fact]
    public async Task CSI_22_une_amelioration_proposee_depuis_plus_de_30_jours_appelle_une_revue()
    {
        var m = api.Client("Manager", "chef.csi22");
        var old = await Json(await m.PostAsJsonAsync("/api/improvements", Improvement("Proposition ancienne CSI-22")));
        var fresh = await Json(await m.PostAsJsonAsync("/api/improvements", Improvement("Proposition récente CSI-22")));
        await using (var db = api.Database())
        {
            var e = await db.Improvements.SingleAsync(x => x.Id == Id(old));
            e.CreatedAt = DateTime.UtcNow.AddDays(-40);
            await db.SaveChangesAsync();
        }
        var decisions = (await Json(await m.GetAsync("/api/console"))).GetProperty("management").GetProperty("decisions").EnumerateArray().ToList();
        Assert.Equal("Proposée depuis 40 jours : revue du registre", Str(decisions.Single(d => Str(d, "type") == "improvement" && Id(d) == Id(old)), "status"));
        Assert.Equal("Proposée", Str(decisions.Single(d => Str(d, "type") == "improvement" && Id(d) == Id(fresh)), "status"));
    }

    [Fact]
    public async Task NIS_23_exigences_sans_texte_signalees_a_l_Admin()
    {
        await using (var db = api.Database())
            Assert.True(await db.SecurityRequirements.AnyAsync(r => r.Text == null || r.Text == ""));   // structure seule, sans import
        var admin = (await Json(await api.Client("Admin", "admin.nis23").GetAsync("/api/console"))).GetProperty("management");
        Assert.True(admin.GetProperty("referentialWithoutText").GetInt32() > 0);
        var manager = (await Json(await api.Client("Manager", "chef.nis23").GetAsync("/api/console"))).GetProperty("management");
        Assert.Equal(0, manager.GetProperty("referentialWithoutText").GetInt32());
    }

    [Fact]
    public async Task CFG_18_un_CI_hors_service_signale_son_service_degrade()
    {
        var m = api.Client("Manager", "chef.cfg18");
        object Ci(string title, string status) => new
        {
            title, ciType = "Serveur", environment = "Production", status, ownerType = "User", ownerId = "owner.cfg18", ownerDisplayName = "Owner"
        };
        var ci = await Json(await m.PostAsJsonAsync("/api/configuration-items", Ci("srv-cfg18", "En service")));
        var svc = await Json(await m.PostAsJsonAsync("/api/services", new
        {
            title = "Paie CFG-18", status = "En service", criticality = "Élevée", serviceHours = "24/7",
            ownerType = "User", ownerId = "svcowner.cfg18", ownerDisplayName = "Responsable"
        }));
        await Json(await m.PostAsJsonAsync("/api/links", new { fromType = "ci", fromId = Id(ci), toReference = Str(svc, "reference") }));
        Assert.False((await Json(await m.GetAsync($"/api/services/{Id(svc)}/degradation"))).GetProperty("degraded").GetBoolean());

        await Json(await m.PutAsJsonAsync($"/api/configuration-items/{Id(ci)}", Ci("srv-cfg18", "Hors service")));
        var degradation = await Json(await api.Client("User", "agent.cfg18").GetAsync($"/api/services/{Id(svc)}/degradation"));
        Assert.True(degradation.GetProperty("degraded").GetBoolean());
        Assert.Equal(Str(ci, "reference"), Str(Assert.Single(degradation.GetProperty("cis").EnumerateArray()), "reference"));
    }
}
