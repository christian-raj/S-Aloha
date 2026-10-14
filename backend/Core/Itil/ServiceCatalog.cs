using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Core.Itil;

/// <summary>Enregistrement rattaché à un service du catalogue (incident, problème, demande).</summary>
public interface IServiceBound
{
    int Id { get; }
    /// <summary>SLM-10 : service du catalogue ; null pour un libellé inconnu du catalogue.</summary>
    int? ServiceId { get; set; }
    /// <summary>Nom du service, recopié du catalogue (affichage, reporting, recherche).</summary>
    string AffectedService { get; set; }
}

/// <summary>
/// SLM-10, INC-20 : le service d'un incident, d'un problème ou d'une demande est choisi dans
/// le catalogue (`ServiceId`) ; son nom est recopié dans `AffectedService`. Un nom donné sans
/// identifiant est retrouvé dans le catalogue sans tenir compte de la casse (clients de l'API,
/// données antérieures) ; inconnu, il reste un simple libellé, sans SLA. SLM-15 : un service
/// Retiré ne peut plus être choisi, mais reste sur les enregistrements qui le portaient.
/// </summary>
public static class ServiceCatalog
{
    public static async Task<string?> ResolveAsync(AppDbContext db, IServiceBound e)
    {
        var entry = db.Entry(e);
        var tracked = e.Id != 0 && entry.State != EntityState.Detached;
        if (e.ServiceId is int id)
        {
            // Un nouveau choix : création, ou autre service qu'avant.
            var choosing = !tracked || !Equals(entry.Property(nameof(IServiceBound.ServiceId)).OriginalValue, e.ServiceId);
            var s = await db.Services.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new { x.Title, x.Status }).FirstOrDefaultAsync();
            if (s is null) return "Choisir un service du catalogue.";
            if (choosing && s.Status == "Retiré") return $"Le service « {s.Title} » est retiré : en choisir un autre.";
            e.AffectedService = s.Title;
            return null;
        }
        var name = (e.AffectedService ?? "").Trim();
        e.AffectedService = name;
        if (name == "") return null;
        var key = name.ToLower();
        var match = await db.Services.AsNoTracking().Where(x => x.Title.ToLower() == key)
            .OrderBy(x => x.Status == "Retiré").ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.Title, x.Status }).FirstOrDefaultAsync();
        if (match is null) return null;
        // Par son nom : un nouveau choix si le nom change (casse ignorée).
        var renamed = !tracked || !string.Equals(entry.Property(nameof(IServiceBound.AffectedService)).OriginalValue as string,
                                                 name, StringComparison.OrdinalIgnoreCase);
        if (renamed && match.Status == "Retiré") return $"Le service « {match.Title} » est retiré : en choisir un autre.";
        e.ServiceId = match.Id; e.AffectedService = match.Title;
        return null;
    }
}
