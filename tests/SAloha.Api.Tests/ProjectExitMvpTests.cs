using System.Net.Http.Json;
using System.Text.Json;
using SAloha.Api.Modules.ProjectManagement;

namespace SAloha.Api.Tests;

/// <summary>Projets, sortie du MVP : prévision de la date de fin (PRJ-22), console (PRJ-34).</summary>
[Collection("api")]
public class ProjectExitMvpTests(ApiFixture api)
{
    private static readonly DateTime Today = new(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);

    private static Project P(string method = "Scrum", DateTime? start = null, DateTime? target = null, int? tolDays = null) =>
        new() { Method = method, Status = "En cours", StartDate = start, TargetEndDate = target, ToleranceDays = tolDays };

    [Fact]
    public void PRJ_22_prevision_par_la_velocite()
    {
        // Deux sprints clos de 14 jours, 10 et 6 points terminés : 8 points par sprint ; 20 restants → 3 sprints.
        var sprints = new List<Sprint>
        {
            new() { Id = 1, Status = "Clos", StartDate = Today.AddDays(-28), EndDate = Today.AddDays(-14) },
            new() { Id = 2, Status = "Clos", StartDate = Today.AddDays(-14), EndDate = Today },
        };
        var items = new List<WorkItem>
        {
            new() { SprintId = 1, Points = 10, Status = "Terminé" }, new() { SprintId = 2, Points = 6, Status = "Terminé" },
            new() { Points = 20, Status = "À faire" },
        };
        var f = ProjectHealth.ForecastEnd(P(target: Today.AddDays(30), tolDays: 20), items, sprints, [], Today);
        Assert.Equal("Vélocité", f.Method);
        Assert.Equal(Today.AddDays(42), f.Date);
        Assert.Equal(ProjectHealth.Watch, f.Tolerance);   // 42 jours : au-delà de la cible (30), dans la tolérance (50)
    }

    [Fact]
    public void PRJ_22_prevision_par_les_phases_avec_retard()
    {
        var phases = new List<ProjectPhase>
        {
            new() { Name = "Conception", Order = 1, Status = "Terminée", PlannedEnd = Today.AddDays(-30) },
            new() { Name = "Réalisation", Order = 2, Status = "En cours", PlannedEnd = Today.AddDays(-5) },
            new() { Name = "Recette", Order = 3, Status = "À venir", PlannedEnd = Today.AddDays(20) },
        };
        var f = ProjectHealth.ForecastEnd(P("Cycle en V", target: Today.AddDays(20)), [], [], phases, Today);
        Assert.Equal("Phases", f.Method);
        Assert.Equal(Today.AddDays(25), f.Date);   // fin de la recette + 5 jours de retard de la réalisation
        Assert.Equal(ProjectHealth.Out, f.Tolerance);
    }

    [Fact]
    public void PRJ_22_prevision_par_l_avancement_et_sans_donnees()
    {
        var items = new List<WorkItem> { new() { Status = "Terminé" }, new() { Status = "À faire" }, new() { Status = "En cours" }, new() { Status = "À faire" } };
        // Un élément sur quatre en 10 jours : 40 jours au même rythme.
        var f = ProjectHealth.ForecastEnd(P("Kanban", start: Today.AddDays(-10), target: Today.AddDays(60)), items, [], [], Today);
        Assert.Equal("Avancement", f.Method);
        Assert.Equal(Today.AddDays(30), f.Date);
        Assert.Equal(ProjectHealth.Within, f.Tolerance);
        Assert.Null(ProjectHealth.ForecastEnd(P(), [], [], [], Today).Date);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();

    [Fact]
    public async Task PRJ_34_console_projets_a_valider_alertes_et_mes_projets()
    {
        var lead = api.Client("User", "prj34.chef");
        var m = api.Client("Manager", "prj34.manager");
        object Dto(string title, string? status = null) => new
        {
            title, description = "", status, ownerType = "User", ownerId = "prj34.chef", ownerDisplayName = "Chef",
            method = "Cycle en V", components = (string[]?)null, startDate = DateTime.UtcNow.Date.AddDays(-30), targetEndDate = DateTime.UtcNow.Date.AddDays(60),
            budget = (decimal?)null, actualCost = (decimal?)null, justification = (string?)null, expectedBenefits = (string?)null,
            expectedCosts = (string?)null, majorRisks = (string?)null, toleranceDays = 10, toleranceCostPercent = (decimal?)null,
            toleranceScope = (string?)null, toleranceQuality = (string?)null, abandonReason = (string?)null
        };
        var draft = await Json(await lead.PostAsJsonAsync("/api/projects", Dto("PRJ-34 en cadrage")));
        var running = await Json(await lead.PostAsJsonAsync("/api/projects", Dto("PRJ-34 en cours")));
        await Json(await m.PutAsJsonAsync($"/api/projects/{Id(running)}", Dto("PRJ-34 en cours", "En cours")));
        await Json(await lead.PostAsJsonAsync($"/api/projects/{Id(running)}/milestones",
            new { name = "Jalon dépassé", phaseId = (int?)null, dueDate = DateTime.UtcNow.Date.AddDays(-3), achieved = false }));

        var console = await Json(await m.GetAsync("/api/console"));
        var management = console.GetProperty("management");
        Assert.Contains(management.GetProperty("decisions").EnumerateArray(), d => Id(d) == Id(draft) && d.GetProperty("status").GetString() == "À valider");
        var alert = Assert.Single(management.GetProperty("projectAlerts").EnumerateArray(), a => Id(a) == Id(running));
        Assert.Contains("1 jalon(s) en retard", alert.GetProperty("status").GetString());

        var mine = await Json(await lead.GetAsync("/api/console"));
        Assert.Contains(mine.GetProperty("myAssignments").EnumerateArray(), a => Id(a) == Id(running) && a.GetProperty("type").GetString() == "project");
        Assert.Contains(mine.GetProperty("myProjectAlerts").EnumerateArray(), a => Id(a) == Id(running));

        // La synthèse porte la prévision (PRJ-22).
        var summary = await Json(await lead.GetAsync($"/api/projects/{Id(running)}/summary"));
        Assert.Equal("Aucune", summary.GetProperty("forecast").GetProperty("method").GetString());
    }
}
