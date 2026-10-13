using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Modules.ComplianceAssessment;

/// <summary>Exigence telle qu'une évaluation l'a évaluée (NIS-18).</summary>
public record EvaluatedRequirement(int Id, string Code, int ObjectiveId, string ObjectiveTitle, string Pillar, string Theme, int Order);

/// <summary>
/// NIS-18 : une évaluation est rattachée à la version du référentiel évaluée. À la
/// validation, la liste des exigences applicables (code, objectif, pilier, thématique)
/// est figée : une nouvelle structure embarquée — exigence ajoutée, applicabilité ou
/// objectif changés — ne modifie ni le score ni le questionnaire d'une évaluation
/// validée. La rouvrir la réaligne sur le référentiel en vigueur.
/// </summary>
public static class AssessmentReferential
{
    /// <summary>Exigences applicables selon le référentiel embarqué aujourd'hui.</summary>
    public static async Task<List<EvaluatedRequirement>> CurrentAsync(AppDbContext db, Assessment e) =>
        (await db.SecurityRequirements.AsNoTracking().Include(r => r.Objective).ToListAsync())
            .Where(e.Applies).OrderBy(r => r.Order)
            .Select(r => new EvaluatedRequirement(r.Id, r.Code, r.ObjectiveId, r.Objective!.Title, r.Objective.Pillar, r.Theme, r.Order))
            .ToList();

    /// <summary>Exigences de l'évaluation : figées si elle est validée, sinon celles du référentiel en vigueur.</summary>
    public static async Task<List<EvaluatedRequirement>> RequirementsAsync(AppDbContext db, Assessment e) =>
        e.FrozenRequirementsJson is { } json
            ? JsonSerializer.Deserialize<List<EvaluatedRequirement>>(json) ?? []
            : await CurrentAsync(db, e);

    /// <summary>Fige les exigences et la version à la validation ; les libère à la réouverture.</summary>
    public static async Task AlignAsync(AppDbContext db, Assessment e)
    {
        if (e.Status == "Validée")
        {
            e.FrozenRequirementsJson ??= JsonSerializer.Serialize(await CurrentAsync(db, e));
            return;
        }
        e.FrozenRequirementsJson = null;
        e.ReferentialVersion = ReferentialStore.Version;
    }

    /// <summary>
    /// Au démarrage, AVANT l'alignement de la structure embarquée : fige les évaluations
    /// validées qui ne le sont pas encore (antérieures à NIS-18), sur la structure qu'elles
    /// ont évaluée.
    /// </summary>
    public static void FreezeValidated(AppDbContext db)
    {
        if (!db.SecurityRequirements.Any()) return;
        var pending = db.Assessments.Where(a => a.Status == "Validée" && a.FrozenRequirementsJson == null).ToList();
        foreach (var a in pending) AlignAsync(db, a).GetAwaiter().GetResult();
        if (pending.Count > 0) db.SaveChanges();
    }
}
