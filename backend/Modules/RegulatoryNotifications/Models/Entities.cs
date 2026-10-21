using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.RegulatoryNotifications;

/// <summary>
/// Déclaration réglementaire d'un incident de sécurité (DRG) : une par régime (NIS 2, RGPD)
/// et par incident. S-Aloha ne déclare pas lui-même : il calcule les échéances légales, les
/// rappelle et garde la trace de chaque envoi fait sur le portail de l'autorité.
/// </summary>
public class RegulatoryNotification : Record
{
    public const string Nis2 = "NIS 2", Gdpr = "RGPD";
    public static readonly string[] Regimes = [Nis2, Gdpr];

    public const string ToQualify = "À qualifier", NotRequired = "Non requise", ToDeclare = "À déclarer",
        EarlyWarningSent = "Alerte précoce envoyée", NotificationSent = "Notification envoyée",
        FinalReportSent = "Rapport final envoyé", Closed = "Close";
    public static readonly string[] Statuses = [ToQualify, ToDeclare, EarlyWarningSent, NotificationSent, FinalReportSent, Closed, NotRequired];
    public static readonly string[] Done = [Closed, NotRequired];

    /// <summary>§ 4 : transitions permises ; celles propres à un régime sont contrôlées à part (DRG-06).</summary>
    public static readonly Dictionary<string, string[]> Transitions = new()
    {
        [ToQualify] = [ToDeclare, NotRequired],
        [ToDeclare] = [EarlyWarningSent, NotificationSent],
        [EarlyWarningSent] = [NotificationSent],
        [NotificationSent] = [FinalReportSent, Closed],
        [FinalReportSent] = [Closed],
        [Closed] = [],
        [NotRequired] = [],
    };

    /// <summary>Autorité par défaut du régime (France).</summary>
    public static string AuthorityOf(string regime) => regime == Nis2 ? "ANSSI" : "CNIL";

    public int IncidentId { get; set; }
    public IncidentManagement.Incident? Incident { get; set; }
    [MaxLength(20)] public string Regime { get; set; } = Nis2;
    [MaxLength(100)] public string Authority { get; set; } = "ANSSI";
    /// <summary>Prise de connaissance de l'incident : point de départ des délais légaux.</summary>
    public DateTime AwareAt { get; set; }
    [MaxLength(300)] public string? AwareAtReason { get; set; }

    public DateTime? EarlyWarningDueAt { get; set; }
    public DateTime? NotificationDueAt { get; set; }
    public DateTime? FinalReportDueAt { get; set; }
    public DateTime? EarlyWarningSentAt { get; set; }
    public DateTime? NotificationSentAt { get; set; }
    public DateTime? FinalReportSentAt { get; set; }
    [MaxLength(100)] public string? AuthorityCaseRef { get; set; }

    public string? Summary { get; set; }
    public string? Impact { get; set; }
    public string? MeasuresTaken { get; set; }
    public bool? SuspectedMalicious { get; set; }
    public bool? CrossBorderImpact { get; set; }
    [MaxLength(300)] public string? PersonalDataCategories { get; set; }
    public int? DataSubjectsCount { get; set; }
    public string? NotRequiredReason { get; set; }
    /// <summary>DRG-12 : motifs du retard d'une notification RGPD envoyée après 72 h.</summary>
    public string? LateReason { get; set; }
    /// <summary>DRG-07 : contenu tel qu'envoyé, une version par envoi (JSON).</summary>
    public string SentVersions { get; set; } = "[]";

    /// <summary>
    /// DRG-10 : échéances en heures calendaires depuis la prise de connaissance. NIS 2 : alerte
    /// précoce + 24 h, notification + 72 h, rapport final un mois après la notification (à
    /// défaut, après son échéance) ; RGPD : notification + 72 h.
    /// </summary>
    public void ComputeDueDates()
    {
        NotificationDueAt = AwareAt.AddHours(72);
        if (Regime == Nis2)
        {
            EarlyWarningDueAt = AwareAt.AddHours(24);
            FinalReportDueAt = (NotificationSentAt ?? NotificationDueAt.Value).AddMonths(1);
        }
        else (EarlyWarningDueAt, FinalReportDueAt) = (null, null);
    }

    /// <summary>Prochaine échéance à tenir (étape, date), ou null : rien en attente d'envoi.</summary>
    public (string Step, DateTime Due)? NextDue()
    {
        if (Done.Contains(Status)) return null;
        if (Regime == Nis2 && EarlyWarningSentAt is null && EarlyWarningDueAt is { } e) return ("Alerte précoce", e);
        if (NotificationSentAt is null && NotificationDueAt is { } n) return ("Notification", n);
        if (Regime == Nis2 && FinalReportSentAt is null && FinalReportDueAt is { } f) return ("Rapport final", f);
        return null;
    }

    public record SentVersion(string Step, DateTime SentAt, string? Summary, string? Impact, string? MeasuresTaken);

    public List<SentVersion> Versions() => JsonSerializer.Deserialize<List<SentVersion>>(SentVersions) ?? [];

    public void Freeze(string step, DateTime sentAt)
    {
        var versions = Versions();
        versions.Add(new SentVersion(step, sentAt, Summary, Impact, MeasuresTaken));
        SentVersions = JsonSerializer.Serialize(versions);
    }
}

public record RegulatoryNotificationDto(string Title, string? Description, string? Status,
    string? OwnerType, string? OwnerId, string? OwnerDisplayName,
    int IncidentId, string? Regime, string? Authority, DateTime? AwareAt = null, string? AwareAtReason = null,
    DateTime? EarlyWarningSentAt = null, DateTime? NotificationSentAt = null, DateTime? FinalReportSentAt = null,
    string? AuthorityCaseRef = null, string? Summary = null, string? Impact = null, string? MeasuresTaken = null,
    bool? SuspectedMalicious = null, bool? CrossBorderImpact = null, string? PersonalDataCategories = null,
    int? DataSubjectsCount = null, string? NotRequiredReason = null, string? LateReason = null,
    int? PerimeterId = null) : IRecordDto;
