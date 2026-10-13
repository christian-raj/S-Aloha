using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Perimeters;
using SAloha.Api.Modules.ChangeEnablement;
using SAloha.Api.Modules.ComplianceAssessment;
using SAloha.Api.Modules.IncidentManagement;
using SAloha.Api.Modules.ProjectManagement;

namespace SAloha.Api.Core.Pilotage;

/// <summary>
/// Tableau de bord du reporting, un point d'entrée par onglet (règles REP-xx,
/// docs/reference/reporting.md). Filtres globaux, communs aux onglets :
/// période (<c>from</c>, <c>to</c> ; 30 derniers jours par défaut) et service
/// affecté (<c>service</c>, incidents et problèmes : les autres flux n'ont pas
/// de service).
/// </summary>
[ApiController]
[Route("api/reports/dashboard")]
[Authorize(Policy = "User")]
public class DashboardController(AppDbContext db, PerimeterAccess access) : ControllerBase
{
    private ReportScope _scope = null!;
    private IQueryable<T> In<T>(IQueryable<T> q) where T : Records.Record => _scope.In(q);

    public record Filters(DateTime From, DateTime To, string? Service);

    private static Filters Read(DateTime? from, DateTime? to, string? service)
    {
        var end = (to?.Date ?? DateTime.UtcNow.Date).AddDays(1);              // borne exclusive
        var start = from?.Date ?? end.AddDays(-30);
        return new Filters(DateTime.SpecifyKind(start, DateTimeKind.Utc), DateTime.SpecifyKind(end, DateTimeKind.Utc),
            string.IsNullOrWhiteSpace(service) ? null : service.Trim().ToLower());
    }

    private static bool InService(string affected, Filters f) => f.Service is null || affected.Trim().ToLower() == f.Service;
    private static double? Pct(int part, int total) => total == 0 ? null : Math.Round(100.0 * part / total, 1);
    private static double? Avg(IEnumerable<double> v) { var l = v.ToList(); return l.Count == 0 ? null : Math.Round(l.Average(), 1); }
    private static readonly string[] OpenProjects = ["En cours", "Suspendu"];

    private async Task<List<Incident>> Incidents(Filters f) =>
        (await In(db.Incidents.AsNoTracking()).ToListAsync()).Where(i => InService(i.AffectedService, f)).ToList();

    /// <summary>REP-05 : état des tolérances d'un projet (même calcul que sa synthèse, PRJ-31/32).</summary>
    private static (string Time, string Cost) Tolerance(Project p)
    {
        var today = DateTime.UtcNow.Date;
        string time = p.TargetEndDate is null ? "Sans objet"
            : today <= p.TargetEndDate.Value.Date ? "Dans la cible"
            : p.ToleranceDays is { } d && today <= p.TargetEndDate.Value.Date.AddDays(d) ? "À surveiller" : "Hors tolérance";
        string cost = p.Budget is not { } b || p.ActualCost is not { } c ? "Sans objet"
            : c <= b ? "Dans la cible"
            : p.ToleranceCostPercent is { } t && c <= b * (1 + t / 100) ? "À surveiller" : "Hors tolérance";
        return (time, cost);
    }

    /// <summary>Onglet Synthèse globale (REP-01 à REP-06) et badges des onglets (REP-07).</summary>
    [HttpGet("overview")]
    public async Task<IActionResult> Overview([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? service,
        [FromQuery] int? perimeter)
    {
        _scope = await ReportScope.ForAsync(access, perimeter);
        var f = Read(from, to, service);
        var now = DateTime.UtcNow;
        var incidents = await Incidents(f);
        var open = incidents.Where(i => !Incident.Done.Contains(i.Status)).ToList();
        var problems = (await In(db.Problems.AsNoTracking()).ToListAsync()).Where(p => InService(p.AffectedService, f)).ToList();
        var changes = await In(db.Changes.AsNoTracking()).ToListAsync();
        var requests = await In(db.ServiceRequests.AsNoTracking()).Where(r => r.CreatedAt >= f.From && r.CreatedAt < f.To).ToListAsync();
        var projects = await In(db.Projects.AsNoTracking()).Where(p => OpenProjects.Contains(p.Status)).ToListAsync();

        // REP-01 : score de santé composé, formule affichée.
        var majorOpen = open.Count(i => i.IsMajor);
        var p1Open = open.Count(i => i.Priority == "P1" && !i.IsMajor);
        var breached = open.Count(i => i.ResolutionDueAt < now);
        var failed = changes.Count(c => c.Outcome == "Échoué" && c.ClosedAt >= f.From && c.ClosedAt < f.To);
        var outOfTolerance = projects.Count(p => Tolerance(p) is var t && (t.Time == "Hors tolérance" || t.Cost == "Hors tolérance"));
        var factors = new[]
        {
            new { Label = "Incidents majeurs ouverts", Count = majorOpen, Penalty = Math.Min(50, majorOpen * 25), Rule = "−25 chacun, 50 au plus" },
            new { Label = "Incidents P1 ouverts (non majeurs)", Count = p1Open, Penalty = Math.Min(30, p1Open * 10), Rule = "−10 chacun, 30 au plus" },
            new { Label = "Incidents ouverts hors SLA", Count = breached, Penalty = Math.Min(25, breached * 5), Rule = "−5 chacun, 25 au plus" },
            new { Label = "Changements en échec sur la période", Count = failed, Penalty = Math.Min(20, failed * 5), Rule = "−5 chacun, 20 au plus" },
            new { Label = "Projets hors tolérance", Count = outOfTolerance, Penalty = Math.Min(20, outOfTolerance * 10), Rule = "−10 chacun, 20 au plus" },
        };
        var score = Math.Max(0, 100 - factors.Sum(x => x.Penalty));

        // REP-02 : taux de résolution dans le SLA (INC-21), sur la période.
        var resolvedWithDue = incidents.Where(i => i.ResolvedAt >= f.From && i.ResolvedAt < f.To && i.ResolutionDueAt != null).ToList();
        // Semaine calendaire courante, du lundi au dimanche (UTC).
        var monday = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
        var thisWeek = changes.Count(c => c.PlannedStart >= monday && c.PlannedStart < monday.AddDays(7) && c.Status != "Rejeté");
        var atRisk = projects.Select(p => new { p, t = Tolerance(p) })
            .Where(x => x.t.Time != "Dans la cible" && x.t.Time != "Sans objet" || x.t.Cost != "Dans la cible" && x.t.Cost != "Sans objet")
            .Select(x => new { x.p.Id, x.p.Reference, x.p.Title, x.t.Time, x.t.Cost }).ToList();

        // REP-03 : volumétrie croisée, par jour.
        var days = Enumerable.Range(0, (int)(f.To - f.From).TotalDays).Select(n => f.From.AddDays(n)).ToList();
        var volume = days.Select(d => new
        {
            Date = d,
            Incidents = incidents.Count(i => i.CreatedAt >= d && i.CreatedAt < d.AddDays(1)),
            Requests = requests.Count(r => r.CreatedAt >= d && r.CreatedAt < d.AddDays(1)),
            // Mise en œuvre (début planifié) des changements autorisés : c'est elle qui peut provoquer des incidents.
            Changes = changes.Count(c => c.PlannedStart >= d && c.PlannedStart < d.AddDays(1) && c.Status is "Planifié" or "Mis en œuvre" or "Clos"),
        });

        // REP-04 : matrice des priorités des flux ouverts.
        string Level(string p) => p switch { "P1" => "Critique", "P2" => "Élevée", "P3" => "Moyenne", _ => "Basse" };
        var openProblems = problems.Where(p => p.Status is not ("Résolu" or "Clos")).ToList();
        var matrix = new[] { "Critique", "Élevée", "Moyenne", "Basse" }.Select(level => new
        {
            Level = level,
            Incidents = open.Count(i => Level(i.Priority) == level),
            Problems = openProblems.Count(p => Level(p.Priority) == level),
        });

        var rcaPending = await RcaPendingCount(problems);
        var slaToReview = await In(db.Agreements).CountAsync(a => a.Status == "En vigueur" && a.ReviewDate < now.Date);
        return Ok(new
        {
            Period = new { f.From, To = f.To.AddDays(-1) },
            Health = new { Score = score, Level = score >= 80 ? "Bon" : score >= 50 ? "Dégradé" : "Critique", Factors = factors },
            Kpis = new
            {
                MajorIncidentsOpen = majorOpen,
                SlaRate = Pct(resolvedWithDue.Count(i => i.ResolvedAt <= i.ResolutionDueAt), resolvedWithDue.Count),
                SlaMeasured = resolvedWithDue.Count,
                ChangesThisWeek = thisWeek,
                ProjectsAtRisk = atRisk.Count,
            },
            ProjectsAtRisk = atRisk,
            Volume = volume,
            PriorityMatrix = matrix,
            // REP-07 : alertes portées par les onglets.
            Badges = new
            {
                Incidents = new { Count = majorOpen + breached, Tone = majorOpen > 0 ? "bad" : "wait" },
                Problems = new { Count = rcaPending, Tone = "wait" },
                ChangesProjects = new { Count = outOfTolerance + failed, Tone = outOfTolerance > 0 ? "bad" : "wait" },
                Compliance = new { Count = slaToReview, Tone = "wait" },
            }
        });
    }

    /// <summary>REP-22 : problèmes en analyse dont une analyse conclut, sans cause racine validée.</summary>
    private async Task<int> RcaPendingCount(List<Modules.ProblemManagement.Problem> problems)
    {
        var ids = problems.Where(p => p.Status == "En analyse" && string.IsNullOrWhiteSpace(p.RootCause)).Select(p => p.Id).ToList();
        return await db.Analyses.Where(a => ids.Contains(a.ProblemId) && a.Conclusion != null && a.Conclusion != "")
            .Select(a => a.ProblemId).Distinct().CountAsync();
    }

    /// <summary>Onglet Incidents (REP-10 à REP-15).</summary>
    [HttpGet("incidents")]
    public async Task<IActionResult> IncidentsTab([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? service,
        [FromQuery] int? perimeter)
    {
        _scope = await ReportScope.ForAsync(access, perimeter);
        var f = Read(from, to, service);
        var now = DateTime.UtcNow;
        var all = await Incidents(f);
        var created = all.Where(i => i.CreatedAt >= f.From && i.CreatedAt < f.To).ToList();
        var resolved = all.Where(i => i.ResolvedAt >= f.From && i.ResolvedAt < f.To).ToList();
        var open = all.Where(i => !Incident.Done.Contains(i.Status)).ToList();
        var days = Enumerable.Range(0, (int)(f.To - f.From).TotalDays).Select(n => f.From.AddDays(n)).ToList();
        string Sla(Incident i)
        {
            if (i.ResolutionDueAt is not { } due) return "Sans SLA";
            if (now > due) return "SLA dépassé";
            var window = (due - i.CreatedAt).TotalMinutes;
            return window > 0 && (due - now).TotalMinutes < window * 0.25 ? "SLA en risque" : "Dans le SLA";   // INC-24
        }
        return Ok(new
        {
            Kpis = new
            {
                MttrHours = Avg(resolved.Select(i => (i.ResolvedAt!.Value - i.CreatedAt).TotalHours)),           // REP-10
                MttdHours = Avg(created.Where(i => i.OccurredAt != null).Select(i => (i.CreatedAt - i.OccurredAt!.Value).TotalHours)), // REP-11, INC-27
                MttdMeasured = created.Count(i => i.OccurredAt != null),
                Fcr = Pct(resolved.Count(i => i.FirstContactResolution == true), resolved.Count(i => i.FirstContactResolution != null)), // REP-12, INC-23
                Resolved = resolved.Count,
                Open = open.Count,
            },
            // REP-13 : ancienneté du backlog ouvert.
            Backlog = new[]
            {
                new { Bucket = "< 24 h", Count = open.Count(i => now - i.CreatedAt < TimeSpan.FromHours(24)) },
                new { Bucket = "1 à 7 j", Count = open.Count(i => now - i.CreatedAt >= TimeSpan.FromHours(24) && now - i.CreatedAt < TimeSpan.FromDays(7)) },
                new { Bucket = "> 7 j", Count = open.Count(i => now - i.CreatedAt >= TimeSpan.FromDays(7)) },
            },
            // REP-14 : burn-up — cumul des créés et des résolus depuis le début de la période.
            BurnUp = days.Select(d => new
            {
                Date = d,
                Created = created.Count(i => i.CreatedAt < d.AddDays(1)),
                Resolved = resolved.Count(i => i.ResolvedAt < d.AddDays(1)),
            }),
            ByCategory = created.GroupBy(i => i.Category == "" ? "Non catégorisé" : i.Category)
                .Select(g => new { Label = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count),
            ByService = created.GroupBy(i => i.AffectedService == "" ? "Non renseigné" : i.AffectedService)
                .Select(g => new { Label = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count),
            // REP-15 : incidents à suivre — majeurs, et SLA en risque ou dépassé.
            Watch = open.Select(i => new { i, sla = Sla(i) }).Where(x => x.i.IsMajor || x.sla is "SLA en risque" or "SLA dépassé")
                .OrderByDescending(x => x.i.IsMajor).ThenBy(x => x.i.ResolutionDueAt)
                .Select(x => new { x.i.Id, x.i.Reference, x.i.Title, x.i.Priority, x.i.IsMajor, x.i.Status, x.i.OwnerDisplayName,
                    x.i.ResolutionDueAt, Sla = x.sla, x.i.AffectedService }),
        });
    }

    /// <summary>Onglet Problèmes (REP-20 à REP-25).</summary>
    [HttpGet("problems")]
    public async Task<IActionResult> ProblemsTab([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? service,
        [FromQuery] int? perimeter)
    {
        _scope = await ReportScope.ForAsync(access, perimeter);
        var f = Read(from, to, service);
        var today = DateTime.UtcNow.Date;
        var problems = (await In(db.Problems.AsNoTracking()).ToListAsync()).Where(p => InService(p.AffectedService, f)).ToList();
        var ids = problems.Select(p => p.Id).ToHashSet();
        var links = await db.ItemLinks.AsNoTracking()
            .Where(l => (l.FromType == "problem" && l.ToType == "incident") || (l.FromType == "incident" && l.ToType == "problem")).ToListAsync();
        int IncidentsOf(int pid) => links.Count(l => (l.FromType == "problem" && l.FromId == pid) || (l.ToType == "problem" && l.ToId == pid));
        var changeLinked = (await db.ItemLinks.AsNoTracking()
            .Where(l => (l.FromType == "problem" && l.ToType == "change") || (l.FromType == "change" && l.ToType == "problem")).ToListAsync())
            .Select(l => l.FromType == "problem" ? l.FromId : l.ToId).ToHashSet();
        var actions = (await db.Actions.AsNoTracking().Include(a => a.Raci).Include(a => a.Problem).ToListAsync())
            .Where(a => ids.Contains(a.ProblemId)).ToList();
        var openActions = actions.Where(a => a.Status is not ("Terminée" or "Annulée")).ToList();
        var open = problems.Where(p => p.Status is not ("Résolu" or "Clos")).ToList();
        bool FixPlanned(Modules.ProblemManagement.Problem p) => openActions.Any(a => a.ProblemId == p.Id) || changeLinked.Contains(p.Id);

        return Ok(new
        {
            Kpis = new
            {
                Open = open.Count,
                KnownErrors = problems.Count(p => p.Status == "Erreur connue"),                                     // REP-20
                IncidentsPerProblem = problems.Count == 0 ? (double?)null : Math.Round(problems.Average(p => (double)IncidentsOf(p.Id)), 1), // REP-21
                RcaPending = await RcaPendingCount(problems),                                                       // REP-22
            },
            // REP-23 : services qui génèrent le plus de problèmes, et les incidents qui leur sont liés.
            TopServices = problems.Where(p => p.AffectedService != "").GroupBy(p => p.AffectedService)
                .Select(g => new { Label = g.Key, Count = g.Count(), Incidents = g.Sum(p => IncidentsOf(p.Id)) })
                .OrderByDescending(x => x.Count).ThenByDescending(x => x.Incidents).Take(5),
            // REP-24 : entonnoir — où en sont les problèmes (statut courant, correctif planifié = action ou changement).
            Funnel = new[]
            {
                new { Stage = "Identifié", Count = problems.Count(p => p.Status == "Nouveau") },
                new { Stage = "RCA en cours", Count = problems.Count(p => p.Status == "En analyse") },
                new { Stage = "Contournement trouvé", Count = problems.Count(p => p.Status == "Erreur connue" && !FixPlanned(p)) },
                new { Stage = "Correctif planifié", Count = open.Count(p => p.Status != "Nouveau" && FixPlanned(p)) },
                new { Stage = "Clôturé", Count = problems.Count(p => p.Status is "Résolu" or "Clos") },
            },
            // REP-25 : actions correctives.
            Actions = new
            {
                Open = openActions.Count,
                Overdue = openActions.Count(a => a.DueDate < today),
                Done = actions.Count(a => a.CompletedAt >= f.From && a.CompletedAt < f.To),
                OverdueList = openActions.Where(a => a.DueDate < today).OrderBy(a => a.DueDate).Select(a => new
                {
                    a.Id, a.Title, a.DueDate, Problem = new { a.Problem!.Id, a.Problem.Reference },
                    Responsibles = a.Raci.Where(r => r.Role == "R").Select(r => r.AssigneeDisplayName)
                }),
            }
        });
    }

    /// <summary>Onglet Changements & projets (REP-30 à REP-34).</summary>
    [HttpGet("changes-projects")]
    public async Task<IActionResult> ChangesProjects([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? perimeter)
    {
        _scope = await ReportScope.ForAsync(access, perimeter);
        var f = Read(from, to, null);
        var today = DateTime.UtcNow.Date;
        var changes = await In(db.Changes.AsNoTracking()).ToListAsync();
        var period = changes.Where(c => c.CreatedAt >= f.From && c.CreatedAt < f.To).ToList();
        var outcomes = changes.Where(c => c.Outcome != null && c.ClosedAt >= f.From && c.ClosedAt < f.To).ToList();
        var projects = await In(db.Projects.AsNoTracking()).Where(p => OpenProjects.Contains(p.Status)).ToListAsync();
        var pids = projects.Select(p => p.Id).ToList();
        var items = await db.WorkItems.AsNoTracking().Where(w => pids.Contains(w.ProjectId)).ToListAsync();
        var phases = await db.ProjectPhases.AsNoTracking().Where(w => pids.Contains(w.ProjectId)).ToListAsync();
        var deliverables = await db.Deliverables.AsNoTracking().Where(w => pids.Contains(w.ProjectId)).ToListAsync();
        var risks = await db.ProjectRisks.AsNoTracking().Where(r => pids.Contains(r.ProjectId) && r.Status == "Ouvert").ToListAsync();

        // REP-32 : avancement d'un projet, selon ses composants — points terminés,
        // sinon phases ou étapes terminées, sinon livrables validés.
        double? Progress(Project p)
        {
            var pts = items.Where(w => w.ProjectId == p.Id).Sum(w => w.Points ?? 0);
            if (pts > 0) return 100.0 * items.Where(w => w.ProjectId == p.Id && w.Status == "Terminé").Sum(w => w.Points ?? 0) / pts;
            var ph = phases.Where(x => x.ProjectId == p.Id).ToList();
            if (ph.Count > 0) return 100.0 * ph.Count(x => x.Status == "Terminée") / ph.Count;
            var dl = deliverables.Where(x => x.ProjectId == p.Id).ToList();
            return dl.Count > 0 ? 100.0 * dl.Count(x => x.Status == "Validé") / dl.Count : null;
        }
        var progress = projects.Select(p => new { p.Id, p.Reference, p.Title, p.Method, p.Status, Progress = Progress(p) is { } v ? Math.Round(v) : (double?)null,
            // Objet nommé : un tuple se sérialise vide en JSON.
            Tolerance = Tolerance(p) is var t ? new { t.Time, t.Cost } : null }).ToList();

        return Ok(new
        {
            Kpis = new
            {
                SuccessRate = Pct(outcomes.Count(c => c.Outcome == "Réussi"), outcomes.Count),                       // REP-30
                Implemented = outcomes.Count,
                ByType = Change.Types.Select(t => new { Type = t, Count = period.Count(c => c.ChangeType == t) }),     // REP-31
                ProjectsProgress = Avg(progress.Where(p => p.Progress != null).Select(p => p.Progress!.Value)),       // REP-32
                ActiveProjects = projects.Count,
            },
            Projects = progress,
            // REP-33 : calendrier des 30 prochains jours.
            Upcoming = changes.Where(c => c.PlannedStart >= today && c.PlannedStart < today.AddDays(30)
                                          && c.Status is not ("Rejeté" or "Clos"))
                .OrderBy(c => c.PlannedStart)
                .Select(c => new { c.Id, c.Reference, c.Title, c.ChangeType, c.Risk, c.Status, c.PlannedStart, c.PlannedEnd }),
            // REP-34 : matrice des risques projets ouverts (probabilité × impact).
            RiskMatrix = risks.GroupBy(r => new { r.Probability, r.Impact })
                .Select(g => new { g.Key.Probability, g.Key.Impact, Count = g.Count(),
                    Risks = g.Select(r => new { r.Title, Project = projects.First(p => p.Id == r.ProjectId).Reference }) }),
        });
    }

    /// <summary>Onglet Conformité & métriques (REP-40 à REP-43).</summary>
    [HttpGet("compliance")]
    public async Task<IActionResult> Compliance([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? service,
        [FromQuery] int? perimeter)
    {
        _scope = await ReportScope.ForAsync(access, perimeter);
        var f = Read(from, to, service);
        var today = DateTime.UtcNow.Date;

        // REP-40 : dernière évaluation NIS 2 validée — score, maturité, piliers (NIS-10 à NIS-12).
        var last = await In(db.Assessments.AsNoTracking()).Where(a => a.Status == "Validée")
            .OrderByDescending(a => a.ValidatedAt).FirstOrDefaultAsync();
        object? nis2 = null;
        if (last is not null)
        {
            // NIS-18 : les exigences figées à la validation, pas celles du référentiel en vigueur.
            var reqs = await AssessmentReferential.RequirementsAsync(db, last);
            var resp = await db.AssessmentResponses.AsNoTracking().Where(r => r.AssessmentId == last.Id).ToDictionaryAsync(r => r.RequirementId);
            int? ScoreOf(EvaluatedRequirement r) => resp.TryGetValue(r.Id, out var x) && !x.NotApplicable ? x.Score : null;
            var percent = Scoring.Percent(reqs.Select(ScoreOf));
            var improvements = await db.ItemLinks.CountAsync(l => l.FromType == "assessment" && l.FromId == last.Id && l.ToType == "improvement");
            nis2 = new
            {
                last.Id, last.Reference, last.Title, last.ValidatedAt, Percent = percent,
                Maturity = percent is null ? null : Scoring.MaturityOf(percent.Value),
                Pillars = reqs.GroupBy(r => r.Pillar).Select(g => new { Pillar = g.Key, Percent = Scoring.Percent(g.Select(ScoreOf)) }),
                Gaps = reqs.Count(r => ScoreOf(r) is < Scoring.Max),
                Improvements = improvements,
            };
        }

        // REP-41 : respect des SLA par service (incidents résolus sur la période, INC-21).
        var incidents = (await Incidents(f)).Where(i => i.ResolvedAt >= f.From && i.ResolvedAt < f.To && i.ResolutionDueAt != null).ToList();
        var sla = incidents.GroupBy(i => i.AffectedService).Select(g => new
        {
            Service = g.Key, Resolved = g.Count(), WithinSla = g.Count(i => i.ResolvedAt <= i.ResolutionDueAt),
            Rate = Pct(g.Count(i => i.ResolvedAt <= i.ResolutionDueAt), g.Count())
        }).OrderBy(x => x.Rate);
        var toReview = await In(db.Agreements.AsNoTracking()).Include(a => a.Service)
            .Where(a => a.Status == "En vigueur" && a.ReviewDate < today)
            .Select(a => new { a.Id, a.Reference, a.Title, Service = a.Service!.Title, a.ReviewDate }).ToListAsync();

        // REP-42 : amélioration continue ; REP-43 : connaissances.
        var improvementsAll = await In(db.Improvements.AsNoTracking()).ToListAsync();
        var articles = await In(db.KnowledgeArticles.AsNoTracking()).ToListAsync();
        return Ok(new
        {
            Nis2 = nis2,
            // Évaluations non validées : signalées, sans score (seul un score validé engage).
            Nis2Pending = await In(db.Assessments.AsNoTracking()).Where(a => a.Status != "Validée")
                .OrderByDescending(a => a.CreatedAt).Select(a => new { a.Id, a.Reference, a.Title, a.Status }).ToListAsync(),
            Sla = new { ByService = sla, ToReview = toReview },
            Improvements = new
            {
                ByStatus = Modules.ContinualImprovement.Improvement.Statuses.Select(s => new { Status = s, Count = improvementsAll.Count(i => i.Status == s) }),
                Completed = improvementsAll.Count(i => i.CompletedAt >= f.From && i.CompletedAt < f.To),
            },
            Knowledge = new
            {
                Published = articles.Count(a => a.Status == "Publié"),
                Drafts = articles.Count(a => a.Status == "Brouillon"),
                ReviewOverdue = articles.Count(a => a.Status == "Publié" && a.ReviewDate < today),
                PublishedInPeriod = articles.Count(a => a.PublishedAt >= f.From && a.PublishedAt < f.To),
            },
            // Déclarations réglementaires (DRG) : ajoutées à cet onglet avec leur module.
        });
    }
}
