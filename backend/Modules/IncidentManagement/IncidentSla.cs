using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Modules.IncidentManagement;

/// <summary>
/// INC-21 : échéance de résolution d'un incident = déclaration + délai de
/// résolution de sa priorité dans le SLA En vigueur de son service.
/// En attendant le service choisi dans le catalogue (INC-20), le service est
/// retrouvé par son nom, sans tenir compte de la casse. Délais en heures
/// calendaires : heures de service et suspension En attente viendront avec
/// INC-22. Sans SLA applicable : pas d'échéance.
/// </summary>
public static class IncidentSla
{
    public static async Task<DateTime?> DueAsync(AppDbContext db, Incident e)
    {
        if (string.IsNullOrWhiteSpace(e.AffectedService)) return null;
        var name = e.AffectedService.Trim().ToLower();
        var at = e.CreatedAt;
        var hours = await db.Agreements.AsNoTracking()
            .Where(a => a.Status == "En vigueur" && a.Service!.Title.ToLower() == name
                        && (a.ValidFrom == null || a.ValidFrom <= at) && (a.ValidTo == null || a.ValidTo >= at))
            .Select(a => e.Priority == "P1" ? a.ResolutionHoursP1 : e.Priority == "P2" ? a.ResolutionHoursP2
                       : e.Priority == "P3" ? a.ResolutionHoursP3 : a.ResolutionHoursP4)
            .Where(h => h != null).MinAsync(h => (int?)h);
        return hours is null ? null : at.AddHours(hours.Value);
    }

    /// <summary>Au démarrage : échéance des incidents antérieurs à INC-21 (ou dont le SLA est apparu depuis).</summary>
    public static async Task BackfillAsync(AppDbContext db)
    {
        var pending = await db.Incidents.Where(i => i.ResolutionDueAt == null && i.AffectedService != "").ToListAsync();
        foreach (var i in pending) i.ResolutionDueAt = await DueAsync(db, i);
        if (pending.Count > 0) await db.SaveChangesAsync();
    }
}
