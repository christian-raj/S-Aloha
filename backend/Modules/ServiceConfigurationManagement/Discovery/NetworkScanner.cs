using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

/// <summary>
/// Balayage IP non intrusif (CFG-46) : ping, puis connexion TCP brève sur les ports témoins
/// de la tâche, nom par DNS inverse, adresse MAC par la table ARP locale quand l'hôte est sur
/// le même segment. Un hôte qui ne répond pas au ping mais ouvre un port est retenu (ICMP
/// filtré, ou interdit au conteneur de l'API).
/// </summary>
public class NetworkScanner(ILogger<NetworkScanner> logger) : IDiscoveryCollector
{
    public string ScanType => "IpSweep";
    private const int PingTimeoutMs = 600, PortTimeoutMs = 400, DnsTimeoutMs = 1500;

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
        return new Observation("IpSweep", IpAddress: ip.ToString(), Hostname: await ReverseDnsAsync(ip, ct),
            Os: os, Type: type, OpenPorts: open.Count > 0 ? string.Join(",", open) : null);
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
            return entry.HostName == ip.ToString() ? null : entry.HostName.ToLowerInvariant();
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException or ArgumentException)
        {
            return null;
        }
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
