using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Renci.SshNet;
using Renci.SshNet.Common;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

/// <summary>Secret d'un compte SSH (CFG-52) : mot de passe, ou clé privée et sa phrase ; chiffré au coffre.</summary>
public record SshSecret(string? Password, string? PrivateKey, string? Passphrase)
{
    public string Serialize() => JsonSerializer.Serialize(this);
    public static SshSecret Parse(string stored) =>
        JsonSerializer.Deserialize<SshSecret>(stored) ?? throw new InvalidOperationException("Secret SSH illisible.");

    /// <summary>Message d'erreur si la clé privée ne se lit pas (format, phrase), ou null.</summary>
    public static string? CheckKey(string privateKey, string? passphrase)
    {
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(privateKey.Trim() + "\n"));
            using var _ = string.IsNullOrEmpty(passphrase) ? new PrivateKeyFile(stream) : new PrivateKeyFile(stream, passphrase);
            return null;
        }
        catch (Exception ex) when (ex is SshException or SshPassPhraseNullOrEmptyException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return "Clé privée SSH illisible (format OpenSSH ou PEM attendu, ou phrase de la clé incorrecte).";
        }
    }

    public AuthenticationMethod Method(string user)
    {
        if (string.IsNullOrEmpty(PrivateKey)) return new PasswordAuthenticationMethod(user, Password ?? "");
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(PrivateKey.Trim() + "\n"));
        var key = string.IsNullOrEmpty(Passphrase) ? new PrivateKeyFile(stream) : new PrivateKeyFile(stream, Passphrase);
        return new PrivateKeyAuthenticationMethod(user, key);
    }
}

/// <summary>
/// Collecteur SSH (Linux, Unix) : inventaire en lecture seule, sans élévation de droits —
/// système, constructeur et modèle (DMI), logiciels installés, services actifs, ports en
/// écoute (CFG-52). La clé d'hôte est vérifiée avant l'envoi de l'identifiant (CFG-53).
/// </summary>
public class SshCollector(AppDbContext db, ILogger<SshCollector> logger) : IDiscoveryCollector
{
    public string ScanType => "Ssh";
    private const int ProbeTimeoutMs = 400, ConnectTimeoutSeconds = 5, CommandTimeoutSeconds = 20;
    public const int MaxSoftware = 3000;

    /// <summary>
    /// Script d'inventaire, envoyé tel quel : uniquement des lectures, chaque section marquée
    /// (« ##os »…), une commande absente laisse sa section vide.
    /// </summary>
    public const string Script = """
        echo '##os'; cat /etc/os-release 2>/dev/null
        echo '##uname'; uname -srm 2>/dev/null
        echo '##hostname'; hostname -f 2>/dev/null || hostname 2>/dev/null
        echo '##dmi'; for f in sys_vendor product_name product_serial; do printf '%s=' "$f"; cat "/sys/class/dmi/id/$f" 2>/dev/null || echo; done
        echo '##pkg'
        if command -v dpkg-query >/dev/null 2>&1; then dpkg-query -W -f='${Package}\t${Version}\n' 2>/dev/null
        elif command -v rpm >/dev/null 2>&1; then rpm -qa --qf '%{NAME}\t%{VERSION}-%{RELEASE}\n' 2>/dev/null
        elif command -v apk >/dev/null 2>&1; then apk info -v 2>/dev/null; fi
        echo '##svc'; command -v systemctl >/dev/null 2>&1 && systemctl list-units --type=service --state=running --no-legend --plain 2>/dev/null
        echo '##listen'; ss -tlnH 2>/dev/null || netstat -tln 2>/dev/null
        """;

    public async Task<List<Observation>> CollectAsync(CollectContext context, CancellationToken ct)
    {
        var hosts = IpRange.Parse(context.Job.TargetRange, out var error) ?? throw new InvalidOperationException(error);
        var credential = context.Credential ?? throw new InvalidOperationException("Compte SSH absent : choisir un identifiant SSH du coffre.");
        var secret = SshSecret.Parse(context.Secret ?? throw new InvalidOperationException("Secret SSH absent."));
        var port = SshPort(context.Job.PortList);
        var perimeter = context.Job.PerimeterId;
        var known = new ConcurrentDictionary<string, SshHostKey>(
            (await db.SshHostKeys.Where(k => k.PerimeterId == perimeter && k.Port == port).ToListAsync(ct)).ToDictionary(k => k.Host));

        var results = new List<Observation>();
        using var throttle = new SemaphoreSlim(Math.Max(1, context.MaxConcurrency));
        await Task.WhenAll(hosts.Select(async ip =>
        {
            await throttle.WaitAsync(ct);
            try
            {
                if (!await IsOpenAsync(ip, port, ct)) return;
                if (await ReadAsync(ip, port, credential.Username ?? "", secret, known, perimeter, ct) is { } seen) lock (results) results.Add(seen);
            }
            finally { throttle.Release(); }
        }));

        // Clés nouvelles ou changées : enregistrées après la collecte (le contexte n'est pas partagé entre tâches).
        foreach (var key in known.Values.Where(k => k.Id == 0)) db.SshHostKeys.Add(key);
        await db.SaveChangesAsync(ct);
        return results;
    }

    /// <summary>Port SSH de la tâche : le seul port de sa liste, sinon 22.</summary>
    public static int SshPort(string? portList) =>
        IpRange.Ports(portList, out _) is [var single] ? single : 22;

    /// <summary>Inventaire d'un hôte, ou null (clé d'hôte refusée, accès refusé, hôte muet).</summary>
    public async Task<Observation?> ReadAsync(IPAddress ip, int port, string user, SshSecret secret,
        ConcurrentDictionary<string, SshHostKey> known, int perimeter, CancellationToken ct)
    {
        var host = ip.ToString();
        var info = new Renci.SshNet.ConnectionInfo(host, port, user, secret.Method(user)) { Timeout = TimeSpan.FromSeconds(ConnectTimeoutSeconds) };
        using var client = new SshClient(info);
        client.HostKeyReceived += (_, e) => e.CanTrust = Trust(known, perimeter, host, port, e.HostKeyName, e.FingerPrintSHA256);
        try
        {
            await client.ConnectAsync(ct);
            using var command = client.CreateCommand(Script);
            command.CommandTimeout = TimeSpan.FromSeconds(CommandTimeoutSeconds);
            await command.ExecuteAsync(ct);
            return SshInventory.Parse(command.Result, host);
        }
        catch (Exception ex) when (ex is SshException or SocketException or ProxyException or TimeoutException
                                       or OperationCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogDebug("SSH sans inventaire pour {Host}:{Port} : {Error}", host, port, ex.Message);
            return null;
        }
        finally
        {
            if (client.IsConnected) client.Disconnect();
        }
    }

    /// <summary>
    /// CFG-53 : confiance au premier contact. Clé inconnue : retenue ; même clé : acceptée ;
    /// clé différente : refusée (la connexion s'arrête avant l'authentification) et notée en
    /// attente de l'Admin.
    /// </summary>
    public static bool Trust(ConcurrentDictionary<string, SshHostKey> known, int perimeter, string host, int port, string algorithm, string fingerprint)
    {
        var now = DateTime.UtcNow;
        var key = known.GetOrAdd(host, _ => new SshHostKey
        {
            PerimeterId = perimeter, Host = host, Port = port, Algorithm = algorithm, Fingerprint = fingerprint, FirstSeenAt = now
        });
        lock (key)
        {
            if (key.Algorithm == algorithm && key.Fingerprint == fingerprint)
            {
                key.LastSeenAt = now;
                return true;
            }
            (key.PendingAlgorithm, key.PendingFingerprint, key.PendingSeenAt) = (algorithm, fingerprint, now);
            return false;
        }
    }

    private static async Task<bool> IsOpenAsync(IPAddress ip, int port, CancellationToken ct)
    {
        using var probe = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ProbeTimeoutMs);
        try { await probe.ConnectAsync(ip, port, timeout.Token); return true; }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException && !ct.IsCancellationRequested) { return false; }
    }
}

/// <summary>Lecture de la sortie du script d'inventaire SSH (CFG-52).</summary>
public static class SshInventory
{
    private static readonly string[] Placeholders = ["", "0", "none", "default string", "to be filled by o.e.m.", "not specified", "system serial number", "system product name"];

    public static Observation Parse(string output, string ip)
    {
        var sections = Sections(output);
        var os = OsName(sections.GetValueOrDefault("os"), sections.GetValueOrDefault("uname"));
        var dmi = KeyValues(sections.GetValueOrDefault("dmi"), '=');
        var vendor = Useful(dmi.GetValueOrDefault("sys_vendor"));
        var hostname = sections.GetValueOrDefault("hostname")?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        var software = Packages(sections.GetValueOrDefault("pkg"));
        var services = Services(sections.GetValueOrDefault("svc"));
        var listening = ListeningPorts(sections.GetValueOrDefault("listen"));
        return new Observation("Ssh", IpAddress: ip,
            Hostname: NetworkScanner.UsefulHostname(hostname, IPAddress.Parse(ip)),
            SerialNumber: Useful(dmi.GetValueOrDefault("product_serial")),
            Vendor: vendor is null ? null : MacVendors.ShortName(vendor),
            Model: Useful(dmi.GetValueOrDefault("product_name")),
            Os: os, Type: "Serveur",
            OpenPorts: listening.Count > 0 ? string.Join(",", listening) : null,
            Software: software, Services: services.Count > 0 ? string.Join("\n", services) : null,
            ListeningPorts: listening.Count > 0 ? string.Join(",", listening) : null);
    }

    public static Dictionary<string, string> Sections(string output)
    {
        var sections = new Dictionary<string, string>();
        string? current = null;
        var text = new StringBuilder();
        foreach (var line in output.Replace("\r", "").Split('\n'))
        {
            if (line.StartsWith("##", StringComparison.Ordinal) && line.Length > 2 && !line.Contains(' '))
            {
                if (current is not null) sections[current] = text.ToString();
                current = line[2..];
                text.Clear();
            }
            else if (current is not null) text.Append(line).Append('\n');
        }
        if (current is not null) sections[current] = text.ToString();
        return sections;
    }

    /// <summary>Nom du système : <c>PRETTY_NAME</c> d'os-release, sinon <c>uname</c>.</summary>
    public static string? OsName(string? osRelease, string? uname)
    {
        var values = KeyValues(osRelease, '=');
        var pretty = values.GetValueOrDefault("PRETTY_NAME") ?? values.GetValueOrDefault("NAME");
        var name = Banner.Clean(pretty?.Trim('"', '\'')) ?? Banner.Clean(uname?.Split('\n')[0]);
        return name is { Length: > 150 } ? name[..150] : name;
    }

    /// <summary>Paquets : « nom⇥version » (dpkg, rpm) ou « nom-version-rN » (apk), 3 000 au plus.</summary>
    public static List<SoftwareItem> Packages(string? text)
    {
        var list = new List<SoftwareItem>();
        foreach (var raw in (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (list.Count >= SshCollector.MaxSoftware) break;
            string name, version;
            if (raw.Contains('\t'))
            {
                var parts = raw.Split('\t', 2);
                (name, version) = (parts[0].Trim(), parts[1].Trim());
            }
            else
            {
                // apk : « musl-1.2.5-r1 » → musl, 1.2.5-r1 (la version commence au premier segment chiffré).
                var segments = raw.Split('-');
                var at = Array.FindIndex(segments, 1, s => s.Length > 0 && char.IsDigit(s[0]));
                if (at < 1) continue;
                (name, version) = (string.Join('-', segments[..at]), string.Join('-', segments[at..]));
            }
            if (name.Length == 0) continue;
            list.Add(new SoftwareItem(Cut(name, 200)!, Cut(version.Length == 0 ? null : version, 100)));
        }
        return list;
    }

    /// <summary>Services systemd en cours : « ssh.service loaded active running … » → « ssh ».</summary>
    public static List<string> Services(string? text) =>
        (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.Split(' ', 2)[0])
            .Where(u => u.EndsWith(".service", StringComparison.Ordinal))
            .Select(u => u[..^".service".Length])
            .Distinct().Order().Take(300).ToList();

    /// <summary>Ports TCP en écoute (ss ou netstat), hors adresses de boucle locale, triés.</summary>
    public static List<int> ListeningPorts(string? text)
    {
        var ports = new SortedSet<int>();
        foreach (var line in (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var local = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(t => t.Contains(':'));
            if (local is null) continue;
            var at = local.LastIndexOf(':');
            var address = local[..at].Trim('[', ']');
            if (address.StartsWith("127.") || address is "::1" || address.StartsWith("::ffff:127.")) continue;
            if (int.TryParse(local[(at + 1)..], out var port) && port is > 0 and < 65536) ports.Add(port);
        }
        return [.. ports.Take(50)];
    }

    private static Dictionary<string, string> KeyValues(string? text, char sep)
    {
        var values = new Dictionary<string, string>();
        foreach (var line in (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var at = line.IndexOf(sep);
            if (at > 0) values[line[..at].Trim()] = line[(at + 1)..].Trim().Trim('"');
        }
        return values;
    }

    private static string? Useful(string? v)
    {
        var clean = Banner.Clean(v);
        return clean is null || Placeholders.Contains(clean.ToLowerInvariant()) ? null : clean;
    }

    private static string? Cut(string? v, int max) => v is null || v.Length <= max ? v : v[..max];
}
