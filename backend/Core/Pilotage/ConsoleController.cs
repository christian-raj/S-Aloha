using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Auth;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Perimeters;
using SAloha.Api.Core.Records;
using SAloha.Api.Modules.ChangeEnablement;
using SAloha.Api.Modules.ContinualImprovement;
using SAloha.Api.Modules.IncidentManagement;
using SAloha.Api.Modules.KnowledgeManagement;
using SAloha.Api.Modules.ProblemManagement;
using SAloha.Api.Modules.ServiceLevelManagement;
using SAloha.Api.Modules.ServiceRequestManagement;

namespace SAloha.Api.Core.Pilotage;

/// <summary>Console personnalisée par rôle.</summary>
[ApiController]
[Route("api/console")]
[Authorize(Policy = "User")]
public class ConsoleController(AppDbContext db, PerimeterAccess access) : ControllerBase
{
    /// <summary>
    /// Console adaptée au rôle : User = mon activité, Manager/Admin = pilotage du processus.
    /// PER-09 : mon activité se limite à mes périmètres, le pilotage aux périmètres dont je
    /// suis gestionnaire ; l'Admin voit tout et peut filtrer par périmètre (<c>perimeter</c>).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? perimeter)
    {
        var scope = await access.GetAsync();
        var all = scope.Global && perimeter is null;
        var seen = (perimeter is { } only ? (scope.Sees(only) ? [only] : []) : scope.Ids).ToList();
        var managed = (perimeter is { } one ? (scope.ManagerOn(one) ? [one] : []) : scope.ManagedIds).ToList();
        var me = User.Identity?.Name ?? "";
        // L'AD ignore la casse des identifiants : la comparaison aussi (M2,
        // revue du 2026-10-06). Couvre les données enregistrées avant que le
        // jeton ne porte le sAMAccountName de l'annuaire.
        var meKey = me.ToLowerInvariant();
        // SOC-10 : ce qui est confié à l'un de mes groupes AD est aussi à moi.
        var groups = User.GroupKeys();
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "User";
        // L'échéance est une date (stockée à minuit UTC) : une action n'est en
        // retard qu'à partir du LENDEMAIN de son échéance, pas dès le matin
        // du jour même (M6, revue du 2026-10-06).
        var today = DateTime.UtcNow.Date;

        // --- Bloc personnel (tous les rôles) ---
        var myProblems = await db.Problems.AsNoTracking()
            .Where(p => p.CreatedBy.ToLower() == meKey && p.Status != "Clos" && (all || seen.Contains(p.PerimeterId)))
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new { p.Id, p.Reference, p.Title, p.Status, p.Priority, p.CreatedAt })
            .Take(10).ToListAsync();

        var myActions = await db.Actions.AsNoTracking()
            .Include(a => a.Raci).Include(a => a.Problem)
            .Where(a => a.Status != "Terminée" && a.Status != "Annulée" && (all || seen.Contains(a.Problem!.PerimeterId))
                        && a.Raci.Any(r => r.AssigneeType == "Group"
                            ? groups.Contains(r.AssigneeId.ToLower()) : r.AssigneeId.ToLower() == meKey))
            .OrderBy(a => a.DueDate)
            .Select(a => new
            {
                a.Id, a.Title, a.Status, a.DueDate,
                MyRoles = a.Raci.Where(r => r.AssigneeType == "Group"
                    ? groups.Contains(r.AssigneeId.ToLower()) : r.AssigneeId.ToLower() == meKey).Select(r => r.Role).Distinct(),
                Problem = new { a.Problem!.Id, a.Problem.Reference, a.Problem.Title },
                Overdue = a.DueDate != null && a.DueDate < today
            }).ToListAsync();

        // Enregistrements des autres processus dont je suis responsable (assigné, propriétaire).
        var myAssignments = new List<Item>();
        // PRB-21 : les problèmes dont je suis le gestionnaire.
        myAssignments.AddRange(await Mine(db.Problems, "problem", meKey, groups, ["Résolu", "Clos"], all, seen));
        myAssignments.AddRange(await Mine(db.Incidents, "incident", meKey, groups, Incident.Done, all, seen));
        myAssignments.AddRange(await Mine(db.ServiceRequests, "request", meKey, groups, ServiceRequest.Done, all, seen));
        myAssignments.AddRange(await Mine(db.Changes, "change", meKey, groups, Change.Done, all, seen));
        myAssignments.AddRange(await Mine(db.Improvements, "improvement", meKey, groups, Improvement.Done, all, seen));

        // INC-24 : incidents ouverts en risque ou hors délai — les miens (Urgent du responsable).
        var now = DateTime.UtcNow;
        var slaOpen = (await db.Incidents.AsNoTracking()
                .Where(i => !Incident.Done.Contains(i.Status) && i.Status != "En attente" && i.ResolutionDueAt != null
                            && (all || seen.Contains(i.PerimeterId)))
                .ToListAsync())
            .Select(i => new { i, State = IncidentSla.State(i, now) })
            .Where(x => x.State is IncidentSla.AtRisk or IncidentSla.Breached)
            .OrderBy(x => x.i.ResolutionDueAt).ToList();
        var mySla = slaOpen.Where(x => x.i.OwnerId != null && (x.i.OwnerType == "Group"
                ? groups.Contains(x.i.OwnerId.ToLower()) : x.i.OwnerId.ToLower() == meKey))
            .Select(x => new Item("incident", x.i.Id, x.i.Reference, x.i.Title, x.State)).ToList();

        object? management = null;
        // ADR-0014 D4 : le pilotage revient à qui est gestionnaire d'au moins un périmètre.
        if (all || managed.Count > 0)
        {
            // --- Bloc pilotage (Manager / Admin) ---
            var toQualify = await db.Problems.AsNoTracking()
                .Where(p => p.Status == "Nouveau" && (all || managed.Contains(p.PerimeterId))).OrderBy(p => p.CreatedAt)
                .Select(p => new { p.Id, p.Reference, p.Title, p.Priority, p.CreatedAt, p.CreatedByDisplayName })
                .ToListAsync();

            var inAnalysisNoRootCause = await db.Problems.AsNoTracking()
                .Where(p => p.Status == "En analyse" && (p.RootCause == null || p.RootCause == "") && (all || managed.Contains(p.PerimeterId)))
                .Select(p => new { p.Id, p.Reference, p.Title, p.Priority, AnalysesCount = p.Analyses.Count })
                .ToListAsync();

            var knownErrorsNoAction = await db.Problems.AsNoTracking()
                .Where(p => p.Status == "Erreur connue" && !p.Actions.Any(a => a.Status != "Annulée") && (all || managed.Contains(p.PerimeterId)))
                .Select(p => new { p.Id, p.Reference, p.Title, p.Priority })
                .ToListAsync();

            var overdue = await db.Actions.AsNoTracking()
                .Include(a => a.Raci).Include(a => a.Problem)
                .Where(a => a.Status != "Terminée" && a.Status != "Annulée"
                            && a.DueDate != null && a.DueDate < today && (all || managed.Contains(a.Problem!.PerimeterId)))
                .Select(a => new
                {
                    a.Id, a.Title, a.DueDate,
                    Problem = new { a.Problem!.Id, a.Problem.Reference },
                    Responsibles = a.Raci.Where(r => r.Role == "R").Select(r => r.AssigneeDisplayName)
                }).ToListAsync();

            var majorIncidents = await db.Incidents.AsNoTracking()
                .Where(i => i.IsMajor && !Incident.Done.Contains(i.Status) && (all || managed.Contains(i.PerimeterId)))
                .Select(i => new Item("incident", i.Id, i.Reference, i.Title, i.Status)).ToListAsync();

            // Décisions de gestionnaire en attente : autoriser, approuver, valider.
            var decisions = new List<Item>();
            decisions.AddRange(await InStatus(db.Changes, "change", all, managed, "Demandé", "Évalué"));
            decisions.AddRange(await InStatus(db.ServiceRequests, "request", all, managed, "Soumise"));
            decisions.AddRange(await InStatus(db.Improvements, "improvement", all, managed, "Proposée"));
            // Connaissances : brouillons soumis à publication (KB-21), archivages demandés à la revue (KB-17).
            decisions.AddRange(await db.KnowledgeArticles.AsNoTracking()
                .Where(a => a.Status == "Brouillon" && a.SubmittedAt != null && (all || managed.Contains(a.PerimeterId))).OrderBy(a => a.SubmittedAt)
                .Select(a => new Item("article", a.Id, a.Reference, a.Title, "À publier")).ToListAsync());
            decisions.AddRange(await db.KnowledgeArticles.AsNoTracking()
                .Where(a => a.Status == "Publié" && a.ArchiveRequestedAt != null && (all || managed.Contains(a.PerimeterId))).OrderBy(a => a.ArchiveRequestedAt)
                .Select(a => new Item("article", a.Id, a.Reference, a.Title, "Archivage demandé")).ToListAsync());

            // Revues échues : articles publiés et SLA en vigueur dont la date de revue est passée.
            var reviewsDue = new List<Item>();
            reviewsDue.AddRange(await db.KnowledgeArticles.AsNoTracking()
                .Where(a => a.Status == "Publié" && a.ReviewDate != null && a.ReviewDate < today && (all || managed.Contains(a.PerimeterId)))
                .Select(a => new Item("article", a.Id, a.Reference, a.Title, a.Status)).ToListAsync());
            // KB-12 : article d'erreur connue dont le problème est clos « Corrigé » : à archiver.
            var fixedProblems = db.Problems.Where(p => p.Status == "Clos" && p.ClosureCode == "Corrigé").Select(p => p.Id);
            reviewsDue.AddRange(await db.KnowledgeArticles.AsNoTracking()
                .Where(a => a.Status == "Publié" && a.ArticleType == "Erreur connue" && (all || managed.Contains(a.PerimeterId))
                            && db.ItemLinks.Any(l => (l.FromType == "article" && l.FromId == a.Id && l.ToType == "problem" && fixedProblems.Contains(l.ToId))
                                                  || (l.ToType == "article" && l.ToId == a.Id && l.FromType == "problem" && fixedProblems.Contains(l.FromId))))
                .Select(a => new Item("article", a.Id, a.Reference, a.Title, "À archiver : problème corrigé")).ToListAsync());
            reviewsDue.AddRange(await db.Agreements.AsNoTracking()
                .Where(a => a.Status == "En vigueur" && a.ReviewDate != null && a.ReviewDate < today && (all || managed.Contains(a.PerimeterId)))
                .Select(a => new Item("agreement", a.Id, a.Reference, a.Title, a.Status)).ToListAsync());
            // NIS-16 : évaluation NIS 2 validée arrivée à la date de la prochaine évaluation.
            reviewsDue.AddRange(await db.Assessments.AsNoTracking()
                .Where(a => a.Status == "Validée" && a.NextAssessmentDate != null && a.NextAssessmentDate <= today && (all || managed.Contains(a.PerimeterId)))
                .Select(a => new Item("assessment", a.Id, a.Reference, a.Title, "À renouveler")).ToListAsync());

            management = new { ToQualify = toQualify, InAnalysisNoRootCause = inAnalysisNoRootCause,
                               KnownErrorsNoAction = knownErrorsNoAction, OverdueActions = overdue,
                               MajorIncidents = majorIncidents, Decisions = decisions, ReviewsDue = reviewsDue,
                               // INC-24 : incidents en risque ou hors délai des périmètres gérés.
                               SlaIncidents = slaOpen.Where(x => all || managed.Contains(x.i.PerimeterId))
                                   .Select(x => new Item("incident", x.i.Id, x.i.Reference, x.i.Title, x.State)).ToList() };
        }

        return Ok(new { Role = role, Username = me, MyProblems = myProblems, MyActions = myActions,
                        MyAssignments = myAssignments, MySlaIncidents = mySla, Management = management });
    }

    /// <summary>Ligne de console désignant un enregistrement d'un processus.</summary>
    public record Item(string Type, int Id, string Reference, string Title, string Status);

    private static Task<List<Item>> Mine<T>(DbSet<T> set, string type, string meKey, List<string> groups, string[] done,
        bool all, List<int> seen) where T : Record =>
        set.AsNoTracking()
            .Where(x => x.OwnerId != null && !done.Contains(x.Status) && (all || seen.Contains(x.PerimeterId))
                        && (x.OwnerType == "Group" ? groups.Contains(x.OwnerId.ToLower()) : x.OwnerId.ToLower() == meKey))
            .OrderBy(x => x.CreatedAt)
            .Select(x => new Item(type, x.Id, x.Reference, x.Title, x.Status)).ToListAsync();

    private static Task<List<Item>> InStatus<T>(DbSet<T> set, string type, bool all, List<int> managed, params string[] statuses) where T : Record =>
        set.AsNoTracking().Where(x => statuses.Contains(x.Status) && (all || managed.Contains(x.PerimeterId))).OrderBy(x => x.CreatedAt)
            .Select(x => new Item(type, x.Id, x.Reference, x.Title, x.Status)).ToListAsync();
}
