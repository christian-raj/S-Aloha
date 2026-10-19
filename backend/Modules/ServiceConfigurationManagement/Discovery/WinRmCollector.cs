using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

/// <summary>
/// Client WS-Management minimal (DSP0226) : énumération d'une classe CIM (Enumerate puis Pull)
/// sur <c>/wsman</c>, en lecture seule. Chaque instance est rendue en dictionnaire propriété →
/// valeurs (une propriété tableau, comme <c>IPAddress</c>, a plusieurs valeurs).
/// </summary>
public class WsManClient(HypervisorApi http)
{
    public const string Cimv2 = "http://schemas.microsoft.com/wbem/wsman/1/wmi/root/cimv2/";
    public const string StandardCimv2 = "http://schemas.microsoft.com/wbem/wsman/1/wmi/root/StandardCimv2/";
    private const string Enumerate = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/Enumerate";
    private const string Pull = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/Pull";
    private const int MaxElements = 200, MaxPages = 50;

    /// <summary>Instances de la classe ; une classe absente (ancien Windows) donne une liste vide.</summary>
    public async Task<List<Dictionary<string, List<string>>>> EnumerateAsync(string resourceUri, CancellationToken ct, int max = 5000)
    {
        var items = new List<Dictionary<string, List<string>>>();
        var reply = await SendAsync(resourceUri, Enumerate, "<n:Enumerate><w:OptimizeEnumeration/>"
            + $"<w:MaxElements>{MaxElements}</w:MaxElements></n:Enumerate>", ct);
        for (var page = 0; reply is not null && page < MaxPages; page++)
        {
            items.AddRange(Instances(reply).Take(max - items.Count));
            var context = reply.Descendants().FirstOrDefault(e => e.Name.LocalName == "EnumerationContext")?.Value;
            var done = reply.Descendants().Any(e => e.Name.LocalName == "EndOfSequence");
            if (done || context is null || items.Count >= max) break;
            reply = await SendAsync(resourceUri, Pull, $"<n:Pull><n:EnumerationContext>{SecurityElement.Escape(context)}</n:EnumerationContext>"
                + $"<n:MaxElements>{MaxElements}</n:MaxElements></n:Pull>", ct);
        }
        return items;
    }

    /// <summary>Instances d'une réponse : enfants de <c>Items</c>, chaque propriété par son nom local.</summary>
    public static IEnumerable<Dictionary<string, List<string>>> Instances(XDocument reply) =>
        reply.Descendants().Where(e => e.Name.LocalName == "Items").SelectMany(i => i.Elements())
            .Select(instance =>
            {
                var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in instance.Elements())
                {
                    if (p.Attributes().Any(a => a.Name.LocalName == "nil" && a.Value == "true")) continue;
                    if (!values.TryGetValue(p.Name.LocalName, out var list)) values[p.Name.LocalName] = list = [];
                    list.Add(p.Value.Trim());
                }
                return values;
            });

    /// <summary>
    /// Requête SOAP ; une faute « classe inconnue » (ancien Windows, espace WMI absent) donne
    /// null ; une autre faute est levée avec son motif.
    /// </summary>
    private async Task<XDocument?> SendAsync(string resourceUri, string action, string body, CancellationToken ct)
    {
        var to = new Uri(http.BaseUri, "wsman").ToString();
        var envelope = $"""
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:a="http://schemas.xmlsoap.org/ws/2004/08/addressing" xmlns:w="http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd" xmlns:n="http://schemas.xmlsoap.org/ws/2004/09/enumeration">
            <s:Header>
            <a:To>{SecurityElement.Escape(to)}</a:To>
            <w:ResourceURI s:mustUnderstand="true">{SecurityElement.Escape(resourceUri)}</w:ResourceURI>
            <a:ReplyTo><a:Address s:mustUnderstand="true">http://schemas.xmlsoap.org/ws/2004/08/addressing/role/anonymous</a:Address></a:ReplyTo>
            <a:Action s:mustUnderstand="true">{action}</a:Action>
            <w:MaxEnvelopeSize s:mustUnderstand="true">512000</w:MaxEnvelopeSize>
            <a:MessageID>uuid:{Guid.NewGuid()}</a:MessageID>
            <w:OperationTimeout>PT30S</w:OperationTimeout>
            </s:Header>
            <s:Body>{body}</s:Body>
            </s:Envelope>
            """;
        var text = await http.PostRawAsync("wsman", envelope, "application/soap+xml;charset=UTF-8", ct);
        XDocument reply;
        try { reply = XDocument.Parse(text); }
        catch (System.Xml.XmlException ex) { throw new InvalidOperationException($"{http.BaseUri.Host} : réponse WS-Management illisible.", ex); }
        if (reply.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault") is not { } fault) return reply;
        var reason = fault.Descendants().FirstOrDefault(e => e.Name.LocalName == "Text")?.Value ?? "faute SOAP";
        var detail = string.Join(" ", fault.Descendants().Where(e => e.Name.LocalName is "Value" or "Message").Select(e => e.Value));
        // Classe ou espace de noms WMI absents : pas une erreur de l'inventaire.
        if (detail.Contains("InvalidSelectors") || detail.Contains("DestinationUnreachable") || reason.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("2150858752") || detail.Contains("0x80041010")) return null;
        throw new InvalidOperationException($"{http.BaseUri.Host} : {reason.Trim()}");
    }
}

/// <summary>
/// Collecteur WinRM (Windows, CFG-57) : inventaire en lecture seule par WS-Management sur HTTPS
/// (port 5986), avec un compte de domaine ou local du coffre (authentification NTLM). Système,
/// constructeur, modèle et numéro de série, adresses, logiciels, services actifs, ports en
/// écoute et connexions établies (flux, CFG-56). Le certificat est retenu au premier contact
/// (CFG-53). À valider sur un Windows réel : testé contre un serveur WS-Management factice.
/// </summary>
public class WinRmCollector(AppDbContext db, ILogger<WinRmCollector> logger) : IDiscoveryCollector
{
    public string ScanType => "WinRm";
    public const int DefaultPort = 5986;
    private const int ProbeTimeoutMs = 400;
    /// <summary>Ports d'administration écartés des flux entrants : WinRM lui-même, et le bureau à distance.</summary>
    private static readonly int[] AdminPorts = [5985, 5986, 3389];

    /// <summary>
    /// Tests seulement : accepte aussi l'authentification Basic du serveur factice. En service,
    /// seul NTLM est proposé, pour qu'un serveur qui réclamerait Basic n'obtienne pas le mot de passe.
    /// </summary>
    public bool AllowBasic { get; init; }

    public async Task<List<Observation>> CollectAsync(CollectContext context, CancellationToken ct)
    {
        var hosts = IpRange.Parse(context.Job.TargetRange, out var error) ?? throw new InvalidOperationException(error);
        var credential = context.Credential ?? throw new InvalidOperationException("Compte Windows absent : choisir un compte de domaine du coffre.");
        var port = IpRange.Ports(context.Job.PortList, out _) is [var single] ? single : DefaultPort;
        var perimeter = context.Job.PerimeterId;
        var known = new ConcurrentDictionary<string, SshHostKey>((await db.SshHostKeys
            .Where(k => k.PerimeterId == perimeter && k.Protocol == SshHostKey.Tls && k.Port == port).ToListAsync(ct)).ToDictionary(k => k.Host));
        var network = new NetworkCredential(credential.Username, context.Secret, credential.Domain);

        var results = new List<Observation>();
        var refused = 0;
        using var throttle = new SemaphoreSlim(Math.Max(1, context.MaxConcurrency));
        await Task.WhenAll(hosts.Select(async ip =>
        {
            await throttle.WaitAsync(ct);
            try
            {
                if (!await IsOpenAsync(ip, port, ct)) return;
                var uri = new Uri($"https://{ip}:{port}/");
                var cache = new CredentialCache { { uri, "NTLM", network } };
                if (AllowBasic) cache.Add(uri, "Basic", network);
                using var http = HypervisorApi.ForHost(uri, perimeter, known, cache);
                try
                {
                    if (await ReadAsync(new WsManClient(http), ip.ToString(), port, ct) is { } seen) lock (results) results.Add(seen);
                }
                catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or HttpRequestException
                                               or TaskCanceledException && !ct.IsCancellationRequested)
                {
                    if (ex is UnauthorizedAccessException) Interlocked.Increment(ref refused);
                    logger.LogDebug("WinRM sans inventaire pour {Host} : {Error}", ip, ex.Message);
                }
            }
            finally { throttle.Release(); }
        }));
        foreach (var key in known.Values.Where(k => k.Id == 0)) db.SshHostKeys.Add(key);
        await db.SaveChangesAsync(ct);
        if (results.Count == 0 && refused > 0)
            throw new InvalidOperationException($"Accès refusé par {refused} hôte(s) WinRM : vérifier le compte du coffre et son appartenance au groupe « Remote Management Users ».");
        return results;
    }

    /// <summary>Inventaire d'un hôte ; null si l'hôte ne publie pas même son système.</summary>
    public static async Task<Observation?> ReadAsync(WsManClient wsman, string ip, int port, CancellationToken ct)
    {
        var os = (await wsman.EnumerateAsync(WsManClient.Cimv2 + "Win32_OperatingSystem", ct)).FirstOrDefault();
        if (os is null) return null;
        var system = (await wsman.EnumerateAsync(WsManClient.Cimv2 + "Win32_ComputerSystem", ct)).FirstOrDefault() ?? [];
        var bios = (await wsman.EnumerateAsync(WsManClient.Cimv2 + "Win32_BIOS", ct)).FirstOrDefault() ?? [];
        var adapters = await wsman.EnumerateAsync(WsManClient.Cimv2 + "Win32_NetworkAdapterConfiguration", ct, 64);
        var programs = await wsman.EnumerateAsync(WsManClient.Cimv2 + "Win32_InstalledWin32Program", ct, SshCollector.MaxSoftware);
        var services = await wsman.EnumerateAsync(WsManClient.Cimv2 + "Win32_Service", ct, 1000);
        var tcp = await wsman.EnumerateAsync(WsManClient.StandardCimv2 + "MSFT_NetTCPConnection", ct, 5000);
        return Build(ip, port, os, system, bios, adapters, programs, services, tcp);
    }

    public static Observation Build(string ip, int port, Dictionary<string, List<string>> os, Dictionary<string, List<string>> system,
        Dictionary<string, List<string>> bios, List<Dictionary<string, List<string>>> adapters, List<Dictionary<string, List<string>>> programs,
        List<Dictionary<string, List<string>>> services, List<Dictionary<string, List<string>>> tcp)
    {
        static string? One(Dictionary<string, List<string>> d, string name) =>
            d.TryGetValue(name, out var v) && v.Count > 0 && v[0].Length > 0 ? v[0] : null;

        var caption = One(os, "Caption");
        var version = One(os, "Version");
        var osName = caption is null ? null : version is null ? caption : $"{caption} ({version})";
        // ProductType : 1 poste de travail, 2 contrôleur de domaine, 3 serveur.
        var type = One(os, "ProductType") == "1" ? "Poste de travail" : "Serveur";
        var host = One(system, "DNSHostName") ?? One(os, "CSName");
        var domain = One(system, "PartOfDomain") == "true" ? One(system, "Domain") : null;
        var hostname = host is null ? null : (domain is null ? host : $"{host}.{domain}").ToLowerInvariant();
        var adapter = adapters.FirstOrDefault(a => One(a, "IPEnabled") == "true" && a.TryGetValue("IPAddress", out var ips) && ips.Contains(ip))
                      ?? adapters.FirstOrDefault(a => One(a, "IPEnabled") == "true" && One(a, "MACAddress") is not null);
        var vendor = One(system, "Manufacturer");

        var software = programs.Select(p => (Name: One(p, "Name"), Version: One(p, "Version")))
            .Where(p => p.Name is not null).DistinctBy(p => (p.Name, p.Version)).Take(SshCollector.MaxSoftware)
            .Select(p => new SoftwareItem(Cut(p.Name!, 200), p.Version is null ? null : Cut(p.Version, 100))).ToList();
        var running = services.Where(s => One(s, "State") == "Running").Select(s => One(s, "Name")).OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).Take(300).ToList();

        // MSFT_NetTCPConnection.State : 2 Listen, 5 Established.
        (IPAddress Ip, int Port)? End(Dictionary<string, List<string>> c, string address, string portName) =>
            IPAddress.TryParse(One(c, address), out var a) && int.TryParse(One(c, portName), out var p) ? (a, p) : null;
        var listening = tcp.Where(c => One(c, "State") == "2").Select(c => End(c, "LocalAddress", "LocalPort")).OfType<(IPAddress Ip, int Port)>()
            .Where(e => !IPAddress.IsLoopback(e.Ip)).Select(e => e.Port).Distinct().Order().Take(50).ToList();
        var established = tcp.Where(c => One(c, "State") == "5")
            .Select(c => (Local: End(c, "LocalAddress", "LocalPort"), Peer: End(c, "RemoteAddress", "RemotePort")))
            .Where(c => c.Local is not null && c.Peer is not null).Select(c => (c.Local!.Value, c.Peer!.Value));
        var flows = SshInventory.Flows(established, listening, AdminPorts.Append(port).ToArray());

        return new Observation("WinRm", IpAddress: ip, MacAddress: adapter is null ? null : One(adapter, "MACAddress"),
            Hostname: hostname, SerialNumber: Useful(One(bios, "SerialNumber")),
            Vendor: vendor is null ? null : MacVendors.ShortName(vendor), Model: Useful(One(system, "Model")),
            Os: osName is null ? null : Cut(osName, 150), Type: type,
            OpenPorts: listening.Count > 0 ? string.Join(",", listening) : null,
            Software: software, Services: running.Count > 0 ? string.Join("\n", running) : null,
            ListeningPorts: listening.Count > 0 ? string.Join(",", listening) : null, Flows: flows);
    }

    private static readonly string[] Placeholders = ["0", "none", "default string", "to be filled by o.e.m.", "system serial number", "system product name"];
    private static string? Useful(string? v) => v is null || Placeholders.Contains(v.Trim().ToLowerInvariant()) ? null : v.Trim();
    private static string Cut(string v, int max) => v.Length <= max ? v : v[..max];

    private static async Task<bool> IsOpenAsync(IPAddress ip, int port, CancellationToken ct)
    {
        using var probe = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ProbeTimeoutMs);
        try { await probe.ConnectAsync(ip, port, timeout.Token); return true; }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException && !ct.IsCancellationRequested) { return false; }
    }
}
