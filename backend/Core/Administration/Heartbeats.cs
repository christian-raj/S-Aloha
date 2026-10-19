using System.Collections.Concurrent;

namespace SAloha.Api.Core.Administration;

/// <summary>
/// ADM-20 : dernier passage de chaque tâche de fond (relevé des notifications, planificateur de
/// la découverte, purge du journal d'audit), pour la page Santé. En mémoire : un redémarrage
/// repart de zéro, ce que la page indique (« pas encore passée depuis le démarrage »).
/// </summary>
public class Heartbeats
{
    public record Beat(DateTime At, bool Ok, string? Message);

    private readonly ConcurrentDictionary<string, Beat> _beats = new();

    public void Record(string task, bool ok, string? message = null) => _beats[task] = new Beat(DateTime.UtcNow, ok, message);

    public Beat? Last(string task) => _beats.GetValueOrDefault(task);
}
