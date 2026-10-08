using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Core.Search;

/// <summary>
/// Vecteurs des passages, gardés en mémoire ; cosinus calculé par force brute
/// (ADR-0013). Suffisant jusqu'à quelques dizaines de milliers de passages
/// (1024 × 4 octets chacun) ; au-delà, passer à pgvector. Dérivé de la table
/// SearchPassages, rechargé au démarrage et tenu à jour par l'indexeur.
/// </summary>
public class VectorStore
{
    public record Entry(int PassageId, string SourceType, int SourceId, float[] Vector);
    private readonly ConcurrentDictionary<int, Entry> _entries = new();

    public int Count => _entries.Count;

    public async Task LoadAsync(AppDbContext db)
    {
        _entries.Clear();
        var rows = await db.SearchPassages.AsNoTracking().Where(p => p.Embedding != null)
            .Select(p => new { p.Id, p.SourceType, p.SourceId, p.Embedding }).ToListAsync();
        foreach (var r in rows) _entries[r.Id] = new Entry(r.Id, r.SourceType, r.SourceId, r.Embedding!);
    }

    public void RemoveSource(string type, int id)
    {
        foreach (var e in _entries.Values.Where(e => e.SourceType == type && e.SourceId == id))
            _entries.TryRemove(e.PassageId, out _);
    }

    public void Add(SearchPassage p)
    {
        if (p.Embedding is not null) _entries[p.Id] = new Entry(p.Id, p.SourceType, p.SourceId, p.Embedding);
    }

    public void Clear() => _entries.Clear();

    public List<(Entry Entry, float Similarity)> Nearest(float[] query, IReadOnlyCollection<string> types, int k, float minSimilarity) =>
        _entries.Values.Where(e => types.Contains(e.SourceType))
            .Select(e => (Entry: e, Similarity: Vectors.Dot(query, e.Vector)))
            .Where(x => x.Similarity >= minSimilarity)
            .OrderByDescending(x => x.Similarity).Take(k).ToList();
}
