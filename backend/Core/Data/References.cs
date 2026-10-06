using Microsoft.EntityFrameworkCore;

namespace SAloha.Api.Core.Data;

/// <summary>Enregistrement porteur d'une référence unique XXX-AAAA-NNNN.</summary>
public interface IHasReference
{
    string Reference { get; set; }
}

/// <summary>
/// Références annuelles XXX-AAAA-NNNN, communes à tous les processus.
/// </summary>
public static class References
{
    private const int MaxAttempts = 10;

    /// <summary>
    /// Ajoute <paramref name="build"/>() avec la prochaine référence et enregistre.
    /// La référence part du plus grand numéro de l'année, pas du nombre
    /// d'enregistrements : compter redonnait un numéro existant dès qu'un
    /// enregistrement avait été supprimé, et l'index unique bloquait alors
    /// toutes les créations suivantes (B2, revue du 2026-10-06). Deux créations
    /// simultanées lisent le même maximum : l'index unique tranche, la perdante
    /// relit et réessaie (B3).
    /// </summary>
    public static async Task<T> CreateAsync<T>(AppDbContext db, string code, Func<T> build)
        where T : class, IHasReference
    {
        var set = db.Set<T>();
        for (var attempt = 1; ; attempt++)
        {
            var entity = build();
            entity.Reference = await NextAsync(set, code);
            set.Add(entity);
            try
            {
                await db.SaveChangesAsync();
                return entity;
            }
            catch (DbUpdateException) when (attempt < MaxAttempts)
            {
                db.Entry(entity).State = EntityState.Detached;
                // Toute autre erreur d'enregistrement que la collision de
                // référence remonte telle quelle.
                if (!await set.AnyAsync(x => x.Reference == entity.Reference)) throw;
                await Task.Delay(Random.Shared.Next(5, 25 * attempt));
            }
        }
    }

    private static async Task<string> NextAsync<T>(DbSet<T> set, string code) where T : class, IHasReference
    {
        var prefix = $"{code}-{DateTime.UtcNow.Year}-";
        // Numéro sur 4 chiffres complétés de zéros : l'ordre alphabétique est
        // l'ordre numérique jusqu'à 9999 enregistrements dans l'année.
        var last = await set.Where(x => x.Reference.StartsWith(prefix))
            .OrderByDescending(x => x.Reference).Select(x => x.Reference).FirstOrDefaultAsync();
        var n = last is null ? 0 : int.Parse(last[prefix.Length..]);
        return $"{prefix}{n + 1:D4}";
    }
}
