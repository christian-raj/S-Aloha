using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Links;
using SAloha.Api.Core.Notifications;
using SAloha.Api.Modules.ContinualImprovement;
using SAloha.Api.Modules.ServiceLevelManagement;

namespace SAloha.Api.Core.Itil;

/// <summary>
/// SLM-20 : respect mesuré d'un SLA, par mois (UTC). Compte les incidents du service créés
/// pendant la validité de l'accord et dotés d'une échéance (INC-21, en heures de service et
/// hors attente, INC-22). Chacun compte une fois : dans le mois de sa résolution (à temps ou
/// non) ; encore ouvert, dans le mois où son échéance est passée (en retard). Les incidents ne portent pas de
/// client : un SLA mesure tout son service. SLM-23 : un mois achevé sous la cible
/// (<see cref="ServiceLevelAgreement.ComplianceTarget"/>) alerte les gestionnaires et propose
/// une amélioration reliée (CSI-20). Vit dans le socle : Niveaux de service, Incidents et
/// Amélioration ne s'appellent pas.
/// </summary>
public static class SlaCompliance
{
    public record Measure(int Total, int OnTime, double? Percent);
    public record Month(string Period, int Total, int OnTime, double? Percent, decimal? Target, bool? Met,
        IReadOnlyList<PriorityMeasure> ByPriority, Availability? Availability = null);
    /// <summary>SLM-21 : disponibilité mesurée du mois, en heures de service.</summary>
    public record Availability(double ServiceHours, double DowntimeHours, double? Percent, decimal? Target, bool? Met, int MajorIncidents);
    public record PriorityMeasure(string Priority, int Total, int OnTime, double? Percent);

    private static double? Percent(int onTime, int total) => total == 0 ? null : Math.Round(onTime * 100.0 / total, 1);

    public static async Task<Month> MonthAsync(AppDbContext db, ServiceLevelAgreement sla, DateTime monthStart, DateTime now)
    {
        var start = new DateTime(monthStart.Year, monthStart.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        var cutoff = end < now ? end : now;
        var from = sla.ValidFrom ?? DateTime.MinValue;
        var rows = await db.Incidents.AsNoTracking()
            .Where(i => i.ServiceId == sla.ServiceId && i.ResolutionDueAt != null && i.CreatedAt >= from
                        && ((i.ResolvedAt != null && i.ResolvedAt >= start && i.ResolvedAt < end)
                            || (i.ResolvedAt == null && i.ResolutionDueAt >= start && i.ResolutionDueAt < cutoff)))
            .Select(i => new { i.Priority, OnTime = i.ResolvedAt != null && i.ResolvedAt <= i.ResolutionDueAt })
            .ToListAsync();
        var availability = await AvailabilityAsync(db, sla, start, cutoff);
        var onTime = rows.Count(r => r.OnTime);
        var percent = Percent(onTime, rows.Count);
        var target = sla.ComplianceTarget;
        return new Month(start.ToString("yyyy-MM"), rows.Count, onTime, percent, target,
            percent is null || target is null ? null : (decimal)percent.Value >= target.Value,
            rows.GroupBy(r => r.Priority).OrderBy(g => g.Key)
                .Select(g => new PriorityMeasure(g.Key, g.Count(), g.Count(r => r.OnTime), Percent(g.Count(r => r.OnTime), g.Count()))).ToList(),
            availability);
    }

    /// <summary>
    /// SLM-21 : 100 % moins la durée cumulée des incidents majeurs du service (création →
    /// résolution, ou jusqu'à maintenant s'ils sont ouverts), comptée dans les heures de service
    /// et rapportée aux heures de service du mois (jusqu'à maintenant pour le mois en cours).
    /// Des incidents majeurs simultanés ne comptent qu'une fois.
    /// </summary>
    public static async Task<Availability?> AvailabilityAsync(AppDbContext db, ServiceLevelAgreement sla, DateTime start, DateTime cutoff)
    {
        if (cutoff <= start) return null;
        var calendar = await Modules.IncidentManagement.IncidentSla.CalendarAsync(db, sla.ServiceId);
        var serviceHours = calendar.Between(start, cutoff).TotalHours;
        var spans = (await db.Incidents.AsNoTracking()
                .Where(i => i.ServiceId == sla.ServiceId && i.IsMajor && i.CreatedAt < cutoff && (i.ResolvedAt == null || i.ResolvedAt > start))
                .Select(i => new { i.CreatedAt, i.ResolvedAt }).ToListAsync())
            .Select(i => (From: i.CreatedAt < start ? start : i.CreatedAt, To: i.ResolvedAt is { } r && r < cutoff ? r : cutoff))
            .OrderBy(s => s.From).ToList();
        var merged = new List<(DateTime From, DateTime To)>();
        foreach (var s in spans)
            if (merged.Count > 0 && s.From <= merged[^1].To) merged[^1] = (merged[^1].From, s.To > merged[^1].To ? s.To : merged[^1].To);
            else merged.Add(s);
        var down = merged.Sum(s => calendar.Between(s.From, s.To).TotalHours);
        double? percent = serviceHours <= 0 ? null : Math.Round(Math.Max(0, 100 - down * 100 / serviceHours), 2);
        var target = sla.AvailabilityTarget;
        return new Availability(Math.Round(serviceHours, 1), Math.Round(down, 2), percent, target,
            percent is null || target is null ? null : (decimal)percent.Value >= target.Value, spans.Count);
    }

    /// <summary>
    /// SLM-23, CSI-20 : au relevé périodique, le mois précédent de chaque SLA En vigueur doté
    /// d'une cible est évalué une fois ; sous la cible, gestionnaires alertés et amélioration
    /// Proposée (source « SLA ») reliée au SLA. Retourne le nombre d'alertes.
    /// </summary>
    public static async Task<int> AlertAsync(AppDbContext db, Notifier notify, DateTime now, Func<string, int, string, bool> fresh)
    {
        var count = 0;
        var previous = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
        foreach (var sla in await db.Agreements.AsNoTracking()
                     .Where(a => a.Status == "En vigueur" && (a.ComplianceTarget != null || a.AvailabilityTarget != null)).ToListAsync())
        {
            if (sla.ValidFrom is { } vf && vf >= previous.AddMonths(1)) continue;   // accord pas encore en vigueur ce mois-là
            var m = await MonthAsync(db, sla, previous, now);
            if (m.Met is false)
                count += await RaiseAsync(db, notify, fresh, sla, $"SLA sous la cible ({m.Period} : {m.Percent:0.#} % pour {sla.ComplianceTarget:0.#} %)",
                    $"{sla.Reference} sous la cible en {m.Period} : {sla.Title}",
                    $"Le SLA {sla.Reference} a tenu {m.OnTime} incident(s) sur {m.Total} dans le délai en {m.Period} "
                    + $"({m.Percent:0.#} %), sous sa cible de {sla.ComplianceTarget:0.#} %. Proposée automatiquement (SLM-23, CSI-20).",
                    $"{m.Percent:0.#} % dans le délai ({m.Period})", $"{sla.ComplianceTarget:0.#} % dans le délai");
            // SLM-21, SLM-23 : disponibilité mesurée sous sa cible.
            if (m.Availability is { Met: false } a)
                count += await RaiseAsync(db, notify, fresh, sla, $"Disponibilité sous la cible ({m.Period} : {a.Percent:0.##} % pour {a.Target:0.##} %)",
                    $"{sla.Reference} : disponibilité sous la cible en {m.Period} : {sla.Title}",
                    $"Le service du SLA {sla.Reference} a été indisponible {a.DowntimeHours:0.##} h de service sur {a.ServiceHours:0.#} en {m.Period} "
                    + $"({a.MajorIncidents} incident(s) majeur(s)) : {a.Percent:0.##} % de disponibilité, sous la cible de {a.Target:0.##} %. Proposée automatiquement (SLM-21, SLM-23, CSI-20).",
                    $"{a.Percent:0.##} % de disponibilité ({m.Period})", $"{a.Target:0.##} % de disponibilité");
        }
        return count;
    }

    /// <summary>Alerte des gestionnaires et amélioration Proposée (source « SLA ») reliée au SLA, une fois par événement.</summary>
    private static async Task<int> RaiseAsync(AppDbContext db, Notifier notify, Func<string, int, string, bool> fresh, ServiceLevelAgreement sla,
        string ev, string title, string description, string baseline, string target)
    {
        if (!fresh("agreement", sla.Id, ev)) return 0;
        notify.ToManagers(sla, "agreement", ev);
        var improvement = await References.CreateAsync(db, "AMI", () => new Improvement
        {
            Title = title.Length > 200 ? title[..200] : title, Description = description,
            Status = "Proposée", Step = 2, Priority = "Moyenne", Source = "SLA", PerimeterId = sla.PerimeterId,
            Baseline = baseline, Target = target, CreatedBy = "système", CreatedByDisplayName = "Système"
        });
        db.ItemLinks.Add(new ItemLink { FromType = "improvement", FromId = improvement.Id, ToType = "agreement", ToId = sla.Id, CreatedBy = "système" });
        await db.SaveChangesAsync();
        notify.ToManagers(improvement, "improvement", "Amélioration proposée");
        return 1;
    }
}
