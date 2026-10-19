using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Core.Administration;

/// <summary>
/// ADM-12 : date de la dernière connexion d'un compte — rien d'autre (donnée personnelle
/// minimale). Clé : le compte de l'annuaire, en minuscules.
/// </summary>
public class LastLogin
{
    [Key, MaxLength(200)] public string Username { get; set; } = "";
    public DateTime At { get; set; }

    /// <summary>Connexion réussie : la date du compte est mise à jour (ou créée).</summary>
    public static async Task RecordAsync(AppDbContext db, string username)
    {
        var key = username.ToLowerInvariant();
        var row = await db.LastLogins.FindAsync(key);
        if (row is null) db.LastLogins.Add(new LastLogin { Username = key, At = DateTime.UtcNow });
        else row.At = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}

/// <summary>ADM-12 : purge quotidienne des comptes sans connexion depuis <c>Admin:LastLoginRetentionMonths</c> (13 mois).</summary>
public class LastLoginRetention(IServiceScopeFactory scopes, IConfiguration config, ILogger<LastLoginRetention> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var months = config.GetValue("Admin:LastLoginRetentionMonths", 13);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var limit = DateTime.UtcNow.AddMonths(-months);
                var purged = await db.LastLogins.Where(l => l.At < limit).ExecuteDeleteAsync(ct);
                if (purged > 0) logger.LogInformation("Dernières connexions : {Count} compte(s) inactif(s) depuis {Months} mois effacé(s).", purged, months);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Dernières connexions : purge en échec.");
            }
            await Task.Delay(TimeSpan.FromDays(1), ct);
        }
    }
}
