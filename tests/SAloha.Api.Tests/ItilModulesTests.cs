using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SAloha.Api.Tests;

/// <summary>Modules ITIL 4 (MVP) : références, valeurs fermées, décisions de gestionnaire, liens, console.</summary>
[Collection("api")]
public class ItilModulesTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static async Task<JsonElement> Create(HttpClient client, string path, object dto) =>
        await Json(await client.PostAsJsonAsync(path, dto));

    private static Task<HttpResponseMessage> Put(HttpClient client, string path, int id, object dto) =>
        client.PutAsJsonAsync($"{path}/{id}", dto);

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();
    private static string Str(JsonElement e, string p) => e.GetProperty(p).GetString()!;

    private static object Incident(string title, string? status = null, string? resolution = null,
        string? owner = null, bool major = false) => new
    {
        title, description = "d", status, impact = "Élevé", urgency = "Moyenne", category = "Réseau",
        affectedService = "Messagerie", isMajor = major, resolution,
        resolutionCode = resolution is null ? null : "Correctif appliqué",
        // Un responsable : la prise en charge (En cours) l'exige (INC-06).
        ownerType = "User", ownerId = owner ?? "hery.rakoto", ownerDisplayName = owner ?? "Hery Rakoto"
    };

    private static object Request(string title, string? status = null) =>
        new { title, status, requestedItem = "Accès VPN" };

    // Créneau fixe : un créneau qui bougerait à chaque appel serait une
    // modification du changement, qui retire son autorisation (F10).
    private static readonly DateTime Slot = DateTime.UtcNow.Date.AddDays(2).AddHours(20);

    private static object Change(string title, string type, string? status = null, string? outcome = null,
        string risk = "Moyen") => new
    {
        title, status, changeType = type, risk, outcome, plannedStart = Slot, plannedEnd = Slot.AddHours(2),
        implementationPlan = "Appliquer", backoutPlan = "Restaurer", rejectionReason = "Hors périmètre"
    };

    public static TheoryData<string, string, object> Modules => new()
    {
        { "/api/incidents", "INC", Incident("Smoke incident") },
        { "/api/requests", "REQ", Request("Smoke demande") },
        { "/api/changes", "CHG", Change("Smoke changement", "Normal") },
        { "/api/configuration-items", "CI", new { title = "srv-smoke", ciType = "Serveur", environment = "Production", ownerType = "User", ownerId = "hery.rakoto", ownerDisplayName = "Hery Rakoto" } },
        { "/api/services", "SVC", new { title = "Messagerie smoke", criticality = "Élevée" } },
        { "/api/knowledge", "KB", new { title = "Smoke article", articleType = "Solution", content = "c" } },
        { "/api/improvements", "AMI", new { title = "Smoke amélioration", step = 1, priority = "Moyenne" } },
    };

    [Theory]
    [MemberData(nameof(Modules))]
    public async Task Chaque_module_cree_lit_et_refuse_un_statut_inconnu(string path, string code, object dto)
    {
        var client = api.Client("Manager");
        var created = await Create(client, path, dto);
        Assert.Matches(new Regex($"^{code}-{DateTime.UtcNow.Year}-\\d{{4}}$"), Str(created, "reference"));

        var read = await Json(await client.GetAsync($"{path}/{Id(created)}"));
        Assert.Equal(Str(created, "reference"), Str(read, "reference"));

        var invalid = JsonSerializer.SerializeToElement(dto).Clone();
        var withBadStatus = JsonSerializer.Deserialize<Dictionary<string, object?>>(invalid.GetRawText())!;
        withBadStatus["status"] = "Inexistant";
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, path, Id(created), withBadStatus)).StatusCode);
    }

    [Fact]
    public async Task Valeur_fermee_invalide_refusee_a_la_creation()
    {
        var res = await api.Client().PostAsJsonAsync("/api/incidents",
            new { title = "x", impact = "Énorme", urgency = "Moyenne" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Creations_simultanees_d_incidents_toutes_abouties()
    {
        var client = api.Client();
        var results = await Task.WhenAll(Enumerable.Range(1, 8)
            .Select(i => client.PostAsJsonAsync("/api/incidents", Incident($"Simultané {i}"))));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var refs = await Task.WhenAll(results.Select(async r => Str(await Json(r), "reference")));
        Assert.Equal(refs.Length, refs.Distinct().Count());
    }

    [Fact]
    public async Task Incident_resolu_seulement_avec_resolution_et_rouvert_sans_date()
    {
        var client = api.Client("User");
        var i = await Create(client, "/api/incidents", Incident("Résolution"));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Put(client, "/api/incidents", Id(i), Incident("Résolution", "Résolu"))).StatusCode);

        var resolved = await Json(await Put(client, "/api/incidents", Id(i), Incident("Résolution", "Résolu", "Redémarrage")));
        Assert.NotEqual(JsonValueKind.Null, resolved.GetProperty("resolvedAt").ValueKind);

        var reopened = await Json(await Put(client, "/api/incidents", Id(i), Incident("Résolution", "En cours", "Redémarrage")));
        Assert.Equal(JsonValueKind.Null, reopened.GetProperty("resolvedAt").ValueKind);
    }

    [Fact]
    public async Task Demande_approuvee_par_un_gestionnaire_avant_traitement()
    {
        var user = api.Client("User");
        var r = await Create(user, "/api/requests", Request("Approbation"));
        Assert.Equal("Soumise", Str(r, "status"));

        Assert.Equal(HttpStatusCode.BadRequest, (await Put(user, "/api/requests", Id(r), Request("Approbation", "En cours"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Put(user, "/api/requests", Id(r), Request("Approbation", "Approuvée"))).StatusCode);

        var approved = await Json(await Put(api.Client("Manager"), "/api/requests", Id(r), Request("Approbation", "Approuvée")));
        Assert.NotEqual(JsonValueKind.Null, approved.GetProperty("approvedAt").ValueKind);
        await Json(await Put(user, "/api/requests", Id(r), Request("Approbation", "En cours")));
    }

    [Fact]
    public async Task Changement_standard_pre_autorise_normal_autorise_puis_clos_avec_resultat()
    {
        var user = api.Client("User");
        var standard = await Create(user, "/api/changes", Change("Standard", "Standard"));
        Assert.Equal("Autorisé", Str(standard, "status"));

        var normal = await Create(user, "/api/changes", Change("Normal", "Normal"));
        Assert.Equal("Demandé", Str(normal, "status"));
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(user, "/api/changes", Id(normal), Change("Normal", "Normal", "Planifié"))).StatusCode);
        await Json(await Put(user, "/api/changes", Id(normal), Change("Normal", "Normal", "Évalué")));
        Assert.Equal(HttpStatusCode.Forbidden, (await Put(user, "/api/changes", Id(normal), Change("Normal", "Normal", "Autorisé"))).StatusCode);

        await Json(await Put(api.Client("Manager"), "/api/changes", Id(normal), Change("Normal", "Normal", "Autorisé")));
        await Json(await Put(user, "/api/changes", Id(normal), Change("Normal", "Normal", "Planifié")));
        await Json(await Put(user, "/api/changes", Id(normal), Change("Normal", "Normal", "Mis en œuvre")));
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(user, "/api/changes", Id(normal), Change("Normal", "Normal", "Clos"))).StatusCode);
        await Json(await Put(user, "/api/changes", Id(normal), Change("Normal", "Normal", "Clos", "Réussi")));

        var schedule = await Json(await user.GetAsync("/api/changes/schedule"));
        Assert.Contains(schedule.EnumerateArray(), c => Id(c) == Id(standard));
    }

    [Fact]
    public async Task Catalogue_et_SLA_reserves_aux_gestionnaires()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client("User").PostAsJsonAsync("/api/services",
            new { title = "Interdit", criticality = "Faible" })).StatusCode);

        var manager = api.Client("Manager");
        var svc = await Create(manager, "/api/services", new { title = "ERP", criticality = "Élevée" });
        var sla = await Create(manager, "/api/agreements", new
        {
            title = "SLA ERP", serviceId = Id(svc), customer = "Finance",
            availabilityTarget = 99.5, resolutionHoursP1 = 4
        });
        Assert.Equal("Brouillon", Str(sla, "status"));
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsJsonAsync("/api/agreements",
            new { title = "Orphelin", serviceId = 999999 })).StatusCode);

        var detail = await Json(await manager.GetAsync($"/api/services/{Id(svc)}"));
        Assert.Single(detail.GetProperty("agreements").EnumerateArray());
    }

    [Fact]
    public async Task Relations_entre_CI()
    {
        var client = api.Client();
        var app = await Create(client, "/api/configuration-items", new { title = "app-paie", ciType = "Application", environment = "Production", ownerType = "User", ownerId = "hery.rakoto", ownerDisplayName = "Hery Rakoto" });
        var srv = await Create(client, "/api/configuration-items", new { title = "srv-paie", ciType = "Serveur", environment = "Production", ownerType = "User", ownerId = "hery.rakoto", ownerDisplayName = "Hery Rakoto" });

        await Json(await client.PostAsJsonAsync($"/api/configuration-items/{Id(app)}/relations",
            new { targetReference = Str(srv, "reference"), type = "Dépend de" }));
        var detail = await Json(await client.GetAsync($"/api/configuration-items/{Id(srv)}"));
        Assert.Single(detail.GetProperty("incoming").EnumerateArray());
    }

    [Fact]
    public async Task Lien_incident_probleme_lu_des_deux_cotes_et_retire_a_la_suppression()
    {
        var admin = api.Client();
        var inc = await Create(admin, "/api/incidents", Incident("Lien"));
        var prb = await Create(admin, "/api/problems", new
        {
            title = "Lien", description = "d", impact = "Moyen", urgency = "Moyenne", category = "c", affectedService = "s"
        });

        await Json(await admin.PostAsJsonAsync("/api/links",
            new { fromType = "incident", fromId = Id(inc), toReference = Str(prb, "reference").ToLower() }));
        var fromProblem = await Json(await admin.GetAsync($"/api/links?type=problem&id={Id(prb)}"));
        Assert.Contains(fromProblem.EnumerateArray(), l => Str(l, "reference") == Str(inc, "reference"));

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/links",
            new { fromType = "problem", fromId = Id(prb), toReference = Str(inc, "reference") })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/links",
            new { fromType = "incident", fromId = Id(inc), toReference = "XYZ-2026-0001" })).StatusCode);

        await admin.DeleteAsync($"/api/incidents/{Id(inc)}");
        var after = await Json(await admin.GetAsync($"/api/links?type=problem&id={Id(prb)}"));
        Assert.Empty(after.EnumerateArray());
    }

    [Fact]
    public async Task Console_assignations_et_decisions_en_attente()
    {
        var user = api.Client("User", "Fara.Rasoa");
        var inc = await Create(api.Client(), "/api/incidents", Incident("Pour Fara", owner: "fara.rasoa", major: true));
        var mine = await Json(await user.GetAsync("/api/console"));
        Assert.Contains(mine.GetProperty("myAssignments").EnumerateArray(), a => Id(a) == Id(inc) && Str(a, "type") == "incident");

        var req = await Create(user, "/api/requests", Request("À approuver"));
        var manager = await Json(await api.Client("Manager").GetAsync("/api/console"));
        var m = manager.GetProperty("management");
        Assert.Contains(m.GetProperty("decisions").EnumerateArray(), d => Id(d) == Id(req) && Str(d, "type") == "request");
        Assert.Contains(m.GetProperty("majorIncidents").EnumerateArray(), d => Id(d) == Id(inc));
    }

    // --- Revue de code du 2026-10-06 ---

    [Fact]
    public async Task Changement_rejete_ne_peut_etre_ni_planifie_ni_mis_en_oeuvre_ni_clos()
    {
        var user = api.Client("User");
        var c = await Create(user, "/api/changes", Change("Rejeté", "Normal"));
        await Json(await Put(user, "/api/changes", Id(c), Change("Rejeté", "Normal", "Évalué")));
        await Json(await Put(api.Client("Manager"), "/api/changes", Id(c), Change("Rejeté", "Normal", "Rejeté")));

        foreach (var to in new[] { "Planifié", "Mis en œuvre", "Clos" })
            Assert.Equal(HttpStatusCode.BadRequest,
                (await Put(user, "/api/changes", Id(c), Change("Rejeté", "Normal", to, "Réussi"))).StatusCode);
    }

    [Fact]
    public async Task F10_changement_autorise_modifie_par_un_utilisateur_redemande_une_autorisation()
    {
        var user = api.Client("User");
        var standard = await Create(user, "/api/changes", Change("Standard détourné", "Standard"));
        var edited = await Json(await Put(user, "/api/changes", Id(standard), Change("Standard détourné", "Urgent", "Autorisé")));
        Assert.Equal("Évalué", Str(edited, "status"));
        Assert.Equal(JsonValueKind.Null, edited.GetProperty("authorizedAt").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Put(user, "/api/changes", Id(standard), Change("Standard détourné", "Urgent", "Planifié"))).StatusCode);

        var manager = api.Client("Manager");
        var normal = await Create(user, "/api/changes", Change("Risque revu", "Normal"));
        await Json(await Put(user, "/api/changes", Id(normal), Change("Risque revu", "Normal", "Évalué")));
        await Json(await Put(manager, "/api/changes", Id(normal), Change("Risque revu", "Normal", "Autorisé")));
        await Json(await Put(user, "/api/changes", Id(normal), Change("Risque revu", "Normal", "Planifié")));
        var riskier = await Json(await Put(user, "/api/changes", Id(normal), Change("Risque revu", "Normal", "Planifié", risk: "Élevé")));
        Assert.Equal("Évalué", Str(riskier, "status"));

        // Le gestionnaire est l'autorité de changement : sa modification vaut autorisation.
        await Json(await Put(manager, "/api/changes", Id(normal), Change("Risque revu", "Normal", "Autorisé", risk: "Élevé")));
        var byManager = await Json(await Put(manager, "/api/changes", Id(normal), Change("Risque revu", "Normal", "Autorisé", risk: "Faible")));
        Assert.Equal("Autorisé", Str(byManager, "status"));

        // Renommer ou décrire n'est pas une modification du changement lui-même.
        var renamed = await Json(await Put(user, "/api/changes", Id(normal), Change("Risque revu (titre)", "Normal", "Autorisé", risk: "Faible")));
        Assert.Equal("Autorisé", Str(renamed, "status"));
    }

    [Fact]
    public async Task F9_article_publie_modifie_par_un_utilisateur_repasse_en_brouillon()
    {
        var user = api.Client("User");
        var manager = api.Client("Manager");
        object Dto(string content, string? status) => new { title = "Procédure VPN", articleType = "Procédure", content, status };
        var a = await Create(user, "/api/knowledge", Dto("v1", null));
        await Json(await Put(manager, "/api/knowledge", Id(a), Dto("v1", "Publié")));

        var byManager = await Json(await Put(manager, "/api/knowledge", Id(a), Dto("v2", "Publié")));
        Assert.Equal("Publié", Str(byManager, "status"));

        var byUser = await Json(await Put(user, "/api/knowledge", Id(a), Dto("v3", "Publié")));
        Assert.Equal("Brouillon", Str(byUser, "status"));
        Assert.Equal(JsonValueKind.Null, byUser.GetProperty("publishedAt").ValueKind);
    }

    [Fact]
    public async Task Amelioration_abandonnee_ne_repart_pas_sans_nouvelle_validation()
    {
        var user = api.Client("User");
        var manager = api.Client("Manager");
        object Dto(string? status) => new
        {
            title = "Abandon", step = 1, priority = "Moyenne", status, benefit = "b", baseline = "5 j", target = "2 j",
            abandonReason = "Priorité revue"
        };
        var a = await Create(user, "/api/improvements", Dto(null));
        await Json(await Put(manager, "/api/improvements", Id(a), Dto("Validée")));
        await Json(await Put(manager, "/api/improvements", Id(a), Dto("Abandonnée")));

        Assert.Equal(HttpStatusCode.BadRequest, (await Put(user, "/api/improvements", Id(a), Dto("En cours"))).StatusCode);
    }

    [Fact]
    public async Task Les_conditions_d_un_statut_tiennent_aussi_quand_on_modifie_sans_changer_de_statut()
    {
        var user = api.Client("User");
        var i = await Create(user, "/api/incidents", Incident("Invariant"));
        await Json(await Put(user, "/api/incidents", Id(i), Incident("Invariant", "Résolu", "Redémarrage")));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Put(user, "/api/incidents", Id(i), Incident("Invariant", "Résolu", ""))).StatusCode);

        var c = await Create(user, "/api/changes", Change("Invariant", "Standard"));
        await Json(await Put(user, "/api/changes", Id(c), Change("Invariant", "Standard", "Planifié")));
        await Json(await Put(user, "/api/changes", Id(c), Change("Invariant", "Standard", "Mis en œuvre")));
        await Json(await Put(user, "/api/changes", Id(c), Change("Invariant", "Standard", "Clos", "Réussi")));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Put(user, "/api/changes", Id(c), Change("Invariant", "Standard", "Clos"))).StatusCode);
    }

    [Theory]
    [InlineData("/api/incidents")]
    [InlineData("/api/problems")]
    public async Task Texte_trop_long_refuse_en_400_et_non_500(string path)
    {
        var res = await api.Client().PostAsJsonAsync(path, new
        {
            title = new string('x', 201), description = "d", impact = "Moyen", urgency = "Moyenne",
            category = "c", affectedService = "s"
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task La_liste_des_articles_ne_renvoie_pas_leur_contenu()
    {
        var client = api.Client();
        await Create(client, "/api/knowledge", new { title = "Léger", articleType = "FAQ", content = new string('c', 5000) });
        var list = await Json(await client.GetAsync("/api/knowledge"));
        Assert.All(list.EnumerateArray(), a => Assert.False(a.TryGetProperty("content", out _)));
    }
}
