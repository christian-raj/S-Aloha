using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SAloha.Api.Tests;

/// <summary>Problèmes et changements, lot 3 : problème sans activité (PRB-32), modèle standard suspendu (CHG-27).</summary>
[Collection("api")]
public class ProblemChangeLot3Tests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();

    [Fact]
    public async Task PRB_32_probleme_sans_activite_depuis_30_jours_remonte_aux_gestionnaires()
    {
        var m = api.Client("Manager", "chef.prb32");
        object Problem(string title) => new { title, description = "d", impact = "Moyen", urgency = "Moyenne", category = "", affectedService = "", source = "Proactif" };
        var idle = await Json(await m.PostAsJsonAsync("/api/problems", Problem("Lenteurs récurrentes de l'ERP")));
        var active = await Json(await m.PostAsJsonAsync("/api/problems", Problem("Coupures Wi-Fi du 3e étage")));
        await using (var db = api.Database())
        {
            var old = DateTime.UtcNow.AddDays(-40);
            foreach (var id in new[] { Id(idle), Id(active) })
            {
                (await db.Problems.SingleAsync(p => p.Id == id)).UpdatedAt = old;
                await db.AuditEntries.Where(a => a.EntityType == "problem" && a.EntityId == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.At, old));
            }
            await db.SaveChangesAsync();
        }
        // Un commentaire récent est une activité.
        await Json(await m.PostAsJsonAsync("/api/comments", new { type = "problem", id = Id(active), body = "Relevé des bornes en cours" }));

        var console = await Json(await m.GetAsync("/api/console"));
        var rows = console.GetProperty("management").GetProperty("idleProblems").EnumerateArray().Select(Id).ToList();
        Assert.Contains(Id(idle), rows);
        Assert.DoesNotContain(Id(active), rows);

        await Json(await api.Client("Admin", "admin.prb32").PostAsync("/api/notifications/run", null));
        await Json(await api.Client("Admin", "admin.prb32").PostAsync("/api/notifications/run", null));
        await using var check = api.Database();
        Assert.Equal(1, await check.Notifications.CountAsync(n => n.TargetType == "problem" && n.TargetId == Id(idle) && n.Event.StartsWith("Sans activité depuis")));
        Assert.False(await check.Notifications.AnyAsync(n => n.TargetType == "problem" && n.TargetId == Id(active) && n.Event.StartsWith("Sans activité depuis")));
    }

    [Fact]
    public async Task CHG_27_l_echec_d_un_changement_standard_suspend_son_modele_jusqu_a_la_revue()
    {
        var m = api.Client("Manager", "chef.chg27");
        var u = api.Client("User", "agent.chg27");
        var model = await Json(await m.PostAsJsonAsync("/api/standard-changes", new
        {
            name = $"Mise à jour mensuelle {Guid.NewGuid():N}"[..30], implementationPlan = "Appliquer les correctifs", backoutPlan = "Restaurer l'instantané", risk = "Faible"
        }));
        var slot = DateTime.UtcNow.Date.AddDays(120).AddHours(21);
        object Change(string? outcome = null) => new
        {
            title = "Correctifs du mois", changeType = "Standard", risk = "Faible", standardModelId = Id(model), outcome,
            plannedStart = slot, plannedEnd = slot.AddHours(2), implementationPlan = "Appliquer les correctifs", backoutPlan = "Restaurer l'instantané"
        };
        var c = await Json(await u.PostAsJsonAsync("/api/changes", Change()));
        await Json(await u.PutAsJsonAsync($"/api/changes/{Id(c)}", Change("Échoué")));

        var suspended = (await Json(await u.GetAsync("/api/standard-changes?all=true"))).EnumerateArray().Single(x => Id(x) == Id(model));
        Assert.Equal(c.GetProperty("reference").GetString(), suspended.GetProperty("suspendedByChange").GetString());
        var refused = await u.PostAsJsonAsync("/api/changes", Change());
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("suspendu", await refused.Content.ReadAsStringAsync());
        var console = await Json(await m.GetAsync("/api/console"));
        Assert.Contains(console.GetProperty("management").GetProperty("decisions").EnumerateArray(),
            x => Id(x) == Id(c) && x.GetProperty("status").GetString()!.Contains("suspendu"));
        await using (var db = api.Database())
            Assert.True(await db.Notifications.AnyAsync(n => n.TargetType == "change" && n.TargetId == Id(c) && n.Event.StartsWith("Modèle standard suspendu")));

        // Revue : gestionnaire, motivée ; le modèle est remis en service.
        var review = $"/api/standard-changes/{Id(model)}/review";
        Assert.Equal(HttpStatusCode.Forbidden, (await u.PostAsJsonAsync(review, new { note = "ok" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await m.PostAsJsonAsync(review, new { note = " " })).StatusCode);
        var reviewed = await Json(await m.PostAsJsonAsync(review, new { note = "Instantané absent : étape ajoutée au plan" }));
        Assert.Equal(JsonValueKind.Null, reviewed.GetProperty("suspendedAt").ValueKind);
        Assert.True(reviewed.GetProperty("active").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await m.PostAsJsonAsync(review, new { note = "encore" })).StatusCode);   // plus suspendu
        await Json(await u.PostAsJsonAsync("/api/changes", Change()));
    }
}
