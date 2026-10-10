using Microsoft.AspNetCore.Mvc;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ContinualImprovement;

[Route("api/improvements")]
public class ImprovementsController(AppDbContext db) : RecordController<Improvement, ImprovementDto>(db)
{
    protected override string Code => "AMI";
    protected override string LinkType => "improvement";
    protected override string[] Statuses => Improvement.Statuses;
    protected override IReadOnlyDictionary<string, string[]> Transitions => Improvement.Transitions;

    protected override string? Apply(Improvement e, ImprovementDto dto, bool creating)
    {
        var error = Allowed.Check("Priorité", dto.Priority, Improvement.Priorities);
        if (error is not null) return error;
        if (dto.Step is < 1 or > 7) return "L'étape du modèle d'amélioration va de 1 à 7.";
        e.Step = dto.Step; e.Priority = dto.Priority!;
        e.Benefit = dto.Benefit ?? ""; e.Baseline = dto.Baseline ?? ""; e.Target = dto.Target ?? "";
        e.Outcome = dto.Outcome;
        e.DueDate = Dates.Utc(dto.DueDate);
        e.AbandonReason = string.IsNullOrWhiteSpace(dto.AbandonReason) ? null : dto.AbandonReason.Trim();
        return null;
    }

    // Engager des moyens ou y renoncer est une décision de gestionnaire.
    protected override bool RequiresManager(string status) => status is "Validée" or "Abandonnée";

    protected override string? CheckStatus(Improvement e, string status)
    {
        if (status is "En cours" or "Réalisée" && e.ValidatedAt is null)
            return "L'amélioration doit d'abord être validée par un gestionnaire.";
        // CSI-14 : une amélioration se mesure — valeur, point de départ et cible avant de l'engager.
        if (status is "Validée" or "En cours" or "Réalisée"
            && (string.IsNullOrWhiteSpace(e.Benefit) || string.IsNullOrWhiteSpace(e.Baseline) || string.IsNullOrWhiteSpace(e.Target)))
            return "Renseigner la valeur attendue, la mesure de départ et la mesure cible : une amélioration se mesure.";
        // CSI-16 : réalisée à l'étape 6 au moins, quand le résultat a été comparé à la cible.
        if (status == "Réalisée" && e.Step < 6)
            return "Une amélioration se déclare réalisée à l'étape 6 (« Y sommes-nous parvenus ? ») au moins.";
        // CSI-13 : un abandon se motive.
        if (status == "Abandonnée" && e.AbandonReason is null)
            return "Motiver l'abandon : le motif reste visible sur la fiche.";
        if (status == "Réalisée" && string.IsNullOrWhiteSpace(e.Outcome))
            return "Décrire le résultat constaté avant de déclarer l'amélioration réalisée.";
        return null;
    }

    protected override void OnStatusChanged(Improvement e, string from)
    {
        if (e.Status == "Validée") { e.ValidatedAt = DateTime.UtcNow; e.ValidatedBy = MyDisplay; }
        // Abandonner retire la validation : relancer l'amélioration demande une
        // nouvelle décision de gestionnaire.
        else if (e.Status is "Proposée" or "Abandonnée") { e.ValidatedAt = null; e.ValidatedBy = null; }
        e.CompletedAt = e.Status == "Réalisée" ? DateTime.UtcNow : null;
    }
}
