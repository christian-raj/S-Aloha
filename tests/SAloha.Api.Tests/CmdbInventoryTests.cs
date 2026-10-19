using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SAloha.Api.Tests;

/// <summary>
/// Portefeuille applicatif (CFG-43, CFG-48), actifs matériels (CFG-44), fins de support
/// (CFG-47), rapport d'inventaire (CFG-39) et onglet Configuration du tableau de bord (REP-50 à REP-53).
/// </summary>
[Collection("api")]
public class CmdbInventoryTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();
    private static string Str(JsonElement e, string p) => e.GetProperty(p).GetString()!;

    private static Dictionary<string, object?> App(string tag, string status = "En service") => new()
    {
        ["title"] = $"ERP {tag}", ["ciType"] = "Application", ["environment"] = "Production", ["status"] = status,
        ["ownerType"] = "User", ["ownerId"] = $"owner.{tag}", ["ownerDisplayName"] = "Owner",
        ["appCode"] = $"erp-{tag}", ["businessUnit"] = "Direction financière",
        ["businessOwnerType"] = "User", ["businessOwnerId"] = $"metier.{tag}", ["businessOwnerDisplayName"] = "Métier",
        ["technicalOwnerType"] = "Group", ["technicalOwnerId"] = $"GRP-TMA-{tag}", ["technicalOwnerDisplayName"] = "TMA",
        ["criticality"] = "Vitale", ["rtoHours"] = 4, ["rpoHours"] = 1, ["gdprSensitive"] = true, ["nis2Scope"] = true,
    };

    private static Dictionary<string, object?> Without(Dictionary<string, object?> d, params string[] keys)
    {
        var copy = new Dictionary<string, object?>(d);
        foreach (var k in keys) copy.Remove(k);
        return copy;
    }

    [Fact]
    public async Task CFG_43_CFG_48_une_application_en_service_porte_sa_gouvernance_et_un_cycle_coherent()
    {
        var m = api.Client("Manager", "chef.cfg43");
        // Planifiée : la gouvernance peut attendre (le scénario CFG-43).
        var planned = await Json(await m.PostAsJsonAsync("/api/configuration-items",
            Without(App("cfg43p", "Planifié"), "appCode", "businessOwnerId")));
        Assert.Equal("Étude", Str(planned, "appLifecycle"));

        var missing = await m.PostAsJsonAsync("/api/configuration-items", Without(App("cfg43"), "appCode", "businessOwnerId"));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var message = await missing.Content.ReadAsStringAsync();
        Assert.Contains("code applicatif", message);
        Assert.Contains("propriétaire métier", message);

        var app = await Json(await m.PostAsJsonAsync("/api/configuration-items", App("cfg43")));
        Assert.Equal(("ERP-CFG43", "En production", "GRP-TMA-cfg43"), (Str(app, "appCode"), Str(app, "appLifecycle"), Str(app, "technicalOwnerId")));
        // Code applicatif unique parmi les CI non retirés (409).
        var twin = App("cfg43");
        twin["title"] = "ERP bis cfg43";
        Assert.Equal(HttpStatusCode.Conflict, (await m.PostAsJsonAsync("/api/configuration-items", twin)).StatusCode);

        // Cycle incompatible avec le statut : 400 ; quand seul le statut change, le cycle s'aligne.
        var wrong = App("cfg43");
        wrong["appLifecycle"] = "Étude";
        Assert.Equal(HttpStatusCode.BadRequest, (await m.PutAsJsonAsync($"/api/configuration-items/{Id(app)}", wrong)).StatusCode);
        var retire = App("cfg43", "Retiré");
        retire["appLifecycle"] = "En production";
        Assert.Equal("Retirée", Str(await Json(await m.PutAsJsonAsync($"/api/configuration-items/{Id(app)}", retire)), "appLifecycle"));
        // Retirée, l'application libère son code.
        await Json(await m.PostAsJsonAsync("/api/configuration-items", twin));
    }

    [Fact]
    public async Task CFG_44_un_equipement_physique_en_service_porte_constructeur_serie_et_emplacement()
    {
        var m = api.Client("Manager", "chef.cfg44");
        var dto = new Dictionary<string, object?>
        {
            ["title"] = "sw-cfg44", ["ciType"] = "Réseau", ["environment"] = "Production", ["status"] = "En service",
            ["ownerType"] = "User", ["ownerId"] = "owner.cfg44", ["ownerDisplayName"] = "Owner",
            ["vendor"] = "Cisco", ["model"] = "Catalyst 9200", ["location"] = "Siège > Salle 1 > Baie A > U12",
            ["macAddress"] = "001a.2b3c.4d44", ["ipAddress"] = "10.44.0.1",
        };
        var res = await m.PostAsJsonAsync("/api/configuration-items", dto);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("numéro de série", await res.Content.ReadAsStringAsync());

        dto["serialNumber"] = "FOC-CFG44";
        var sw = await Json(await m.PostAsJsonAsync("/api/configuration-items", dto));
        Assert.Equal("00:1A:2B:3C:4D:44", Str(sw, "macAddress"));
        dto["title"] = "sw-cfg44-bis";
        dto["serialNumber"] = "foc-cfg44";
        Assert.Equal(HttpStatusCode.Conflict, (await m.PostAsJsonAsync("/api/configuration-items", dto)).StatusCode);
        dto["macAddress"] = "pas-une-mac";
        Assert.Equal(HttpStatusCode.BadRequest, (await m.PostAsJsonAsync("/api/configuration-items", dto)).StatusCode);

        // Un serveur peut être une VM : pas de numéro de série exigé ; les champs matériels d'une application sont ignorés.
        var vm = await Json(await m.PostAsJsonAsync("/api/configuration-items", new
        {
            title = "vm-cfg44", ciType = "Serveur", environment = "Production", status = "En service",
            ownerType = "User", ownerId = "owner.cfg44", ownerDisplayName = "Owner"
        }));
        Assert.Equal(JsonValueKind.Null, vm.GetProperty("serialNumber").ValueKind);
        // Recherche par numéro de série et par adresse IP.
        Assert.Contains((await Json(await m.GetAsync("/api/configuration-items?q=FOC-CFG44"))).EnumerateArray(), c => Id(c) == Id(sw));
        Assert.Contains((await Json(await m.GetAsync("/api/configuration-items?q=10.44.0.1"))).EnumerateArray(), c => Id(c) == Id(sw));
    }

    [Fact]
    public async Task CFG_47_CFG_39_fins_de_support_et_rapport_d_inventaire()
    {
        var m = api.Client("Manager", "chef.cfg47");
        var today = DateTime.UtcNow.Date;
        var rule = await Json(await m.PostAsJsonAsync("/api/discovery/end-of-life",
            new { product = "Windows Server 2008 cfg47", endOfSupport = today.AddYears(-3), notes = "Plus de correctifs" }));
        Assert.Equal(HttpStatusCode.BadRequest, (await m.PostAsJsonAsync("/api/discovery/end-of-life",
            new { product = "windows server 2008 CFG47", endOfSupport = today })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client("User", "u.cfg47").PostAsJsonAsync("/api/discovery/end-of-life",
            new { product = "Autre cfg47", endOfSupport = today })).StatusCode);

        Dictionary<string, object?> Srv(string title) => new()
        {
            ["title"] = title, ["ciType"] = "Serveur", ["environment"] = "Production", ["status"] = "En service",
            ["ownerType"] = "User", ["ownerId"] = "owner.cfg47", ["ownerDisplayName"] = "Owner",
        };
        var oldDto = Srv("srv-old-cfg47");
        oldDto["operatingSystem"] = "Microsoft Windows Server 2008 cfg47 R2";
        oldDto["warrantyExpiry"] = today.AddDays(-1);
        var soonDto = Srv("srv-soon-cfg47");
        soonDto["endOfSupport"] = today.AddMonths(2);
        soonDto["warrantyExpiry"] = today.AddYears(2);
        var old = await Json(await m.PostAsJsonAsync("/api/configuration-items", oldDto));
        var soon = await Json(await m.PostAsJsonAsync("/api/configuration-items", soonDto));

        var inventory = await Json(await m.GetAsync("/api/configuration-items/inventory"));
        JsonElement Hw(JsonElement ci) => inventory.GetProperty("hardware").EnumerateArray().Single(h => Id(h) == Id(ci));
        Assert.Equal(("Fin de support dépassée", "Windows Server 2008 cfg47", "Expirée"),
            (Str(Hw(old), "supportState"), Str(Hw(old), "supportSource"), Str(Hw(old), "warranty")));
        Assert.Equal(("Fin de support proche", "Fiche du CI", "Sous garantie"),
            (Str(Hw(soon), "supportState"), Str(Hw(soon), "supportSource"), Str(Hw(soon), "warranty")));
        Assert.True(inventory.GetProperty("summary").GetProperty("supportPassed").GetInt32() >= 1);

        var dashboard = await Json(await m.GetAsync("/api/reports/dashboard/configuration"));
        Assert.True(dashboard.GetProperty("kpis").GetProperty("supportPassed").GetInt32() >= 1);
        Assert.Contains(dashboard.GetProperty("obsolete").EnumerateArray(), o => Id(o) == Id(old));
        Assert.NotEqual(JsonValueKind.Null, dashboard.GetProperty("staging").ValueKind);
        // Un utilisateur voit l'état de la CMDB, pas le sas de staging (réservé aux gestionnaires).
        var user = await Json(await api.Client("User", "u.cfg47").GetAsync("/api/reports/dashboard/configuration"));
        Assert.Equal(JsonValueKind.Null, user.GetProperty("staging").ValueKind);
        var overview = await Json(await m.GetAsync("/api/reports/dashboard/overview"));
        Assert.True(overview.GetProperty("badges").GetProperty("configuration").GetProperty("count").GetInt32() >= 1);

        Assert.Equal(HttpStatusCode.NoContent, (await m.DeleteAsync($"/api/discovery/end-of-life/{Id(rule)}")).StatusCode);
    }
}
