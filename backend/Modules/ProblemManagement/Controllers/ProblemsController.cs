using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Itil;
using SAloha.Api.Core.Links;
using SAloha.Api.Core.Records;
using SAloha.Api.Core.Notifications;

namespace SAloha.Api.Modules.ProblemManagement;

/// <summary>
/// Problèmes sur le socle commun des pratiques (<see cref="RecordController{T,TDto}"/>) :
/// référence, transitions (SOC-05), transition forcée, périmètres (PER-03 à PER-07),
/// responsable AD = gestionnaire du problème (PRB-21). Tout utilisateur déclare un
/// problème ; le qualifier (statut, cause racine, contournement…) est une décision de
/// gestionnaire du périmètre.
/// </summary>
[Route("api/problems")]
public class ProblemsController(AppDbContext db) : RecordController<Problem, ProblemDto, ProblemView>(db)
{
    protected override ProblemView View(Problem e) => ProblemView.From(e);
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
                    ?? Allowed.Check("Code de clôture", dto.ClosureCode, ProblemRules.ClosureCodes, optional: true)
                    ?? Allowed.Check("Source", dto.Source, ProblemManagement.Problem.Sources, optional: true);
        if (error is not null) return error;
        e.Impact = dto.Impact; e.Urgency = dto.Urgency;
        e.Priority = Priority.Compute(dto.Impact, dto.Urgency);
        e.Category = dto.Category ?? ""; e.AffectedService = dto.AffectedService ?? ""; e.ServiceId = dto.ServiceId;
        // PRB-20 : source du problème, réactive par défaut.
        if (dto.Source is not null) e.Source = dto.Source;
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
        e.ReopenReason = string.IsNullOrWhiteSpace(dto.ReopenReason) ? null : dto.ReopenReason.Trim();
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
        // PRB-10 : rouvrir un problème résolu ou clos exige un motif.
        if (changing && target == "En analyse" && p.Status is "Résolu" or "Clos" && string.IsNullOrWhiteSpace(p.ReopenReason))
            return "Motiver la réouverture du problème.";
        // PRB-20 : un problème réactif quitte Nouveau relié à au moins un incident (sauf clôture).
        if (changing && p.Status == "Nouveau" && target != "Clos" && p.Source == "Réactif"
            && (p.Id == 0 || !await ItemLinks.IsLinkedToAsync(Db, "problem", p.Id, "incident")))
            return "Problème réactif : le relier à au moins un incident (onglet Liens) avant de l'analyser, ou le déclarer proactif.";
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

    // SLM-10 : service du catalogue (vérifié ici, recopié sur l'enregistrement juste avant l'écriture).
    // ADM-51 : catégorie tirée de la liste de la pratique ; SLM-10 : service du catalogue.
    protected override async Task<string?> ValidateAsync(Problem e)
    {
        var before = Db.Entry(e).State is Microsoft.EntityFrameworkCore.EntityState.Detached or Microsoft.EntityFrameworkCore.EntityState.Added ? null : Db.Entry(e).Property(x => x.Category).OriginalValue;
        var (error, value) = await Core.Itil.Category.CheckAsync(Db, "problem", e.Category, before);
        e.Category = value;
        return error ?? await ServiceCatalog.ResolveAsync(Db, e);
    }
    protected override async Task BeforeSaveAsync(Problem e, bool creating)
    {
        await ServiceCatalog.ResolveAsync(Db, e);
        // ADM-51 : la création valide une copie ; le nom exact de la liste est repris ici.
        if (creating) e.Category = (await Core.Itil.Category.CheckAsync(Db, "problem", e.Category, null)).Value;
    }

    // PRB-31 : problème qualifié ou clos → déclarant.
    protected override void OnNotify(Problem p, string? from, Notifier n)
    {
        if (from == "Nouveau" && p.Status != "Clos") n.ToCreator(p, LinkType, "Problème qualifié");
        if (from is not null && from != "" && p.Status == "Clos") n.ToCreator(p, LinkType, "Problème clos");
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
        // PRB-10 : le motif vaut pour la réouverture en cours ; effacé à la résolution ou
        // à la clôture suivante (l'historique le garde), une nouvelle réouverture en exige un.
        if (p.Status is "Résolu" or "Clos") p.ReopenReason = null;
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
