using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SAloha.Api.Core.Auth;

namespace SAloha.Api.Tests;

/// <summary>Étape 5 du lot 1 : groupes AD dans le jeton (SOC-10, PRB-26).</summary>
[Collection("api")]
public class GroupAssignmentTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();
    private static IEnumerable<string> Refs(JsonElement list) =>
        list.EnumerateArray().Select(x => x.GetProperty("reference").GetString()!);

    [Fact]
    public void SOC_10_le_jeton_porte_les_groupes_AD()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = new string('k', 64), ["Jwt:Issuer"] = "S-Aloha"
        }).Build();
        var token = new TokenService(config).Create("hery.rakoto", "Hery", "User", ["Support N1", "SAloha-Users"]);
        var groups = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
            .Where(c => c.Type == Principals.GroupClaim).Select(c => c.Value);
        Assert.Equal(["Support N1", "SAloha-Users"], groups);
    }

    [Fact]
    public async Task SOC_10_un_incident_confie_a_un_groupe_est_aux_membres()
    {
        var m = api.Client("Manager", "chef.support");
        var member = api.Client("User", "membre.n1", "SUPPORT-N1-SOC10");     // casse différente : l'AD l'ignore
        var outsider = api.Client("User", "externe.soc10", "Autre-Groupe");

        var inc = await Json(await m.PostAsJsonAsync("/api/incidents", new
        {
            title = "Imprimante du 2e étage", impact = "Faible", urgency = "Faible",
            ownerType = "Group", ownerId = "Support-N1-SOC10", ownerDisplayName = "Support N1"
        }));
        var reference = inc.GetProperty("reference").GetString();

        Assert.Contains(reference, Refs(await Json(await member.GetAsync("/api/incidents?owner=me"))));
        Assert.DoesNotContain(reference, Refs(await Json(await outsider.GetAsync("/api/incidents?owner=me"))));

        var console = await Json(await member.GetAsync("/api/console"));
        Assert.Contains(reference, Refs(console.GetProperty("myAssignments")));
        var other = await Json(await outsider.GetAsync("/api/console"));
        Assert.DoesNotContain(reference, Refs(other.GetProperty("myAssignments")));
    }

    [Fact]
    public async Task SOC_10_un_compte_homonyme_d_un_groupe_ne_recoit_pas_ses_enregistrements()
    {
        var m = api.Client("Manager", "chef.support");
        var homonym = api.Client("User", "reseau-soc10");    // compte nommé comme le groupe, sans en être membre
        var inc = await Json(await m.PostAsJsonAsync("/api/incidents", new
        {
            title = "Switch du 3e", impact = "Faible", urgency = "Faible",
            ownerType = "Group", ownerId = "reseau-soc10", ownerDisplayName = "Réseau"
        }));
        Assert.DoesNotContain(inc.GetProperty("reference").GetString(),
            Refs(await Json(await homonym.GetAsync("/api/incidents?owner=me"))));
    }

    [Fact]
    public async Task PRB_26_les_actions_confiees_a_un_groupe_sont_a_ses_membres()
    {
        var m = api.Client("Manager", "manager.prb26");
        var member = api.Client("User", "membre.prb26", "Equipe-Infra-PRB26");
        var outsider = api.Client("User", "externe.prb26");

        var p = await Json(await m.PostAsJsonAsync("/api/problems", new
        {
            title = "Saturation disque", description = "d", impact = "Moyen", urgency = "Moyenne",
            category = "Infra", affectedService = "Stockage"
        }));
        var dto = new
        {
            title = "Étendre le volume", description = "d", dueDate = DateTime.UtcNow.Date.AddDays(7), status = (string?)null,
            raci = new[]
            {
                new { role = "R", assigneeType = "Group", assigneeId = "Equipe-Infra-PRB26", assigneeDisplayName = "Équipe infra" },
                new { role = "A", assigneeType = "User", assigneeId = "manager.prb26", assigneeDisplayName = "Manager" }
            }
        };
        var a = await Json(await m.PostAsJsonAsync($"/api/problems/{Id(p)}/actions", dto));

        // Mes actions (filtre « assignee » sur moi-même) et ma console.
        var mine = await Json(await member.GetAsync("/api/actions?assignee=membre.prb26"));
        Assert.Contains(mine.EnumerateArray(), x => Id(x) == Id(a));
        var console = await Json(await member.GetAsync("/api/console"));
        var line = console.GetProperty("myActions").EnumerateArray().Single(x => Id(x) == Id(a));
        Assert.Equal(["R"], line.GetProperty("myRoles").EnumerateArray().Select(r => r.GetString()));
        Assert.DoesNotContain((await Json(await outsider.GetAsync("/api/actions?assignee=externe.prb26"))).EnumerateArray(),
            x => Id(x) == Id(a));

        // Membre du groupe R : il modifie l'action (PRB-24) ; un tiers non.
        Assert.Equal(HttpStatusCode.OK, (await member.PutAsJsonAsync($"/api/actions/{Id(a)}", dto with { title = "Étendre le volume à 2 To" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PutAsJsonAsync($"/api/actions/{Id(a)}", dto with { title = "Autre" })).StatusCode);
    }

    [Fact]
    public async Task Une_echeance_d_action_sans_fuseau_est_lue_en_UTC()
    {
        var m = api.Client("Manager", "manager.dates");
        var p = await Json(await m.PostAsJsonAsync("/api/problems", new
        {
            title = "Échéance sans fuseau", description = "d", impact = "Faible", urgency = "Faible",
            category = "Essai", affectedService = "Essai"
        }));
        var res = await m.PostAsync($"/api/problems/{Id(p)}/actions", new StringContent(
            """{"title":"Action","description":"d","dueDate":"2026-10-20","raci":[{"role":"R","assigneeType":"User","assigneeId":"manager.dates","assigneeDisplayName":"M"},{"role":"A","assigneeType":"User","assigneeId":"manager.dates","assigneeDisplayName":"M"}]}""",
            System.Text.Encoding.UTF8, "application/json"));
        var a = await Json(res);
        Assert.StartsWith("2026-10-20T00:00:00", a.GetProperty("dueDate").GetString());
    }
}
