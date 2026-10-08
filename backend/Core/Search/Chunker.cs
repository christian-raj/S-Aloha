using System.Text;
using System.Text.RegularExpressions;

namespace SAloha.Api.Core.Search;

/// <summary>
/// Découpe un texte en passages d'au plus <c>max</c> caractères : par
/// paragraphes regroupés, et un paragraphe trop long est coupé « dur » avec un
/// recouvrement de <c>overlap</c> caractères, pour qu'une phrase à cheval sur
/// deux passages reste trouvable (RAG-04).
/// </summary>
public static partial class Chunker
{
    public static List<string> Split(string? text, int max = 800, int overlap = 100)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;
        var current = new StringBuilder();
        foreach (var raw in Paragraphs().Split(text.Replace("\r\n", "\n")))
        {
            var p = raw.Trim();
            if (p.Length == 0) continue;
            if (p.Length > max)
            {
                Flush(chunks, current);
                for (var start = 0; start < p.Length; start += max - overlap)
                {
                    chunks.Add(p.Substring(start, Math.Min(max, p.Length - start)).Trim());
                    if (start + max >= p.Length) break;
                }
                continue;
            }
            if (current.Length > 0 && current.Length + 2 + p.Length > max) Flush(chunks, current);
            if (current.Length > 0) current.Append("\n\n");
            current.Append(p);
        }
        Flush(chunks, current);
        return chunks;
    }

    private static void Flush(List<string> chunks, StringBuilder current)
    {
        if (current.Length > 0) chunks.Add(current.ToString());
        current.Clear();
    }

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex Paragraphs();
}
