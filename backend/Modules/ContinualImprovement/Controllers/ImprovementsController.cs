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
        return null;
    }

    // Engager des moyens ou y renoncer est une décision de gestionnaire.
    protected override bool RequiresManager(string status) => status is "Validée" or "Abandonnée";

    protected override string? CheckStatus(Improvement e, string status)
    {
        if (status is "En cours" or "Réalisée" && e.ValidatedAt is null)
            return "L'amélioration doit d'abord être validée par un gestionnaire.";
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
