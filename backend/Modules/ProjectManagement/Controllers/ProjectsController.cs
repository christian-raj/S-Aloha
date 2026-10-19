using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ProjectManagement;

/// <summary>
/// Projets (docs/reference/processus/gestion-de-projet.md). Tout rôle propose
/// un projet et en devient chef de projet s'il n'en désigne pas un autre
/// (PRJ-01) ; seuls le chef de projet et les gestionnaires le modifient (PRJ-04).
/// </summary>
[Route("api/projects")]
public class ProjectsController(AppDbContext db) : RecordController<Project, ProjectDto, ProjectView>(db)
{
    protected override ProjectView View(Project e) => ProjectView.From(e);
    protected override string Code => "PRJ";
    protected override string LinkType => "project";
    protected override string[] Statuses => Project.Statuses;

    // Statut enregistré, retenu par CanEdit (appelé avant RequiresManager) : la
    // validation se juge sur le passage Cadrage → En cours, pas sur le statut visé seul.
    private string? _savedStatus;

    protected override bool CanEdit(Project e)
    {
        _savedStatus = e.Status;
        // PRJ-04 : chef de projet, ou gestionnaire du périmètre du projet (PER-06).
        return IsManager || ProjectRights.IsOwner(e, User);
    }

    // PRJ-08 : le chef de projet propose, un gestionnaire valide le cadrage
    // (403 pour le chef de projet, même sur son projet). Reprendre un projet
    // suspendu ne demande pas de nouvelle validation.
    protected override bool RequiresManager(string status) => status == "En cours" && _savedStatus == "Cadrage";

    // PRJ-05 : cycle de vie du projet.
    protected override IReadOnlyDictionary<string, string[]>? Transitions => new Dictionary<string, string[]>
    {
        ["Cadrage"] = ["En cours", "Abandonné"],
        ["En cours"] = ["Suspendu", "Clôturé", "Abandonné"],
        ["Suspendu"] = ["En cours", "Abandonné"],
        // Clôturé et Abandonné sont définitifs : un projet relancé est un nouveau projet.
        ["Clôturé"] = [],
        ["Abandonné"] = [],
    };

    protected override string? Apply(Project e, ProjectDto dto, bool creating)
    {
        var error = Allowed.Check("Méthode", dto.Method, Methods.All);
        if (error is not null) return error;
        var unknown = (dto.Components ?? []).Except(Methods.Components).ToList();
        if (unknown.Count > 0) return $"Composant inconnu : {string.Join(", ", unknown)}.";
        if (dto.ToleranceCostPercent is < 0 || dto.ToleranceDays is < 0) return "Une tolérance ne peut pas être négative.";
        if (dto.Budget is < 0 || dto.ActualCost is < 0) return "Un montant ne peut pas être négatif.";

        e.Method = dto.Method!;
        // PRJ-03 : la méthode fixe les composants ; l'hybride les choisit, au
        // moins un (un projet sans composant n'aurait que ses informations).
        var components = e.Method == Methods.Hybrid
            ? (dto.Components is { Length: > 0 } chosen ? chosen : Methods.DefaultComponents(Methods.Hybrid))
            : Methods.DefaultComponents(e.Method);
        e.Components = string.Join(",", Methods.Components.Where(components.Contains));
        e.StartDate = Dates.Utc(dto.StartDate);
        e.TargetEndDate = Dates.Utc(dto.TargetEndDate);
        if (e.StartDate is not null && e.TargetEndDate is not null && e.TargetEndDate < e.StartDate)
            return "La date de fin cible précède la date de début.";
        e.Budget = dto.Budget; e.ActualCost = dto.ActualCost;
        e.Justification = dto.Justification; e.ExpectedBenefits = dto.ExpectedBenefits;
        e.ExpectedCosts = dto.ExpectedCosts; e.MajorRisks = dto.MajorRisks;
        e.ToleranceDays = dto.ToleranceDays; e.ToleranceCostPercent = dto.ToleranceCostPercent;
        e.ToleranceScope = dto.ToleranceScope; e.ToleranceQuality = dto.ToleranceQuality;
        e.AbandonReason = string.IsNullOrWhiteSpace(dto.AbandonReason) ? null : dto.AbandonReason.Trim();
        // PRJ-01 : sans chef de projet désigné, celui qui crée le projet l'est.
        if (creating && string.IsNullOrWhiteSpace(e.OwnerId))
        {
            e.OwnerType = "User"; e.OwnerId = Me; e.OwnerDisplayName = MyDisplay;
        }
        return null;
    }

    protected override string? CheckStatus(Project e, string status)
    {
        if (status == "En cours")
        {
            // PRJ-08 : un projet validé a un chef de projet et un calendrier ;
            // PRJ-06 : en PRINCE2, une affaire justifiée.
            if (string.IsNullOrWhiteSpace(e.OwnerId)) return "Désigner un chef de projet avant de lancer le projet.";
            if (e.StartDate is null || e.TargetEndDate is null) return "Renseigner les dates de début et de fin cible avant de lancer le projet.";
            if (e.Has(Methods.BusinessCase) && string.IsNullOrWhiteSpace(e.Justification))
                return "Le business case doit être justifié avant de lancer le projet.";
        }
        // PRJ-09 : un abandon se motive.
        if (status == "Abandonné" && string.IsNullOrWhiteSpace(e.AbandonReason))
            return "Indiquer le motif de l'abandon.";
        return null;
    }

    protected override void OnStatusChanged(Project e, string from)
    {
        e.ClosedAt = Project.Done.Contains(e.Status) ? DateTime.UtcNow : null;
        if (from == "Cadrage" && e.Status == "En cours") { e.ValidatedAt = DateTime.UtcNow; e.ValidatedBy = MyDisplay; }
    }

    /// <summary>PRJ-10, PRJ-20 : phases du V ou étapes PRINCE2 proposées à la création, modifiables ensuite.</summary>
    protected override async Task OnCreatedAsync(Project p)
    {
        var (kind, names) = p.Has(Methods.Stages) ? ("Étape", Methods.Prince2Stages)
            : p.Method == Methods.VModel ? ("Phase", Methods.VPhases) : ("", Array.Empty<string>());
        for (var i = 0; i < names.Length; i++)
            Db.ProjectPhases.Add(new ProjectPhase { ProjectId = p.Id, Kind = kind, Name = names[i], Order = i + 1, CreatedBy = Me });
        if (names.Length > 0) await Db.SaveChangesAsync();
    }

    /// <summary>
    /// Synthèse (PRJ-22, PRJ-30 à PRJ-33) : avancement par composant, tolérances et prévision
    /// (délai, coût) — « À surveiller » au-delà de la cible, « Hors tolérance »
    /// au-delà de la tolérance.
    /// </summary>
    [HttpGet("{id:int}/summary")]
    public async Task<IActionResult> Summary(int id)
    {
        var p = await Db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null || !await VisibleAsync(p)) return NotFound();
        var items = await Db.WorkItems.AsNoTracking().Where(w => w.ProjectId == id).ToListAsync();
        var phases = await Db.ProjectPhases.AsNoTracking().Where(x => x.ProjectId == id).ToListAsync();
        var deliverables = await Db.Deliverables.AsNoTracking().Where(x => x.ProjectId == id).ToListAsync();
        var milestones = await Db.Milestones.AsNoTracking().Where(x => x.ProjectId == id).ToListAsync();
        var risks = await Db.ProjectRisks.AsNoTracking().Where(x => x.ProjectId == id && x.Status == "Ouvert").ToListAsync();
        var issues = await Db.ProjectIssues.AsNoTracking().CountAsync(x => x.ProjectId == id && x.Status != "Clos");
        var today = DateTime.UtcNow.Date;
        var sprints = await Db.Sprints.AsNoTracking().Where(x => x.ProjectId == id).ToListAsync();

        return Ok(new
        {
            Components = p.Components.Split(',', StringSplitOptions.RemoveEmptyEntries),
            Tolerances = new { Time = ProjectHealth.TimeTolerance(p, today), Cost = ProjectHealth.CostTolerance(p) },
            // PRJ-22 : date de fin prévue, et sa position par rapport à la tolérance de délai.
            Forecast = ProjectHealth.ForecastEnd(p, items, sprints, phases, today),
            WorkItems = new { Total = items.Count, Done = items.Count(w => w.Status == "Terminé"),
                Points = items.Sum(w => w.Points ?? 0), PointsDone = items.Where(w => w.Status == "Terminé").Sum(w => w.Points ?? 0) },
            Phases = new { Total = phases.Count, Done = phases.Count(x => x.Status == "Terminée"),
                Current = phases.OrderBy(x => x.Order).FirstOrDefault(x => x.Status == "En cours")?.Name },
            Deliverables = new { Total = deliverables.Count, Validated = deliverables.Count(x => x.Status == "Validé") },
            Milestones = new { Total = milestones.Count, Achieved = milestones.Count(x => x.AchievedAt != null),
                Late = milestones.Count(x => x.AchievedAt == null && x.DueDate != null && x.DueDate.Value.Date < today) },
            Risks = new { Open = risks.Count, Critical = risks.Count(r => r.Score >= 15) },
            OpenIssues = issues
        });
    }
}

/// <summary>Droits communs au projet et à ses éléments.</summary>
public static class ProjectRights
{
    /// <summary>PRJ-04 : chef de projet (responsable du projet) ou gestionnaire de son périmètre (PER-06).</summary>
    public static bool IsLead(Project p, System.Security.Claims.ClaimsPrincipal user, Core.Perimeters.PerimeterScope scope) =>
        scope.ManagerOn(p.PerimeterId) || IsOwner(p, user);

    /// <summary>Le chef de projet est une personne (pas un groupe).</summary>
    public static bool IsOwner(Project p, System.Security.Claims.ClaimsPrincipal user) =>
        p.OwnerType != "Group" && p.OwnerId is not null
        && string.Equals(p.OwnerId, user.Identity?.Name, StringComparison.OrdinalIgnoreCase);
}
