using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

/// <summary>
/// Balayage IP non intrusif (CFG-46) : ping, puis connexion TCP brève sur les ports témoins
/// de la tâche, nom par DNS inverse, adresse MAC par la table ARP locale quand l'hôte est sur
/// le même segment. Un hôte qui ne répond pas au ping mais ouvre un port est retenu (ICMP
/// filtré, ou interdit au conteneur de l'API). Sur les ports SSH et HTTP ouverts, la bannière
/// que le service annonce de lui-même est lue (CFG-49) : aucune authentification, aucune
/// requête au-delà d'un <c>HEAD /</c>.
/// </summary>
public class NetworkScanner(ILogger<NetworkScanner> logger) : IDiscoveryCollector
{
    public string ScanType => "IpSweep";
    private const int PingTimeoutMs = 600, PortTimeoutMs = 400, DnsTimeoutMs = 1500, BannerTimeoutMs = 1500;
    /// <summary>Ports HTTP en clair dont on lit l'en-tête <c>Server</c> (HTTPS : phase 3, avec le certificat).</summary>
    public static readonly int[] HttpPorts = [80, 8000, 8008, 8080];

    public async Task<List<Observation>> CollectAsync(CollectContext context, CancellationToken ct)
    {
        var hosts = IpRange.Parse(context.Job.TargetRange, out var error) ?? throw new InvalidOperationException(error);
        var ports = IpRange.Ports(context.Job.PortList, out error) ?? throw new InvalidOperationException(error);
        var results = new List<Observation>();
        using var throttle = new SemaphoreSlim(Math.Max(1, context.MaxConcurrency));
        await Task.WhenAll(hosts.Select(async ip =>
        {
            await throttle.WaitAsync(ct);
            try
            {
                if (await ProbeAsync(ip, ports, ct) is { } seen) lock (results) results.Add(seen);
            }
            finally { throttle.Release(); }
        }));
        var arp = ReadArpTable();
        return results.Select(o => o with { MacAddress = arp.GetValueOrDefault(o.IpAddress!) }).ToList();
    }

    private async Task<Observation?> ProbeAsync(IPAddress ip, int[] ports, CancellationToken ct)
    {
        int? ttl = null;
        var alive = false;
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ip, TimeSpan.FromMilliseconds(PingTimeoutMs), cancellationToken: ct);
            alive = reply.Status == IPStatus.Success;
            ttl = reply.Options?.Ttl;
        }
        catch (Exception ex) when (ex is PingException or PlatformNotSupportedException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Ping indisponible pour {Ip} : sondage TCP seul", ip);
        }

        var open = new List<int>();
        foreach (var port in ports)
        {
            ct.ThrowIfCancellationRequested();
            if (await IsOpenAsync(ip, port, ct)) open.Add(port);
        }
        if (!alive && open.Count == 0) return null;

        var (type, os) = Fingerprint.FromPorts(open, ttl);
        var banners = new List<(int Port, string Text)>();
        foreach (var port in open.Where(p => p == 22 || HttpPorts.Contains(p)))
            if (await ReadBannerAsync(ip, port, port == 22, ct) is { } text) banners.Add((port, text));
        var hint = Fingerprint.FromBanners(banners);
        return new Observation("IpSweep", IpAddress: ip.ToString(), Hostname: await ReverseDnsAsync(ip, ct),
            Vendor: hint.Vendor, Os: hint.Os ?? os, Type: hint.Type ?? type, OpenPorts: open.Count > 0 ? string.Join(",", open) : null,
            Banners: banners.Count > 0 ? string.Join("\n", banners.Select(b => $"{b.Port} {b.Text}")) : null);
    }

    /// <summary>
    /// CFG-49 : ligne d'identification SSH (« SSH-2.0-… », envoyée par le serveur dès la
    /// connexion) ou en-tête <c>Server</c> d'une réponse à <c>HEAD /</c> ; null sinon.
    /// </summary>
    public static async Task<string?> ReadBannerAsync(IPAddress ip, int port, bool ssh, CancellationToken ct)
    {
        using var client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(BannerTimeoutMs);
        try
        {
            await client.ConnectAsync(ip, port, timeout.Token);
            var stream = client.GetStream();
            if (!ssh)
            {
                var request = System.Text.Encoding.ASCII.GetBytes($"HEAD / HTTP/1.0\r\nHost: {ip}\r\nUser-Agent: S-Aloha-Discovery\r\n\r\n");
                await stream.WriteAsync(request, timeout.Token);
            }
            var buffer = new byte[4096];
            var length = 0;
            while (length < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(length), timeout.Token);
                if (read == 0) break;
                length += read;
                if (ssh && Array.IndexOf(buffer, (byte)'\n', 0, length) >= 0) break;
                if (!ssh && System.Text.Encoding.ASCII.GetString(buffer, 0, length).Contains("\r\n\r\n")) break;
            }
            var text = System.Text.Encoding.ASCII.GetString(buffer, 0, length);
            return ssh ? Banner.Ssh(text) : Banner.HttpServer(text);
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            return null;   // service muet ou lent : pas de bannière
        }
    }

    private static async Task<bool> IsOpenAsync(IPAddress ip, int port, CancellationToken ct)
    {
        using var client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(PortTimeoutMs);
        try
        {
            await client.ConnectAsync(ip, port, timeout.Token);
            return true;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            return false;
        }
    }

    private static async Task<string?> ReverseDnsAsync(IPAddress ip, CancellationToken ct)
    {
        try
        {
            var entry = await Dns.GetHostEntryAsync(ip.ToString(), ct).WaitAsync(TimeSpan.FromMilliseconds(DnsTimeoutMs), ct);
            return UsefulHostname(entry.HostName, ip);
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Nom réel d'un hôte, ou null pour un nom fabriqué : l'adresse elle-même, ou le nom
    /// synthétique que le DNS d'un cluster Kubernetes donne à toute adresse
    /// (« 192-168-1-11.kubernetes.default.svc.cluster.local ») — il fausserait le rapprochement.
    /// </summary>
    public static string? UsefulHostname(string? name, IPAddress ip)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var host = name.Trim().TrimEnd('.').ToLowerInvariant();
        var dashed = ip.ToString().Replace('.', '-');
        if (host == ip.ToString() || host.Split('.')[0] == dashed || host.EndsWith(".cluster.local")) return null;
        return host;
    }

    /// <summary>Table ARP du système (Linux) : adresse IP → adresse MAC, pour les hôtes du même segment.</summary>
    private static Dictionary<string, string> ReadArpTable()
    {
        const string path = "/proc/net/arp";
        var table = new Dictionary<string, string>();
        if (!File.Exists(path)) return table;
        foreach (var line in File.ReadLines(path).Skip(1))
        {
            var cols = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (cols.Length >= 4 && CiRules.NormalizeMac(cols[3]) is { } mac && mac != "00:00:00:00:00:00") table[cols[0]] = mac;
        }
        return table;
    }
}
