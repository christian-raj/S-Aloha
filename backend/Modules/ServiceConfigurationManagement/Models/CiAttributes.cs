using System.Globalization;

namespace SAloha.Api.Modules.ServiceConfigurationManagement;

/// <summary>Attribut défini pour un type de CI (CFG-16) : clé de l'API, libellé, nature de la valeur.</summary>
public record CiAttribute(string Key, string Label, string Kind = CiAttributes.Text);

/// <summary>
/// CFG-16 : attributs facultatifs propres à chaque type de CI. Les colonnes dédiées
/// (identification, portefeuille applicatif, actifs matériels, § 3.2 à 3.4) n'y figurent pas.
/// </summary>
public static class CiAttributes
{
    public const string Text = "texte", Number = "nombre", Url = "url";
    public const int MaxLength = 200;

    public static readonly Dictionary<string, CiAttribute[]> ByType = new()
    {
        ["Serveur"] = [new("cpu", "Processeurs (cœurs)", Number), new("memoryGb", "Mémoire (Go)", Number), new("diskGb", "Disque (Go)", Number),
                       new("hypervisor", "Hyperviseur"), new("role", "Rôle")],
        ["Application"] = [new("version", "Version"), new("publisher", "Éditeur"), new("url", "Adresse", Url), new("license", "Licence")],
        ["Base de données"] = [new("engine", "Moteur"), new("version", "Version"), new("sizeGb", "Taille (Go)", Number), new("instance", "Instance")],
        ["Réseau"] = [new("firmware", "Micrologiciel"), new("ports", "Ports", Number), new("role", "Rôle")],
        ["Stockage"] = [new("capacityTb", "Capacité (To)", Number), new("protocol", "Protocole"), new("raid", "RAID")],
        ["Poste de travail"] = [new("user", "Utilisateur"), new("cpu", "Processeurs (cœurs)", Number), new("memoryGb", "Mémoire (Go)", Number)],
        ["Logiciel"] = [new("version", "Version"), new("publisher", "Éditeur"), new("license", "Licence"), new("installs", "Installations", Number)],
        ["Autre"] = [],
    };

    /// <summary>
    /// Attributs saisis pour <paramref name="type"/> : clés connues du type, valeurs vides retirées,
    /// nombres et adresses vérifiés. Renvoie l'erreur (400) ou les attributs retenus.
    /// </summary>
    public static (string? Error, Dictionary<string, string> Values) Check(string type, IReadOnlyDictionary<string, string?> input)
    {
        var defined = ByType.GetValueOrDefault(type, []).ToDictionary(a => a.Key);
        var values = new Dictionary<string, string>();
        foreach (var (key, raw) in input)
        {
            var value = raw?.Trim() ?? "";
            if (value == "") continue;
            if (!defined.TryGetValue(key, out var a))
                return ($"Attribut « {key} » inconnu pour le type {type}" + (defined.Count == 0 ? " (aucun attribut défini)." : $" (attendus : {string.Join(", ", defined.Keys)})."), values);
            if (value.Length > MaxLength) return ($"{a.Label} : {MaxLength} caractères au plus.", values);
            if (a.Kind == Number && !decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                return ($"{a.Label} : un nombre est attendu (« {value} »).", values);
            if (a.Kind == Url && (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
                return ($"{a.Label} : une adresse http(s) est attendue (« {value} »).", values);
            values[key] = a.Kind == Number ? value.Replace(',', '.') : value;
        }
        return (null, values);
    }

    /// <summary>Changement de type : seuls les attributs que le nouveau type définit restent.</summary>
    public static Dictionary<string, string> Keep(string type, Dictionary<string, string> current)
    {
        var keys = ByType.GetValueOrDefault(type, []).Select(a => a.Key).ToHashSet();
        return current.Where(kv => keys.Contains(kv.Key)).ToDictionary();
    }
}
