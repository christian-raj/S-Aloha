using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Modules.ComplianceAssessment;

/// <summary>
/// Structure du ReCyF embarquée (objectifs.csv, exigences.csv) et import du
/// texte des exigences. Le dépôt ne contient pas ce texte : sa réutilisation
/// commerciale est soumise à l'autorisation de l'ANSSI (ADR-0012).
/// </summary>
public static class ReferentialStore
{
    /// <summary>Version du ReCyF dont la structure est embarquée.</summary>
    public const string Version = "ReCyF v2.5 du 17/03/2026 (version de travail)";

    /// <summary>
    /// Aligne la base sur la structure embarquée, au démarrage : ajoute ce qui
    /// manque, met à jour thématique, cibles et mesures ISO. Ne touche jamais au
    /// texte importé ; une exigence retirée de la structure reste en base
    /// (des réponses peuvent la viser).
    /// </summary>
    public static void EnsureStructure(AppDbContext db)
    {
        var objectives = db.SecurityObjectives.ToDictionary(o => o.Id);
        foreach (var row in Rows("objectifs.csv"))
        {
            var id = int.Parse(row["Numero"]);
            if (!objectives.TryGetValue(id, out var o))
                db.SecurityObjectives.Add(o = objectives[id] = new SecurityObjective { Id = id });
            o.Title = row["Titre"]; o.Pillar = row["Pilier"];
        }
        db.SaveChanges();

        var requirements = db.SecurityRequirements.ToDictionary(r => r.Code);
        var order = 0;
        foreach (var row in Rows("exigences.csv"))
        {
            var code = row["Code"];
            if (!requirements.TryGetValue(code, out var r))
                db.SecurityRequirements.Add(r = requirements[code] = new SecurityRequirement { Code = code });
            var targets = row["Cibles"];
            r.ObjectiveId = int.Parse(row["Objectif"]);
            r.Theme = row["Thematique"];
            r.ForImportant = targets.Split('/').Contains("EI");
            r.ForEssential = targets.Split('/').Contains("EE");
            r.IsoControls = row["Iso27002"];
            r.Order = ++order;
        }
        db.SaveChanges();
    }

    public record ImportReport(int Updated, int Unchanged, List<string> Unknown, int WithoutText);

    /// <summary>
    /// Importe le texte des exigences depuis un CSV (séparateur ; ou ,) dont
    /// l'en-tête porte une colonne d'identifiant (« Référence » ou « Code ») et
    /// une colonne de texte (« Contenu » ou « Texte »). Les autres colonnes sont
    /// ignorées. Une exigence absente du fichier garde son texte.
    /// </summary>
    public static async Task<ImportReport> ImportTextAsync(AppDbContext db, string csv)
    {
        var rows = Csv.Parse(csv);
        if (rows.Count < 2) throw new FormatException("Le fichier est vide ou ne contient que l'en-tête.");
        var header = rows[0].Select(h => h.Trim()).ToList();
        var codeCol = IndexOf(header, "Référence", "Reference", "Code");
        var textCol = IndexOf(header, "Contenu", "Texte", "Text");
        if (codeCol < 0 || textCol < 0)
            throw new FormatException("En-tête attendu : une colonne « Référence » (ou « Code ») et une colonne « Contenu » (ou « Texte »).");

        var requirements = await db.SecurityRequirements.ToDictionaryAsync(r => r.Code);
        int updated = 0, unchanged = 0;
        var unknown = new List<string>();
        foreach (var row in rows.Skip(1))
        {
            if (row.Count <= Math.Max(codeCol, textCol)) continue;
            var code = row[codeCol].Trim();
            var text = row[textCol].Trim();
            if (code.Length == 0 || text.Length == 0) continue;
            if (!requirements.TryGetValue(code, out var r)) { unknown.Add(code); continue; }
            if (r.Text == text) { unchanged++; continue; }
            r.Text = text; r.TextImportedAt = DateTime.UtcNow; updated++;
        }
        await db.SaveChangesAsync();
        return new ImportReport(updated, unchanged, unknown, requirements.Values.Count(r => string.IsNullOrEmpty(r.Text)));
    }

    private static int IndexOf(List<string> header, params string[] names) =>
        header.FindIndex(h => names.Any(n => string.Equals(h, n, StringComparison.OrdinalIgnoreCase)));

    private static IEnumerable<Dictionary<string, string>> Rows(string file)
    {
        var name = typeof(ReferentialStore).Assembly.GetManifestResourceNames().Single(n => n.EndsWith("." + file));
        using var stream = typeof(ReferentialStore).Assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var rows = Csv.Parse(reader.ReadToEnd());
        var header = rows[0];
        return rows.Skip(1).Where(r => r.Count == header.Count)
            .Select(r => header.Select((h, i) => (h, v: r[i])).ToDictionary(x => x.h, x => x.v));
    }
}

/// <summary>
/// Lecture CSV (RFC 4180) : guillemets, guillemets doublés, retours à la ligne
/// dans un champ — le texte des exigences en contient. Séparateur deviné sur
/// la première ligne (; ou ,).
/// </summary>
public static class Csv
{
    public static List<List<string>> Parse(string text)
    {
        // Le BOM d'un export Excel précède le guillemet du premier champ.
        text = text.TrimStart('\uFEFF');
        var firstLine = text.Split('\n')[0];
        var sep = firstLine.Count(c => c == ';') >= firstLine.Count(c => c == ',') ? ';' : ',';
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"' && field.Length == 0) quoted = true;
            else if (c == sep) { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Clear();
                if (row.Count > 1 || row[0].Length > 0) rows.Add(row);
                row = new List<string>();
            }
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }
}
