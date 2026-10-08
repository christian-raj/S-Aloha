using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Core.Search;

/// <summary>
/// Recherche hybride dans les contenus validés (RAG-xx,
/// docs/reference/processus/recherche.md ; ADR-0013).
/// </summary>
[ApiController]
[Route("api/search")]
[Authorize(Policy = "User")]
public class SearchController(AppDbContext db, HybridSearch search, IEmbeddingClient embeddings, VectorStore store, IndexQueue queue) : ControllerBase
{
    private static IReadOnlyCollection<string> TypesOf(string? types) =>
        string.IsNullOrWhiteSpace(types)
            ? SearchSources.Types
            : types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(SearchSources.Types.Contains).ToList();

    /// <summary>Recherche libre (RAG-05) : articles publiés, erreurs connues et problèmes résolus, incidents résolus.</summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] string? types, [FromQuery] int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(Array.Empty<object>());
        return Ok(await search.SearchAsync(q.Trim(), TypesOf(types), null, Math.Clamp(limit, 1, 50)));
    }

    /// <summary>
    /// Cas similaires à un enregistrement (RAG-06) : son titre et sa
    /// description servent de requête ; il est exclu des résultats.
    /// </summary>
    [HttpGet("similar")]
    public async Task<IActionResult> Similar([FromQuery] string type, [FromQuery] int id, [FromQuery] string? types, [FromQuery] int limit = 5)
    {
        var doc = await SearchSources.LoadAsync(db, type, id);
        if (doc is null) return NotFound();
        var query = doc.Title + "\n" + (doc.Text.Length > 2000 ? doc.Text[..2000] : doc.Text);
        return Ok(await search.SearchAsync(query, TypesOf(types), (type, id), Math.Clamp(limit, 1, 20), lexicalQuery: doc.Title));
    }

    /// <summary>État de l'index et du service d'embeddings (RAG-08, Admin).</summary>
    [HttpGet("status")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Status()
    {
        var byType = await db.SearchPassages.AsNoTracking().GroupBy(p => p.SourceType)
            .Select(g => new { Type = g.Key, Records = g.Select(p => p.SourceId).Distinct().Count(), Passages = g.Count(),
                WithVector = g.Count(p => p.Embedding != null) }).ToListAsync();
        var reachable = embeddings.Enabled && await embeddings.EmbedAsync(["test"], TimeSpan.FromSeconds(5)) is not null;
        return Ok(new
        {
            Embeddings = new { embeddings.Enabled, embeddings.Model, Reachable = reachable },
            VectorsInMemory = store.Count,
            Sources = byType,
            LastIndexedAt = await db.SearchPassages.MaxAsync(p => (DateTime?)p.IndexedAt)
        });
    }

    /// <summary>Reconstruction complète, en tâche de fond (RAG-08, Admin).</summary>
    [HttpPost("reindex")]
    [Authorize(Policy = "Admin")]
    public IActionResult Reindex()
    {
        queue.EnqueueAll();
        return Accepted(new { message = "Reconstruction de l'index lancée en tâche de fond." });
    }
}
