using System.Collections.Concurrent;
using SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

namespace SAloha.Api.Tests;

/// <summary>
/// Collecteur de test : renvoie les observations préparées pour la cible de la tâche, sans
/// toucher au réseau ni à l'annuaire, et retient le secret reçu (déchiffré par le coffre).
/// </summary>
public class FakeCollector(string scanType) : IDiscoveryCollector
{
    public static readonly ConcurrentDictionary<string, List<Observation>> Observations = new();
    public static readonly ConcurrentDictionary<string, string?> SecretsReceived = new();

    public string ScanType => scanType;

    public Task<List<Observation>> CollectAsync(CollectContext context, CancellationToken ct)
    {
        SecretsReceived[context.Job.TargetRange] = context.Secret;
        return Task.FromResult(Observations.GetValueOrDefault(context.Job.TargetRange) ?? []);
    }
}
