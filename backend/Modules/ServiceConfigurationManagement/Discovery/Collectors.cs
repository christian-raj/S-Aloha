using System.Net;
using System.Net.Sockets;

namespace SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

/// <summary>Ce qu'un collecteur a vu d'un équipement, avant son entrée dans le sas de staging.</summary>
public record Observation(
    string Source,
    string? IpAddress = null, string? MacAddress = null, string? Hostname = null,
    string? SerialNumber = null, string? Vendor = null, string? Model = null, string? Os = null,
    string Type = "Autre", string? OpenPorts = null, string? Location = null,
    bool InDirectory = false, string? Raw = null, string? Banners = null, string? Interfaces = null);

/// <summary>Tâche à exécuter, avec le secret déchiffré de son identifiant (jamais journalisé).</summary>
public record CollectContext(DiscoveryJob Job, DiscoveryCredential? Credential, string? Secret, int MaxConcurrency);

/// <summary>Collecteur d'un type de scan (<see cref="DiscoveryJob.ScanTypes"/>).</summary>
public interface IDiscoveryCollector
{
    string ScanType { get; }
    Task<List<Observation>> CollectAsync(CollectContext context, CancellationToken ct);
}

/// <summary>
/// Plage d'un scan réseau : une adresse IPv4, ou un réseau CIDR de /24 au plus large
/// (CFG-46 : 254 hôtes au plus par tâche, pour ne pas saturer le réseau).
/// </summary>
public static class IpRange
{
    public const int WidestPrefix = 24;

    public static string? Validate(string? range) => Parse(range, out var error) is null ? error : null;

    public static List<IPAddress>? Parse(string? range, out string? error)
    {
        error = null;
        var text = range?.Trim() ?? "";
        var parts = text.Split('/');
        if (!IPAddress.TryParse(parts[0], out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
        {
            error = $"Plage invalide : « {text} » (attendu : une adresse IPv4 ou un réseau CIDR, ex. 192.168.1.0/24).";
            return null;
        }
        if (parts.Length == 1) return [ip];
        if (parts.Length > 2 || !int.TryParse(parts[1], out var prefix) || prefix is < 0 or > 32)
        {
            error = $"Masque invalide : « {text} ».";
            return null;
        }
        if (prefix < WidestPrefix)
        {
            error = $"Plage trop large : /{prefix}. Découper en réseaux de /{WidestPrefix} au plus (254 hôtes).";
            return null;
        }
        var b = ip.GetAddressBytes();
        var mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        var network = ((uint)b[0] << 24 | (uint)b[1] << 16 | (uint)b[2] << 8 | b[3]) & mask;
        var size = 1u << (32 - prefix);
        // Adresse de réseau et de diffusion exclues, sauf pour /31 et /32.
        var (first, last) = size <= 2 ? (0u, size - 1) : (1u, size - 2);
        var list = new List<IPAddress>();
        for (var i = first; i <= last; i++)
        {
            var v = network + i;
            list.Add(new IPAddress(new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v }));
        }
        return list;
    }

    /// <summary>Ports TCP sondés : liste « 22,80,443 » ; message d'erreur ou null.</summary>
    public static int[]? Ports(string? list, out string? error)
    {
        error = null;
        var items = (list ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var ports = new List<int>();
        foreach (var item in items)
        {
            if (!int.TryParse(item, out var p) || p is < 1 or > 65535)
            {
                error = $"Port invalide : « {item} » (1 à 65535).";
                return null;
            }
            if (!ports.Contains(p)) ports.Add(p);
        }
        if (ports.Count > 50) { error = "50 ports au plus par tâche."; return null; }
        return [.. ports];
    }
}

/// <summary>CFG-49 : lecture des bannières, réduites à une ligne de texte imprimable et bornée.</summary>
public static class Banner
{
    public const int MaxLength = 120;

    public static string? Clean(string? text)
    {
        if (text is null) return null;
        var printable = new string(text.Where(c => c >= ' ' && c < 127).ToArray()).Trim();
        return printable.Length == 0 ? null : printable.Length > MaxLength ? printable[..MaxLength] : printable;
    }

    /// <summary>Ligne d'identification d'un serveur SSH (RFC 4253 § 4.2), ou null.</summary>
    public static string? Ssh(string received)
    {
        var line = received.Split('\n').Select(l => l.TrimEnd('\r')).FirstOrDefault(l => l.StartsWith("SSH-", StringComparison.Ordinal));
        return Clean(line);
    }

    /// <summary>Valeur de l'en-tête <c>Server</c> d'une réponse HTTP, ou null.</summary>
    public static string? HttpServer(string received)
    {
        var lines = received.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        if (lines.Count == 0 || !lines[0].StartsWith("HTTP/", StringComparison.Ordinal)) return null;
        var header = lines.Skip(1).TakeWhile(l => l.Length > 0)
            .FirstOrDefault(l => l.StartsWith("Server:", StringComparison.OrdinalIgnoreCase));
        return Clean(header?["Server:".Length..]);
    }
}

/// <summary>Déduction prudente du type d'équipement et du système, d'après les ports ouverts et le TTL.</summary>
public static class Fingerprint
{
    public static (string Type, string? Os) FromPorts(IReadOnlyCollection<int> open, int? ttl)
    {
        if (open.Contains(9100) || open.Contains(515) || open.Contains(631)) return ("Autre", "Imprimante (micrologiciel)");
        var windows = open.Contains(135) || open.Contains(445) || open.Contains(5985);
        if (windows) return (open.Contains(3389) && !open.Contains(139) ? "Serveur" : "Poste de travail", "Microsoft Windows");
        if (open.Contains(22)) return ("Serveur", ttl is > 0 and <= 64 ? "Linux / Unix" : null);
        if (open.Contains(161) || open.Contains(23)) return ("Réseau", null);
        if (open.Contains(80) || open.Contains(443)) return ("Autre", null);
        return ("Autre", null);
    }

    /// <summary>
    /// CFG-49 : précisions tirées des bannières (port, texte) ; chaque valeur est null quand
    /// aucune bannière ne la donne, et les ports gardent alors le dernier mot.
    /// </summary>
    public static (string? Vendor, string? Os, string? Type) FromBanners(IEnumerable<(int Port, string Text)> banners)
    {
        string? vendor = null, os = null, type = null;
        foreach (var (port, text) in banners)
        {
            var t = text.ToLowerInvariant();
            if (port == 22)
            {
                if (t.Contains("openssh_for_windows")) os = "Microsoft Windows";
                else if (t.Contains("ubuntu")) os = "Linux Ubuntu";
                else if (t.Contains("debian")) os = "Linux Debian";
                else if (t.Contains("raspbian")) os = "Linux Raspbian";
                else if (t.Contains("freebsd")) os = "FreeBSD";
                else if (t.Contains("cisco")) (vendor, type) = ("Cisco", "Réseau");
                else if (t.Contains("rosssh")) (vendor, os, type) = ("MikroTik", "RouterOS", "Réseau");
                else if (t.Contains("openssh")) os ??= "Linux / Unix";
            }
            else
            {
                if (t.StartsWith("microsoft-iis")) (os, type) = ("Microsoft Windows", "Serveur");
                else if (t.Contains("hp http server") || t.Contains("hp-chaisoe")) (vendor, os, type) = ("HP", "Imprimante (micrologiciel)", "Autre");
                else if (t.Contains("synology")) (vendor, type) = ("Synology", "Stockage");
                else if (t.Contains("qnap")) (vendor, type) = ("QNAP", "Stockage");
                else if (t.Contains("routeros") || t.Contains("mikrotik")) (vendor, os, type) = ("MikroTik", "RouterOS", "Réseau");
                else if (t.Contains("ubuntu")) os ??= "Linux Ubuntu";
                else if (t.Contains("debian")) os ??= "Linux Debian";
            }
        }
        return (vendor, os, type);
    }

    /// <summary>Constructeur, modèle, système et type d'après la chaîne <c>sysDescr</c> (MIB-II).</summary>
    public static (string? Vendor, string? Model, string? Os, string Type) FromSysDescr(string? sysDescr)
    {
        if (string.IsNullOrWhiteSpace(sysDescr)) return (null, null, null, "Réseau");
        var d = sysDescr.ToLowerInvariant();
        var os = sysDescr.Length > 150 ? sysDescr[..150] : sysDescr;
        if (d.Contains("cisco")) return ("Cisco", null, os, "Réseau");
        if (d.Contains("juniper") || d.Contains("junos")) return ("Juniper", null, os, "Réseau");
        if (d.Contains("fortigate") || d.Contains("fortios")) return ("Fortinet", null, os, "Réseau");
        if (d.Contains("procurve") || d.Contains("aruba")) return ("HPE Aruba", null, os, "Réseau");
        if (d.Contains("mikrotik") || d.Contains("routeros")) return ("MikroTik", null, os, "Réseau");
        if (d.Contains("synology") || d.Contains("diskstation")) return ("Synology", null, os, "Stockage");
        if (d.Contains("qnap")) return ("QNAP", null, os, "Stockage");
        if (d.Contains("printer") || d.Contains("laserjet") || d.Contains("imprimante")) return (null, null, os, "Autre");
        if (d.Contains("windows")) return ("Microsoft", null, os, "Serveur");
        if (d.Contains("linux")) return (null, null, os, "Serveur");
        return (null, null, os, "Réseau");
    }
}
