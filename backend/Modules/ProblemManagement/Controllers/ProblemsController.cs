using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Itil;
using SAloha.Api.Core.Links;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ProblemManagement;

/// <summary>
/// Problèmes sur le socle commun des pratiques (<see cref="RecordController{T,TDto}"/>) :
/// référence, transitions (SOC-05), transition forcée, périmètres (PER-03 à PER-07),
/// responsable AD = gestionnaire du problème (PRB-21). Tout utilisateur déclare un
/// problème ; le qualifier (statut, cause racine, contournement…) est une décision de
/// gestionnaire du périmètre.
/// </summary>
[Route("api/problems")]
public class ProblemsController(AppDbContext db) : RecordController<Problem, ProblemDto>(db)
{
    protected override string Code => "PRB";
    protected override string LinkType => "problem";
    protected override string[] Statuses => ProblemManagement.Problem.Statuses;
    protected override IReadOnlyDictionary<string, string[]> Transitions => ProblemManagement.Problem.Transitions;

    // Qualifier un problème : gestionnaire du périmètre (tableau des droits, PER-06).
    protected override bool CanEdit(Problem e) => IsManager;

    protected override string? Apply(Problem e, ProblemDto dto, bool creating)
    {
        // Impact et urgence : valeurs fermées (PRB-02, #28) ; code de clôture fermé (PRB-13).
        var error = Allowed.Check("Impact", dto.Impact, Priority.Impacts)
                    ?? Allowed.Check("Urgence", dto.Urgency, Priority.Urgencies)
                    ?? Allowed.Check("Code de clôture", dto.ClosureCode, ProblemRules.ClosureCodes, optional: true);
        if (error is not null) return error;
        e.Impact = dto.Impact; e.Urgency = dto.Urgency;
        e.Priority = Priority.Compute(dto.Impact, dto.Urgency);
        e.Category = dto.Category ?? ""; e.AffectedService = dto.AffectedService ?? "";
        // La déclaration ne qualifie pas : contournement, cause et clôture viennent ensuite.
        if (creating) return null;
        // PRB-21 : sans gestionnaire désigné, le gestionnaire qui qualifie le devient.
        if (string.IsNullOrWhiteSpace(e.OwnerId) && IsManager)
        {
            e.OwnerType = "User"; e.OwnerId = Me; e.OwnerDisplayName = MyDisplay;
        }
        e.KnownErrorWorkaround = dto.KnownErrorWorkaround;
        e.RootCause = dto.RootCause;
        e.ClosureCode = string.IsNullOrEmpty(dto.ClosureCode) ? null : dto.ClosureCode;
        return null;
    }

    /// <summary>
    /// Conditions du statut visé (PRB-11 à PRB-13), vérifiées à chaque
    /// enregistrement (SOC-06). L'absence d'action ouverte (PRB-12) se vérifie
    /// au passage à Résolu : les actions vivent leur vie ensuite.
    /// </summary>
    protected override async Task<string?> CheckStatusAsync(Problem p, string target)
    {
        var changing = target != p.Status;
        switch (target)
        {
            case "Erreur connue" when string.IsNullOrWhiteSpace(p.KnownErrorWorkaround):
                return "Documenter le contournement : une erreur connue en a toujours un.";
            case "Résolu":
                if (string.IsNullOrWhiteSpace(p.RootCause)) return "Renseigner la cause racine validée pour résoudre le problème.";
                if (!changing) return null;
                var actions = await Db.Actions.Where(a => a.ProblemId == p.Id).Select(a => a.Status).ToListAsync();
                if (actions.Any(s => ProblemRules.OpenActionStatuses.Contains(s)))
                    return "Des actions correctives sont encore ouvertes : les terminer ou les annuler avant de résoudre.";
                if (p.Status == "Erreur connue" && !actions.Contains("Terminée"))
                    return "Une erreur connue se résout par au moins une action corrective terminée.";
                return null;
            case "Clos":
                var allowed = changing ? ProblemRules.ClosureCodesFrom(p.Status) : ProblemRules.ClosureCodes;
                if (p.ClosureCode is null || !allowed.Contains(p.ClosureCode))
                    return $"Choisir un code de clôture : {string.Join(", ", allowed)}.";
                if (p.ClosureCode == "Doublon" && !await ItemLinks.IsLinkedToAsync(Db, "problem", p.Id, "problem"))
                    return "Un doublon doit être relié au problème conservé (onglet Liens).";
                return null;
            default:
                return null;
        }
    }

    protected override void OnStatusChanged(Problem p, string from)
    {
        // Rouvert, un problème n'est plus clos : garder ClosedAt le faisait
        // compter dans le MTTR comme résolu (M4, revue du 2026-10-06).
        p.ClosedAt = p.Status == "Clos" ? DateTime.UtcNow : null;
        // PRB-14 : ResolvedAt posé à l'entrée de Résolu, effacé au retour en analyse.
        if (p.Status == "Résolu") p.ResolvedAt = DateTime.UtcNow;
        else if (p.Status == "En analyse") p.ResolvedAt = null;
        // Une réouverture efface le code de clôture.
        if (from == "Clos") p.ClosureCode = null;
    }

    protected override IQueryable<Problem> WithDetails(IQueryable<Problem> q) =>
        q.Include(x => x.Analyses).Include(x => x.Actions).ThenInclude(a => a.Raci);

    // Le registre affiche le nombre d'actions, pas leur contenu.
    protected override async Task<IEnumerable<object>> ListItems(IQueryable<Problem> q) =>
        await q.Select(p => new
        {
            p.Id, p.Reference, p.Title, p.Status, p.Priority, p.Category, p.PerimeterId,
            p.AffectedService, p.OwnerId, p.OwnerDisplayName, p.CreatedByDisplayName, p.CreatedAt,
            ActionsCount = p.Actions.Count
        }).ToListAsync();
}
