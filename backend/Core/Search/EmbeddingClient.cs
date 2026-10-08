using System.Net.Http.Json;

namespace SAloha.Api.Core.Search;

/// <summary>Calcul des vecteurs (embeddings) d'une liste de textes.</summary>
public interface IEmbeddingClient
{
    /// <summary>Faux si aucun service n'est configuré : la recherche reste lexicale.</summary>
    bool Enabled { get; }
    string Model { get; }
    /// <summary>Vecteurs normalisés, ou null si le service ne répond pas (jamais d'exception).</summary>
    Task<float[][]?> EmbedAsync(IReadOnlyList<string> texts, TimeSpan timeout, CancellationToken ct = default);
}

/// <summary>
/// Service d'embeddings séparé de l'API (ADR-0013) : Ollama avec bge-m3,
/// appelé en HTTP (POST /api/embed). Le modèle et sa mémoire (~1,2 Go) vivent
/// hors du processus de l'API. Configuration : <c>Embeddings:Url</c> (vide :
/// désactivé) et <c>Embeddings:Model</c> (bge-m3 par défaut).
/// </summary>
public class OllamaEmbeddingClient(HttpClient http, IConfiguration config, ILogger<OllamaEmbeddingClient> logger) : IEmbeddingClient
{
    private string? Url => config["Embeddings:Url"]?.TrimEnd('/');
    public bool Enabled => !string.IsNullOrWhiteSpace(Url);
    public string Model => config["Embeddings:Model"] is { Length: > 0 } m ? m : "bge-m3";

    private record EmbedResponse(float[][] Embeddings);

    public async Task<float[][]?> EmbedAsync(IReadOnlyList<string> texts, TimeSpan timeout, CancellationToken ct = default)
    {
        if (!Enabled || texts.Count == 0) return null;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            var res = await http.PostAsJsonAsync($"{Url}/api/embed", new { model = Model, input = texts }, cts.Token);
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadFromJsonAsync<EmbedResponse>(cts.Token);
            if (body?.Embeddings is not { } vectors || vectors.Length != texts.Count) return null;
            return vectors.Select(Vectors.Normalize).ToArray();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            // Échec silencieux (RAG-02) : la recherche lexicale suffit à répondre.
            logger.LogWarning("Service d'embeddings indisponible ({Url}) : {Message}", Url, ex.Message);
            return null;
        }
    }
}

public static class Vectors
{
    public static float[] Normalize(float[] v)
    {
        var norm = MathF.Sqrt(v.Sum(x => x * x));
        return norm == 0 ? v : v.Select(x => x / norm).ToArray();
    }

    /// <summary>Cosinus de deux vecteurs normalisés : leur produit scalaire.</summary>
    public static float Dot(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        var s = 0f;
        for (var i = 0; i < a.Length; i++) s += a[i] * b[i];
        return s;
    }
}
