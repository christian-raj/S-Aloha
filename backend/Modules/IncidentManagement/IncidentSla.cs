using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Modules.IncidentManagement;

/// <summary>
/// INC-21 : échéance de résolution d'un incident (et de prise en charge, SLM-13) = déclaration + délai de
/// résolution de sa priorité dans le SLA En vigueur de son service.
/// Le service est celui du catalogue (INC-20, `ServiceId`) ; un libellé inconnu du
/// catalogue n'a pas de SLA. Délais en heures
/// calendaires : heures de service et suspension En attente viendront avec
/// INC-22. Sans SLA applicable : pas d'échéance.
/// </summary>
public static class IncidentSla
{
    public static Task<DateTime?> DueAsync(AppDbContext db, Incident e) => DeadlineAsync(db, e, response: false);

    /// <summary>SLM-13 : échéance de prise en charge, selon les délais de prise en charge du même SLA.</summary>
    public static Task<DateTime?> ResponseDueAsync(AppDbContext db, Incident e) => DeadlineAsync(db, e, response: true);

    private static async Task<DateTime?> DeadlineAsync(AppDbContext db, Incident e, bool response)
    {
        if (e.ServiceId is not int serviceId) return null;
        var at = e.CreatedAt;
        var agreements = db.Agreements.AsNoTracking()
            .Where(a => a.Status == "En vigueur" && a.ServiceId == serviceId
                        && (a.ValidFrom == null || a.ValidFrom <= at) && (a.ValidTo == null || a.ValidTo >= at));
        var hours = response
            ? await agreements.Select(a => e.Priority == "P1" ? a.ResponseHoursP1 : e.Priority == "P2" ? a.ResponseHoursP2
                       : e.Priority == "P3" ? a.ResponseHoursP3 : a.ResponseHoursP4)
                .Where(h => h != null).MinAsync(h => (int?)h)
            : await agreements.Select(a => e.Priority == "P1" ? a.ResolutionHoursP1 : e.Priority == "P2" ? a.ResolutionHoursP2
                       : e.Priority == "P3" ? a.ResolutionHoursP3 : a.ResolutionHoursP4)
                .Where(h => h != null).MinAsync(h => (int?)h);
        return hours is null ? null : at.AddHours(hours.Value);
    }

    /// <summary>Au démarrage : échéance des incidents antérieurs à INC-21 (ou dont le SLA est apparu depuis).</summary>
    public static async Task BackfillAsync(AppDbContext db)
    {
        var pending = await db.Incidents.Where(i => i.ResolutionDueAt == null && i.ServiceId != null).ToListAsync();
        foreach (var i in pending) { i.ResolutionDueAt = await DueAsync(db, i); i.ResponseDueAt = await ResponseDueAsync(db, i); }
        // SLM-13 : incidents ouverts pas encore pris en charge, dont le SLA a reçu des délais de prise en charge.
        var waiting = await db.Incidents.Where(i => i.ResponseDueAt == null && i.FirstResponseAt == null
                                                    && i.ServiceId != null && !Incident.Done.Contains(i.Status)).ToListAsync();
        foreach (var i in waiting.Except(pending)) i.ResponseDueAt = await ResponseDueAsync(db, i);
        if (pending.Count > 0 || waiting.Count > 0) await db.SaveChangesAsync();
    }
}
