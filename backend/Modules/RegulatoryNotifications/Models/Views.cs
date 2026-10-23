namespace SAloha.Api.Modules.RegulatoryNotifications;

// Réponses de l'API (A2, ADR-0016) : DTO explicites.

/// <summary>Une étape de déclaration : échéance, envoi, et retard (DRG-11 : signalé même après l'envoi).</summary>
public record DeclarationStep(string Step, DateTime? DueAt, DateTime? SentAt, bool Late);

public record RegulatoryNotificationView(
    int IncidentId,
    string? IncidentReference,
    string? IncidentTitle,
    string Regime,
    string Authority,
    DateTime AwareAt,
    string? AwareAtReason,
    DateTime? EarlyWarningDueAt,
    DateTime? NotificationDueAt,
    DateTime? FinalReportDueAt,
    DateTime? EarlyWarningSentAt,
    DateTime? NotificationSentAt,
    DateTime? FinalReportSentAt,
    string? AuthorityCaseRef,
    string? Summary,
    string? Impact,
    string? MeasuresTaken,
    bool? SuspectedMalicious,
    bool? CrossBorderImpact,
    string? PersonalDataCategories,
    int? DataSubjectsCount,
    string? NotRequiredReason,
    string? LateReason,
    string? Severity,
    string? CompromiseIndicators,
    string? BreachNature,
    string? LikelyConsequences,
    List<string> MissingRubrics,
    List<DeclarationStep> Steps,
    string? NextStep,
    DateTime? NextDueAt,
    List<RegulatoryNotification.SentVersion> SentVersions,
    int Id,
    int PerimeterId,
    string? Reference,
    string? Title,
    string? Description,
    string? Status,
    string? OwnerType,
    string? OwnerId,
    string? OwnerDisplayName,
    string? CreatedBy,
    string? CreatedByDisplayName,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static RegulatoryNotificationView From(RegulatoryNotification e)
    {
        var now = DateTime.UtcNow;
        static DeclarationStep Step(string name, DateTime? due, DateTime? sent, DateTime now) =>
            new(name, due, sent, due is { } d && (sent ?? now) > d);
        var steps = new List<DeclarationStep>();
        if (e.Regime == RegulatoryNotification.Nis2) steps.Add(Step("Alerte précoce", e.EarlyWarningDueAt, e.EarlyWarningSentAt, now));
        steps.Add(Step("Notification", e.NotificationDueAt, e.NotificationSentAt, now));
        if (e.Regime == RegulatoryNotification.Nis2) steps.Add(Step("Rapport final", e.FinalReportDueAt, e.FinalReportSentAt, now));
        var next = e.NextDue();
        return new(e.IncidentId, e.Incident?.Reference, e.Incident?.Title, e.Regime, e.Authority, e.AwareAt, e.AwareAtReason,
            e.EarlyWarningDueAt, e.NotificationDueAt, e.FinalReportDueAt, e.EarlyWarningSentAt, e.NotificationSentAt, e.FinalReportSentAt,
            e.AuthorityCaseRef, e.Summary, e.Impact, e.MeasuresTaken, e.SuspectedMalicious, e.CrossBorderImpact,
            e.PersonalDataCategories, e.DataSubjectsCount, e.NotRequiredReason, e.LateReason,
            e.Severity, e.CompromiseIndicators, e.BreachNature, e.LikelyConsequences, e.Rubrics().Where(r => !r.Filled).Select(r => r.Label).ToList(), steps, next?.Step, next?.Due,
            e.Versions(), e.Id, e.PerimeterId, e.Reference, e.Title, e.Description, e.Status, e.OwnerType, e.OwnerId,
            e.OwnerDisplayName, e.CreatedBy, e.CreatedByDisplayName, e.CreatedAt, e.UpdatedAt);
    }
}
