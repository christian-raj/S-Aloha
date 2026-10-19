using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

/// <summary>File des tâches de scan à exécuter : une seule à la fois, dans l'ordre d'arrivée (CFG-46).</summary>
public class DiscoveryQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();
    public void Enqueue(int jobId) => _channel.Writer.TryWrite(jobId);
    public ChannelReader<int> Reader => _channel.Reader;
}

/// <summary>Fenêtre et fréquence d'une tâche (CFG-46), en heure locale de l'organisation.</summary>
public static class ScanWindow
{
    /// <summary>L'instant <paramref name="local"/> est-il dans la fenêtre ? Une fenêtre peut passer minuit (20:00–06:00).</summary>
    public static bool Contains(TimeOnly? start, TimeOnly? end, TimeOnly local)
    {
        if (start is not { } s || end is not { } e || s == e) return true;
        return s < e ? local >= s && local < e : local >= s || local < e;
    }

    /// <summary>Tâche planifiée à lancer : active, pas déjà en file ou en cours, dans sa fenêtre, échue.</summary>
    public static bool IsDue(DiscoveryJob job, DateTime utcNow, TimeZoneInfo zone) =>
        job.IsActive && job.FrequencyHours > 0
        && job.LastStatus is not (DiscoveryJob.Queued or DiscoveryJob.Running)
        && (job.LastRunAt is null || utcNow - job.LastRunAt.Value >= TimeSpan.FromHours(job.FrequencyHours))
        && Contains(job.WindowStart, job.WindowEnd, TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, zone)));

    public static TimeZoneInfo Zone(IConfiguration config)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(config["Discovery:TimeZone"] ?? "Europe/Paris"); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }
}

/// <summary>
/// Exécution d'une tâche : déchiffre son identifiant, lance le collecteur de son type dans une
/// durée bornée, fait entrer les observations dans le sas (CFG-40) et note le résultat. Le
/// message d'échec ne reprend jamais le secret.
/// </summary>
public class DiscoveryRunner(AppDbContext db, IEnumerable<IDiscoveryCollector> collectors, CredentialVault vault,
    MatchingEngine matching, IConfiguration config, ILogger<DiscoveryRunner> logger)
{
    public async Task RunAsync(int jobId, CancellationToken ct)
    {
        var job = await db.Set<DiscoveryJob>().Include(j => j.Credential).FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null) return;
        job.LastStatus = DiscoveryJob.Running;
        job.LastError = null;
        await db.SaveChangesAsync(ct);
        try
        {
            var collector = collectors.FirstOrDefault(c => c.ScanType == job.ScanType)
                            ?? throw new InvalidOperationException($"Aucun collecteur pour le type « {job.ScanType} ».");
            var secret = job.Credential is { } cred ? vault.Unprotect(cred.EncryptedSecret) : null;
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(TimeSpan.FromMinutes(config.GetValue("Discovery:JobTimeoutMinutes", 30)));
            var context = new CollectContext(job, job.Credential, secret, config.GetValue("Discovery:MaxConcurrency", 10));
            var observations = await collector.CollectAsync(context, limit.Token);
            var result = await matching.IngestAsync(job, observations, ct);
            job.LastStatus = DiscoveryJob.Succeeded;
            job.LastFoundCount = result.Seen;
            logger.LogInformation("Découverte « {Name} » : {Seen} équipement(s), {Added} nouveau(x), {Auto} rapproché(s), {Conflicts} conflit(s).",
                job.Name, result.Seen, result.Added, result.AutoReconciled, result.Conflicts);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            db.ChangeTracker.Clear();
            job = await db.Set<DiscoveryJob>().FirstAsync(j => j.Id == jobId, CancellationToken.None);
            job.LastStatus = DiscoveryJob.Failed;
            var message = ex is OperationCanceledException ? "Durée maximale du scan dépassée." : ex.Message;
            job.LastError = message.Length > 500 ? message[..500] : message;
            logger.LogWarning(ex, "Découverte « {Name} » en échec", job.Name);
        }
        job.LastRunAt = DateTime.UtcNow;
        await db.SaveChangesAsync(CancellationToken.None);
    }
}

/// <summary>
/// Service d'arrière-plan de la découverte : exécute la file, et toutes les
/// <c>Discovery:IntervalMinutes</c> (5 par défaut ; 0 : pas de lancement planifié) y place les
/// tâches échues. Au démarrage, une tâche interrompue par un arrêt est notée en échec et une
/// tâche en file y est remise.
/// </summary>
public class DiscoveryWorker(IServiceScopeFactory scopes, DiscoveryQueue queue, IConfiguration config, ILogger<DiscoveryWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await RecoverAsync(ct);
        var minutes = config.GetValue("Discovery:IntervalMinutes", 5);
        await Task.WhenAll(ConsumeAsync(ct), minutes > 0 ? ScheduleAsync(TimeSpan.FromMinutes(minutes), ct) : Task.CompletedTask);
    }

    private async Task RecoverAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var job in await db.Set<DiscoveryJob>()
                         .Where(j => j.LastStatus == DiscoveryJob.Running || j.LastStatus == DiscoveryJob.Queued).ToListAsync(ct))
            {
                if (job.LastStatus == DiscoveryJob.Queued) { queue.Enqueue(job.Id); continue; }
                job.LastStatus = DiscoveryJob.Failed;
                job.LastError = "Scan interrompu par un arrêt de l'API.";
            }
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Découverte : reprise des tâches interrompues en échec.");
        }
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        await foreach (var jobId in queue.Reader.ReadAllAsync(ct))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<DiscoveryRunner>().RunAsync(jobId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Découverte : exécution de la tâche {Id} en échec.", jobId);
            }
        }
    }

    private async Task ScheduleAsync(TimeSpan every, CancellationToken ct)
    {
        var zone = ScanWindow.Zone(config);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTime.UtcNow;
                foreach (var job in (await db.Set<DiscoveryJob>().Where(j => j.IsActive && j.FrequencyHours > 0).ToListAsync(ct))
                             .Where(j => ScanWindow.IsDue(j, now, zone)))
                {
                    job.LastStatus = DiscoveryJob.Queued;
                    await db.SaveChangesAsync(ct);
                    queue.Enqueue(job.Id);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Découverte : planification en échec.");
            }
            await Task.Delay(every, ct);
        }
    }
}
