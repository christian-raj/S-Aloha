using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

/// <summary>
/// Client HTTPS d'une API d'hyperviseur (CFG-54). Le certificat est vérifié par l'autorité de
/// certification ; s'il ne l'est pas (certificat autosigné, courant sur Proxmox VE et vCenter),
/// son empreinte est retenue au premier contact, comme une clé d'hôte SSH (CFG-53) : un
/// certificat différent ensuite coupe la connexion avant l'envoi de l'identifiant.
/// </summary>
public sealed class HypervisorApi : IDisposable
{
    private const int TimeoutSeconds = 30;
    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, SshHostKey> _known;
    public Uri BaseUri { get; }
    /// <summary>Certificat changé : la connexion a été refusée.</summary>
    public bool CertificateRefused { get; private set; }

    private HypervisorApi(Uri baseUri, int perimeter, ConcurrentDictionary<string, SshHostKey> known)
    {
        BaseUri = baseUri;
        _known = known;
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(5),
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
                    errors == SslPolicyErrors.None || certificate is not null && TrustUnverified(perimeter, certificate)
            }
        };
        _http = new HttpClient(handler) { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(TimeoutSeconds) };
    }

    /// <summary>Empreinte SHA-256 (base64) d'un certificat.</summary>
    public static string Fingerprint(X509Certificate certificate) => Convert.ToBase64String(SHA256.HashData(certificate.GetRawCertData()));

    private bool TrustUnverified(int perimeter, X509Certificate certificate)
    {
        var trusted = SshCollector.Trust(_known, perimeter, BaseUri.Host, BaseUri.Port, "x509", Fingerprint(certificate), SshHostKey.Tls);
        if (!trusted) CertificateRefused = true;
        return trusted;
    }

    /// <summary>URL d'API valide (https, hôte, port facultatif) ou message d'erreur.</summary>
    public static string? Validate(string? target) =>
        Uri.TryCreate(target?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && uri.Host.Length > 0
            && uri.AbsolutePath is "/" or "" && string.IsNullOrEmpty(uri.Query)
            ? null
            : $"Adresse d'API invalide : « {target} » (attendu : https://hôte[:port], ex. https://pve.corp.local:8006).";

    public static async Task<HypervisorApi> OpenAsync(AppDbContext db, int perimeter, string target, CancellationToken ct)
    {
        var uri = new Uri(target.Trim().TrimEnd('/') + "/");
        var known = new ConcurrentDictionary<string, SshHostKey>(await db.SshHostKeys
            .Where(k => k.PerimeterId == perimeter && k.Protocol == SshHostKey.Tls && k.Host == uri.Host && k.Port == uri.Port)
            .ToDictionaryAsync(k => k.Host, ct));
        return new HypervisorApi(uri, perimeter, known);
    }

    /// <summary>Certificats nouveaux ou changés : enregistrés après la collecte.</summary>
    public async Task SaveTrustAsync(AppDbContext db, CancellationToken ct)
    {
        foreach (var key in _known.Values.Where(k => k.Id == 0)) db.SshHostKeys.Add(key);
        await db.SaveChangesAsync(ct);
    }

    public async Task<JsonElement> SendAsync(HttpMethod method, string path, Action<HttpRequestHeaders>? headers, CancellationToken ct,
        bool optional = false)
    {
        using var request = new HttpRequestMessage(method, path.TrimStart('/'));
        headers?.Invoke(request.Headers);
        HttpResponseMessage response;
        try { response = await _http.SendAsync(request, ct); }
        catch (HttpRequestException ex) when (CertificateRefused)
        {
            throw new InvalidOperationException(
                $"Certificat de {BaseUri.Host} changé depuis le premier contact : connexion refusée jusqu'à acceptation par l'Admin.", ex);
        }
        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new InvalidOperationException($"Accès refusé par l'API de {BaseUri.Host} ({(int)response.StatusCode}) : vérifier l'identifiant du coffre.");
            if (!response.IsSuccessStatusCode)
            {
                if (optional) return default;
                throw new InvalidOperationException($"API de {BaseUri.Host} : {method} {path} a répondu {(int)response.StatusCode}.");
            }
            var body = await response.Content.ReadAsStringAsync(ct);
            return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
        }
    }

    public void Dispose() => _http.Dispose();

    public static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static long? Num(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : null;

    public static IEnumerable<JsonElement> Items(JsonElement e) => e.ValueKind == JsonValueKind.Array ? e.EnumerateArray() : [];
}

/// <summary>
/// Collecteur Proxmox VE (CFG-54) : nœuds de la grappe et machines virtuelles (QEMU) et
/// conteneurs (LXC), en lecture seule avec un jeton d'API (rôle PVEAuditor suffisant). Chaque
/// machine porte son nœud : la relation « Héberge » est proposée au gestionnaire (CFG-55).
/// </summary>
public class ProxmoxCollector(AppDbContext db) : IDiscoveryCollector
{
    public string ScanType => "Proxmox";
    public const int MaxMachines = 1000;

    public async Task<List<Observation>> CollectAsync(CollectContext context, CancellationToken ct)
    {
        var token = $"PVEAPIToken={context.Credential?.Username}={context.Secret}";
        using var api = await HypervisorApi.OpenAsync(db, context.Job.PerimeterId, context.Job.TargetRange, ct);
        try
        {
            void Auth(HttpRequestHeaders h) => h.TryAddWithoutValidation("Authorization", token);
            var version = HypervisorApi.Str(Data(await api.SendAsync(HttpMethod.Get, "api2/json/version", Auth, ct)), "version");
            var results = new List<Observation>();
            foreach (var node in HypervisorApi.Items(Data(await api.SendAsync(HttpMethod.Get, "api2/json/cluster/status", Auth, ct)))
                         .Where(n => HypervisorApi.Str(n, "type") == "node"))
                results.Add(new Observation("Proxmox", IpAddress: HypervisorApi.Str(node, "ip"), Hostname: HypervisorApi.Str(node, "name")?.ToLowerInvariant(),
                    Os: version is null ? "Proxmox VE" : $"Proxmox VE {version}", Type: "Serveur"));

            foreach (var vm in HypervisorApi.Items(Data(await api.SendAsync(HttpMethod.Get, "api2/json/cluster/resources?type=vm", Auth, ct))).Take(MaxMachines))
            {
                var (node, kind, id, name) = (HypervisorApi.Str(vm, "node"), HypervisorApi.Str(vm, "type"), HypervisorApi.Num(vm, "vmid"),
                    HypervisorApi.Str(vm, "name"));
                if (node is null || kind is not ("qemu" or "lxc") || id is null) continue;
                var config = Data(await api.SendAsync(HttpMethod.Get, $"api2/json/nodes/{Uri.EscapeDataString(node)}/{kind}/{id}/config", Auth, ct, optional: true));
                results.Add(new Observation("Proxmox", MacAddress: MacOf(config), Hostname: name?.ToLowerInvariant(),
                    Os: OsType(HypervisorApi.Str(config, "ostype")), Type: "Serveur",
                    Raw: $"Proxmox : {(kind == "qemu" ? "VM" : "conteneur")} {id} ({HypervisorApi.Str(vm, "status")}) sur le nœud {node}",
                    HostedBy: node.ToLowerInvariant(), HostedReason: $"Proxmox VE : {(kind == "qemu" ? "VM" : "conteneur")} {id} sur le nœud {node}"));
            }
            return results;
        }
        finally { await api.SaveTrustAsync(db, CancellationToken.None); }
    }

    private static JsonElement Data(JsonElement e) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("data", out var d) ? d : default;

    /// <summary>Adresse MAC de la première carte (QEMU « virtio=BC:24:11:…,bridge=… », LXC « hwaddr=… »).</summary>
    public static string? MacOf(JsonElement config)
    {
        for (var i = 0; i < 8; i++)
        {
            if (HypervisorApi.Str(config, "net" + i) is not { } net) continue;
            foreach (var part in net.Split(','))
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2 && CiRules.NormalizeMac(kv[1]) is { } mac) return mac;
            }
        }
        return null;
    }

    /// <summary>Système d'après <c>ostype</c> (l26, win11, w2k22… ; debian, ubuntu… pour LXC).</summary>
    public static string? OsType(string? ostype) => ostype switch
    {
        null or "" => null,
        "l24" or "l26" => "Linux",
        "win11" => "Microsoft Windows 11 / Server 2022+",
        "win10" => "Microsoft Windows 10 / Server 2016-2019",
        "win8" => "Microsoft Windows 8 / Server 2012",
        "win7" => "Microsoft Windows 7 / Server 2008 R2",
        "wxp" or "w2k" or "w2k3" or "w2k8" or "wvista" => "Microsoft Windows (ancien)",
        "solaris" => "Solaris",
        "other" or "unmanaged" => null,
        _ => char.ToUpperInvariant(ostype[0]) + ostype[1..]
    };
}

/// <summary>
/// Collecteur VMware vCenter (CFG-54), par l'API vSphere Automation (vCenter 7 et suivants) :
/// hôtes ESXi et machines virtuelles de chaque hôte, en lecture seule (rôle Lecture seule
/// suffisant). Chaque VM porte son hôte : la relation « Héberge » est proposée (CFG-55).
/// </summary>
public class VCenterCollector(AppDbContext db) : IDiscoveryCollector
{
    public string ScanType => "VCenter";

    public async Task<List<Observation>> CollectAsync(CollectContext context, CancellationToken ct)
    {
        using var api = await HypervisorApi.OpenAsync(db, context.Job.PerimeterId, context.Job.TargetRange, ct);
        string? session = null;
        try
        {
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{context.Credential?.Username}:{context.Secret}"));
            session = (await api.SendAsync(HttpMethod.Post, "api/session", h => h.Authorization = new AuthenticationHeaderValue("Basic", basic), ct)).GetString();
            void Auth(HttpRequestHeaders h) => h.TryAddWithoutValidation("vmware-api-session-id", session);

            var results = new List<Observation>();
            var machines = 0;
            foreach (var host in HypervisorApi.Items(await api.SendAsync(HttpMethod.Get, "api/vcenter/host", Auth, ct)))
            {
                var (hostId, hostName) = (HypervisorApi.Str(host, "host"), HypervisorApi.Str(host, "name")?.ToLowerInvariant());
                if (hostId is null || hostName is null) continue;
                var hostIp = IPAddress.TryParse(hostName, out _) ? hostName : null;
                results.Add(new Observation("VCenter", IpAddress: hostIp, Hostname: hostIp is null ? hostName : null,
                    Vendor: "VMware", Os: "VMware ESXi", Type: "Serveur"));
                foreach (var vm in HypervisorApi.Items(await api.SendAsync(HttpMethod.Get, $"api/vcenter/vm?hosts={Uri.EscapeDataString(hostId)}", Auth, ct)))
                {
                    if (++machines > ProxmoxCollector.MaxMachines || HypervisorApi.Str(vm, "vm") is not { } vmId) continue;
                    var detail = await api.SendAsync(HttpMethod.Get, $"api/vcenter/vm/{Uri.EscapeDataString(vmId)}", Auth, ct, optional: true);
                    var identity = await api.SendAsync(HttpMethod.Get, $"api/vcenter/vm/{Uri.EscapeDataString(vmId)}/guest/identity", Auth, ct, optional: true);
                    var name = HypervisorApi.Str(identity, "host_name") ?? HypervisorApi.Str(vm, "name");
                    results.Add(new Observation("VCenter", IpAddress: HypervisorApi.Str(identity, "ip_address"), MacAddress: MacOf(detail),
                        Hostname: name?.ToLowerInvariant(), Os: GuestOs(identity, HypervisorApi.Str(detail, "guest_OS")), Type: "Serveur",
                        Raw: $"vCenter : VM {vmId} ({HypervisorApi.Str(vm, "power_state")}) sur l'hôte {hostName}",
                        HostedBy: hostIp ?? hostName, HostedReason: $"vCenter : VM {vmId} sur l'hôte {hostName}"));
                }
            }
            return results;
        }
        finally
        {
            if (session is not null)
                try { await api.SendAsync(HttpMethod.Delete, "api/session", h => h.TryAddWithoutValidation("vmware-api-session-id", session), CancellationToken.None, optional: true); }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException) { /* session expirée d'elle-même */ }
            await api.SaveTrustAsync(db, CancellationToken.None);
        }
    }

    /// <summary>MAC de la première carte : <c>nics</c> est un objet (clé → carte) dans l'API /api.</summary>
    public static string? MacOf(JsonElement detail)
    {
        if (detail.ValueKind != JsonValueKind.Object || !detail.TryGetProperty("nics", out var nics)) return null;
        var cards = nics.ValueKind == JsonValueKind.Object ? nics.EnumerateObject().Select(p => p.Value)
            : HypervisorApi.Items(nics).Select(n => n.TryGetProperty("value", out var v) ? v : n);
        return cards.Select(c => CiRules.NormalizeMac(HypervisorApi.Str(c, "mac_address"))).FirstOrDefault(m => m is not null);
    }

    /// <summary>Système déclaré par les VMware Tools, sinon le type d'invité de la VM (« UBUNTU_64 »).</summary>
    public static string? GuestOs(JsonElement identity, string? guestType)
    {
        if (identity.ValueKind == JsonValueKind.Object && identity.TryGetProperty("full_name", out var full)
            && HypervisorApi.Str(full, "default_message") is { Length: > 0 } text) return text.Length > 150 ? text[..150] : text;
        if (string.IsNullOrEmpty(guestType)) return null;
        var t = guestType.Replace("_64", "").Replace('_', ' ').ToLowerInvariant();
        return t.StartsWith("windows") ? "Microsoft " + char.ToUpperInvariant(t[0]) + t[1..] : char.ToUpperInvariant(t[0]) + t[1..];
    }
}
