using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Links;
using SAloha.Api.Core.Records;
using SAloha.Api.Modules.ContinualImprovement;

namespace SAloha.Api.Modules.ComplianceAssessment;

/// <summary>
/// Évaluations de conformité NIS 2 (docs/reference/processus/conformite-nis2.md).
/// Créer, valider, rouvrir : gestionnaires (NIS-02, NIS-07). Répondre : tout
/// rôle, tant que l'évaluation n'est pas validée (NIS-04, NIS-06).
/// </summary>
[Route("api/assessments")]
public class AssessmentsController(AppDbContext db) : RecordController<Assessment, AssessmentDto>(db)
{
    protected override string Code => "EVA";
    protected override string LinkType => "assessment";
    protected override string[] Statuses => Assessment.Statuses;
    protected override bool ManagerOnly => true;
    protected override bool RequiresManager(string status) => status == "Validée";

    protected override string? Apply(Assessment e, AssessmentDto dto, bool creating)
    {
        var error = Allowed.Check("Catégorie d'entité", dto.EntityCategory, Assessment.Categories);
        if (error is not null) return error;
        e.EntityCategory = dto.EntityCategory!;
        return null;
    }

    protected override async Task<string?> CheckStatusAsync(Assessment e, string status)
    {
        // NIS-07 : une évaluation validée est figée ; seul un gestionnaire la
        // rouvre (ManagerOnly), en changeant son statut.
        if (e.Status == "Validée" && status == "Validée" && e.Id != 0
            && Db.Entry(e).Properties.Any(p => p.IsModified && p.Metadata.Name != nameof(Record.UpdatedAt)))
            return "Une évaluation validée est figée : la rouvrir (statut « En cours ») pour la modifier.";
        if (status != "Validée" || e.Status == "Validée") return null;

        // NIS-05 : toutes les exigences applicables ont une réponse, et toute
        // exigence déclarée non applicable est justifiée.
        var (missing, unjustified) = await Completeness(e);
        if (missing > 0)
            return $"Validation impossible : {missing} exigence(s) applicable(s) sans réponse.";
        if (unjustified > 0)
            return $"Validation impossible : {unjustified} exigence(s) déclarée(s) non applicable(s) sans justification.";
        return null;
    }

    protected override void OnStatusChanged(Assessment e, string from)
    {
        if (e.Status == "Validée") { e.ValidatedAt = DateTime.UtcNow; e.ValidatedBy = MyDisplay; }
        else { e.ValidatedAt = null; e.ValidatedBy = null; }
    }

    private async Task<(int Missing, int Unjustified)> Completeness(Assessment e)
    {
        var requirements = await Db.SecurityRequirements.AsNoTracking().ToListAsync();
        var applicable = requirements.Where(e.Applies).Select(r => r.Id).ToHashSet();
        var responses = await Db.AssessmentResponses.AsNoTracking()
            .Where(r => r.AssessmentId == e.Id && applicable.Contains(r.RequirementId)).ToListAsync();
        var answered = responses.Where(r => r.NotApplicable || r.Score.HasValue).Select(r => r.RequirementId).ToHashSet();
        return (applicable.Count - answered.Count,
            responses.Count(r => r.NotApplicable && string.IsNullOrWhiteSpace(r.Justification)));
    }

    /// <summary>Questionnaire : exigences applicables, par objectif et thématique, avec leur réponse.</summary>
    [HttpGet("{id:int}/questionnaire")]
    public async Task<IActionResult> Questionnaire(int id)
    {
        var e = await Db.Assessments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        var objectives = await Db.SecurityObjectives.AsNoTracking().Include(o => o.Requirements)
            .OrderBy(o => o.Id).ToListAsync();
        var responses = await Db.AssessmentResponses.AsNoTracking()
            .Where(r => r.AssessmentId == id).ToDictionaryAsync(r => r.RequirementId);

        return Ok(new
        {
            ReferentialVersion = ReferentialStore.Version,
            Objectives = objectives.Select(o => new
            {
                o.Id, o.Title, o.Pillar,
                Themes = o.Requirements.Where(e.Applies).OrderBy(r => r.Order).GroupBy(r => r.Theme).Select(g => new
                {
                    Theme = g.Key,
                    Requirements = g.Select(r =>
                    {
                        responses.TryGetValue(r.Id, out var resp);
                        return new
                        {
                            r.Id, r.Code, r.IsoControls, r.Text,
                            resp?.Score, NotApplicable = resp?.NotApplicable ?? false,
                            Justification = resp?.Justification ?? "", resp?.UpdatedBy, resp?.UpdatedAt
                        };
                    })
                })
            }).Where(o => o.Themes.Any())
        });
    }

    /// <summary>Réponse à une exigence (NIS-03, NIS-04) : score 0 à 3, ou non applicable.</summary>
    [HttpPut("{id:int}/responses/{requirementId:int}")]
    public async Task<IActionResult> Answer(int id, int requirementId, ResponseDto dto)
    {
        var e = await Db.Assessments.FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        if (e.Status == "Validée")
            return BadRequest(new { message = "Une évaluation validée est figée : un gestionnaire doit la rouvrir." });
        var requirement = await Db.SecurityRequirements.FindAsync(requirementId);
        if (requirement is null) return NotFound();
        if (!e.Applies(requirement))
            return BadRequest(new { message = $"L'exigence {requirement.Code} ne s'applique pas à une {e.EntityCategory.ToLower()}." });
        if (!dto.NotApplicable && dto.Score is not (null or >= 0 and <= Scoring.Max))
            return BadRequest(new { message = $"Le score va de 0 à {Scoring.Max}." });

        var r = await Db.AssessmentResponses.FirstOrDefaultAsync(x => x.AssessmentId == id && x.RequirementId == requirementId);
        if (r is null) Db.AssessmentResponses.Add(r = new AssessmentResponse { AssessmentId = id, RequirementId = requirementId });
        r.NotApplicable = dto.NotApplicable;
        r.Score = dto.NotApplicable ? null : dto.Score;
        r.Justification = dto.Justification?.Trim() ?? "";
        r.UpdatedBy = MyDisplay; r.UpdatedAt = DateTime.UtcNow;
        // Une première réponse engage l'évaluation (NIS-03).
        if (e.Status == "Brouillon") e.Status = "En cours";
        e.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Ok(new { r.RequirementId, r.Score, r.NotApplicable, r.Justification, r.UpdatedBy, r.UpdatedAt, AssessmentStatus = e.Status });
    }

    /// <summary>Synthèse (NIS-10 à NIS-13) : scores, maturité, couverture et écarts.</summary>
    [HttpGet("{id:int}/score")]
    public async Task<IActionResult> Score(int id)
    {
        var e = await Db.Assessments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        var requirements = await Db.SecurityRequirements.AsNoTracking().Include(r => r.Objective)
            .OrderBy(r => r.Order).ToListAsync();
        var responses = await Db.AssessmentResponses.AsNoTracking()
            .Where(r => r.AssessmentId == id).ToDictionaryAsync(r => r.RequirementId);

        var items = requirements.Where(e.Applies).Select(r =>
        {
            responses.TryGetValue(r.Id, out var resp);
            return new { Req = r, Score = resp is { NotApplicable: false } ? resp.Score : null, Na = resp?.NotApplicable ?? false };
        }).ToList();

        var global = Scoring.Percent(items.Select(i => i.Score));
        return Ok(new
        {
            Percent = global,
            Maturity = global is null ? null : Scoring.MaturityOf(global.Value),
            Applicable = items.Count,
            Answered = items.Count(i => i.Score.HasValue || i.Na),
            NotApplicable = items.Count(i => i.Na),
            Pillars = items.GroupBy(i => i.Req.Objective!.Pillar).Select(g => new
            {
                Pillar = g.Key, Percent = Scoring.Percent(g.Select(i => i.Score))
            }),
            // Par identifiant, pas par instance : sans suivi (AsNoTracking), chaque
            // exigence porte sa propre copie de l'objectif.
            Objectives = items.GroupBy(i => i.Req.ObjectiveId).OrderBy(g => g.Key).Select(g => new
            {
                Id = g.Key, g.First().Req.Objective!.Title, g.First().Req.Objective!.Pillar,
                Percent = Scoring.Percent(g.Select(i => i.Score)),
                Applicable = g.Count(), Answered = g.Count(i => i.Score.HasValue || i.Na),
                Themes = g.GroupBy(i => i.Req.Theme).Select(t => new
                {
                    Theme = t.Key, Percent = Scoring.Percent(t.Select(i => i.Score)),
                    Applicable = t.Count(), Answered = t.Count(i => i.Score.HasValue || i.Na)
                })
            }),
            Gaps = items.Where(i => i.Score is < Scoring.Max)
                .OrderByDescending(i => Scoring.Gap(i.Score!.Value)).ThenBy(i => i.Req.Order)
                .Select(i => new
                {
                    RequirementId = i.Req.Id, i.Req.Code, i.Req.Theme, ObjectiveId = i.Req.ObjectiveId,
                    Score = i.Score!.Value, Gap = Scoring.Gap(i.Score!.Value)
                })
        });
    }

    /// <summary>
    /// NIS-15 : traite un écart par une action d'amélioration (registre
    /// d'amélioration continue), reliée à l'évaluation. Une seule par exigence.
    /// </summary>
    [HttpPost("{id:int}/improvements")]
    public async Task<IActionResult> CreateImprovement(int id, GapActionDto dto)
    {
        var e = await Db.Assessments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        var requirement = await Db.SecurityRequirements.Include(r => r.Objective).FirstOrDefaultAsync(r => r.Code == dto.Code);
        if (requirement is null) return NotFound();
        var response = await Db.AssessmentResponses.AsNoTracking()
            .FirstOrDefaultAsync(r => r.AssessmentId == id && r.RequirementId == requirement.Id);
        if (response is not { NotApplicable: false, Score: < Scoring.Max })
            return BadRequest(new { message = $"L'exigence {requirement.Code} ne présente pas d'écart noté." });

        var prefix = $"NIS 2 {requirement.Code} — ";
        var linked = await Db.ItemLinks.Where(l => l.FromType == LinkType && l.FromId == id && l.ToType == "improvement")
            .Select(l => l.ToId).ToListAsync();
        var existing = await Db.Improvements.AsNoTracking()
            .FirstOrDefaultAsync(i => linked.Contains(i.Id) && i.Title.StartsWith(prefix));
        if (existing is not null)
            return Conflict(new { message = $"Cet écart est déjà traité par {existing.Reference}.", existing.Id, existing.Reference });

        var improvement = await References.CreateAsync(Db, "AMI", () => new Improvement
        {
            Title = (prefix + requirement.Theme).Length > 200 ? (prefix + requirement.Theme)[..200] : prefix + requirement.Theme,
            Description = $"Écart relevé par l'évaluation {e.Reference} sur l'exigence {requirement.Code} "
                + $"(objectif de sécurité {requirement.ObjectiveId} — {requirement.Objective!.Title}) : "
                + $"score {response.Score}/{Scoring.Max}."
                + (string.IsNullOrWhiteSpace(response.Justification) ? "" : $"\n\nConstat : {response.Justification}"),
            Status = Improvement.Statuses[0], Step = 2, Priority = response.Score <= 1 ? "Élevée" : "Moyenne",
            Baseline = $"Score {response.Score}/{Scoring.Max}", Target = $"Score {Scoring.Max}/{Scoring.Max}",
            CreatedBy = Me, CreatedByDisplayName = MyDisplay
        });
        Db.ItemLinks.Add(new ItemLink { FromType = LinkType, FromId = id, ToType = "improvement", ToId = improvement.Id, CreatedBy = Me });
        await Db.SaveChangesAsync();
        return Ok(new { improvement.Id, improvement.Reference });
    }
}
