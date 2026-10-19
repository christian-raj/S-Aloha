using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;

namespace SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

/// <summary>Une interface réseau lue dans l'<c>ifTable</c> (IF-MIB, CFG-51).</summary>
public record SnmpInterface(int Index, string Name, string Kind, bool Up, long? SpeedMbps, string? Mac)
{
    /// <summary>Une ligne lisible : « Gi0/1 · ethernet · active · 1000 Mb/s · 00:1A:2B:3C:4D:5E ».</summary>
    public override string ToString() => string.Join(" · ", new[]
    {
        Name, Kind, Up ? "active" : "inactive", SpeedMbps is { } s ? $"{s} Mb/s" : null, Mac
    }.Where(x => x is not null));
}

/// <summary>
/// Accès SNMP d'une tâche : communauté (v2c) ou utilisateur USM (v3, RFC 3414) avec ses
/// protocoles d'authentification et de chiffrement. La cryptographie USM est celle de la
/// bibliothèque SharpSnmpLib (licence MIT), pas réécrite ici.
/// </summary>
public record SnmpAccess(VersionCode Version, OctetString Community, OctetString User, IPrivacyProvider Privacy)
{
    // MD5 et DES, cassés, ne sont pas proposés ; SHA-1 reste accepté (HMAC-SHA-1, encore seul
    // proposé par bien des équipements anciens) mais n'est pas le défaut.
    public static readonly string[] AuthProtocols = ["SHA256", "SHA384", "SHA512", "SHA1"];
    public static readonly string[] PrivProtocols = ["AES", "AES192", "AES256", "None"];

    public static SnmpAccess V2c(string community) =>
        new(VersionCode.V2, new OctetString(community), OctetString.Empty, DefaultPrivacyProvider.DefaultPair);

    public static SnmpAccess V3(string user, string authProtocol, string authPassphrase, string privProtocol, string? privPassphrase)
    {
        var phrase = new OctetString(authPassphrase);
        IAuthenticationProvider auth = authProtocol switch
        {
#pragma warning disable CS0618 // SHA-1 : voir AuthProtocols.
            "SHA1" => new SHA1AuthenticationProvider(phrase),
#pragma warning restore CS0618
            "SHA256" => new SHA256AuthenticationProvider(phrase),
            "SHA384" => new SHA384AuthenticationProvider(phrase),
            "SHA512" => new SHA512AuthenticationProvider(phrase),
            _ => throw new InvalidOperationException($"Protocole d'authentification SNMP v3 inconnu : {authProtocol}.")
        };
        var secret = new OctetString(privPassphrase ?? "");
        IPrivacyProvider privacy = privProtocol switch
        {
            "AES" => new AESPrivacyProvider(secret, auth),
            "AES192" => new AES192PrivacyProvider(secret, auth),
            "AES256" => new AES256PrivacyProvider(secret, auth),
            "None" => new DefaultPrivacyProvider(auth),
            _ => throw new InvalidOperationException($"Protocole de chiffrement SNMP v3 inconnu : {privProtocol}.")
        };
        return new(VersionCode.V3, OctetString.Empty, new OctetString(user), privacy);
    }

    /// <summary>Accès d'un identifiant du coffre, avec son secret déchiffré.</summary>
    public static SnmpAccess From(DiscoveryCredential? credential, string? secret)
    {
        if (credential is null || string.IsNullOrEmpty(secret))
            throw new InvalidOperationException("Identifiant SNMP absent : choisir une communauté v2c ou un utilisateur v3 du coffre.");
        if (credential.CredentialType == "SnmpV2c") return V2c(secret);
        var s = SnmpV3Secret.Parse(secret);
        return V3(credential.Username ?? "", credential.AuthProtocol ?? "SHA256", s.Auth, credential.PrivProtocol ?? "AES", s.Priv);
    }
}

/// <summary>Les deux phrases secrètes d'un utilisateur SNMP v3, chiffrées ensemble au coffre (CFG-45).</summary>
public record SnmpV3Secret(string Auth, string? Priv)
{
    public string Serialize() => JsonSerializer.Serialize(this);
    public static SnmpV3Secret Parse(string stored) =>
        JsonSerializer.Deserialize<SnmpV3Secret>(stored) ?? throw new InvalidOperationException("Secret SNMP v3 illisible.");
}

/// <summary>
/// Collecteur SNMP v2c et v3 (commutateurs, routeurs, pare-feu, NAS, imprimantes), en lecture
/// seule : MIB-II (sysDescr, sysName, sysLocation), ENTITY-MIB (numéro de série et modèle du
/// châssis, quand l'équipement les publie) et table des interfaces (IF-MIB, CFG-51). Un hôte
/// qui ne répond pas dans le délai est ignoré.
/// </summary>
public class SnmpCollector(ILogger<SnmpCollector> logger) : IDiscoveryCollector
{
    public string ScanType => "Snmp";
    private const int Port = 161, TimeoutMs = 1200, MaxInterfaces = 128;
    public const int MaxRepetitions = 16;

    public const string SysDescr = "1.3.6.1.2.1.1.1.0";
    public const string SysName = "1.3.6.1.2.1.1.5.0";
    public const string SysLocation = "1.3.6.1.2.1.1.6.0";
    public const string EntSerial = "1.3.6.1.2.1.47.1.1.1.1.11.1";
    public const string EntModel = "1.3.6.1.2.1.47.1.1.1.1.13.1";
    private static readonly string[] MibII = [SysDescr, SysName, SysLocation];
    // Requête séparée : un agent qui ignore l'ENTITY-MIB peut refuser toute la requête.
    private static readonly string[] EntityMib = [EntSerial, EntModel];
    // ifTable : ifDescr, ifType, ifSpeed, ifPhysAddress, ifOperStatus.
    public const string IfDescr = "1.3.6.1.2.1.2.2.1.2", IfType = "1.3.6.1.2.1.2.2.1.3", IfSpeed = "1.3.6.1.2.1.2.2.1.5",
        IfPhysAddress = "1.3.6.1.2.1.2.2.1.6", IfOperStatus = "1.3.6.1.2.1.2.2.1.8";

    /// <summary>Port de l'agent : 161, sauf pour les tests (agent sur un port non privilégié).</summary>
    public int AgentPort { get; init; } = Port;

    public async Task<List<Observation>> CollectAsync(CollectContext context, CancellationToken ct)
    {
        var hosts = IpRange.Parse(context.Job.TargetRange, out var error) ?? throw new InvalidOperationException(error);
        var access = SnmpAccess.From(context.Credential, context.Secret);
        var results = new List<Observation>();
        using var throttle = new SemaphoreSlim(Math.Max(1, context.MaxConcurrency));
        await Task.WhenAll(hosts.Select(async ip =>
        {
            await throttle.WaitAsync(ct);
            try
            {
                if (await ReadAsync(new IPEndPoint(ip, AgentPort), access, ct) is { } seen) lock (results) results.Add(seen);
            }
            finally { throttle.Release(); }
        }));
        return results;
    }

    /// <summary>Ce qu'un agent publie de l'équipement, ou null sans réponse (ou accès refusé).</summary>
    public async Task<Observation?> ReadAsync(IPEndPoint agent, SnmpAccess access, CancellationToken ct)
    {
        try
        {
            var session = await SnmpSession.OpenAsync(agent, access, TimeoutMs, ct);
            var values = await session.GetAsync(MibII, ct);
            if (values.Count == 0) return null;
            foreach (var (oid, v) in await session.TryGetAsync(EntityMib, ct)) values[oid] = v;
            var interfaces = await InterfacesAsync(session, ct);
            var (vendor, model, os, type) = Fingerprint.FromSysDescr(values.GetValueOrDefault(SysDescr));
            return new Observation("Snmp", IpAddress: agent.Address.ToString(),
                MacAddress: interfaces.FirstOrDefault(i => i.Kind == "ethernet" && MacVendors.PrefixOf(i.Mac) is not null)?.Mac,
                Hostname: Blank(values.GetValueOrDefault(SysName))?.ToLowerInvariant(),
                SerialNumber: Blank(values.GetValueOrDefault(EntSerial)), Vendor: vendor,
                Model: Blank(values.GetValueOrDefault(EntModel)) ?? model, Os: os, Type: type,
                Location: Blank(values.GetValueOrDefault(SysLocation)), Raw: values.GetValueOrDefault(SysDescr),
                Interfaces: interfaces.Count > 0 ? string.Join("\n", interfaces) : null);
        }
        catch (Exception ex) when (ex is SocketException or SnmpException or DecryptionException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogDebug("SNMP sans réponse de {Agent} : {Error}", agent, ex.Message);
            return null;
        }
    }

    /// <summary>CFG-51 : interfaces de l'<c>ifTable</c>, boucle locale exclue, 128 au plus.</summary>
    private static async Task<List<SnmpInterface>> InterfacesAsync(SnmpSession session, CancellationToken ct)
    {
        var names = await session.WalkAsync(IfDescr, MaxInterfaces, ct);
        if (names.Count == 0) return [];
        var types = await session.WalkAsync(IfType, MaxInterfaces, ct);
        var speeds = await session.WalkAsync(IfSpeed, MaxInterfaces, ct);
        var macs = await session.WalkAsync(IfPhysAddress, MaxInterfaces, ct);
        var status = await session.WalkAsync(IfOperStatus, MaxInterfaces, ct);
        return names.Select(n =>
            {
                var kind = Kind(types.GetValueOrDefault(n.Key)?.ToString());
                var speed = long.TryParse(speeds.GetValueOrDefault(n.Key)?.ToString(), out var bps) && bps > 0 ? bps / 1_000_000 : (long?)null;
                return new SnmpInterface(n.Key, Banner.Clean(n.Value.ToString()) ?? $"if{n.Key}", kind,
                    status.GetValueOrDefault(n.Key)?.ToString() == "1", speed, Mac(macs.GetValueOrDefault(n.Key)));
            })
            .Where(i => i.Kind != "boucle locale").ToList();
    }

    /// <summary>Nature d'une interface d'après son <c>ifType</c> (IANAifType-MIB).</summary>
    public static string Kind(string? ifType) => ifType switch
    {
        "6" or "62" or "117" => "ethernet",
        "24" => "boucle locale",
        "71" => "wifi",
        "135" => "vlan",
        "161" => "agrégat",
        "131" => "tunnel",
        "53" => "virtuelle",
        _ => "autre"
    };

    /// <summary><c>ifPhysAddress</c> (6 octets) en adresse MAC normalisée, ou null.</summary>
    public static string? Mac(ISnmpData? data)
    {
        if (data is not OctetString s) return null;
        var bytes = s.GetRaw();
        if (bytes.Length != 6 || bytes.All(b => b == 0)) return null;
        return string.Join(":", bytes.Select(b => b.ToString("X2")));
    }

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

/// <summary>
/// Échanges SNMP avec un agent : la bibliothèque code et décode les messages (et la sécurité
/// USM), l'envoi et l'attente de la réponse se font ici, bornés par un délai et annulables.
/// En v3, une découverte préalable obtient l'identifiant et l'horloge du moteur de l'agent.
/// </summary>
public class SnmpSession
{
    private readonly IPEndPoint _agent;
    private readonly SnmpAccess _access;
    private readonly int _timeoutMs;
    private readonly UserRegistry _users;
    private ISnmpMessage? _report;

    private SnmpSession(IPEndPoint agent, SnmpAccess access, int timeoutMs)
    {
        _agent = agent; _access = access; _timeoutMs = timeoutMs;
        _users = access.Version == VersionCode.V3 ? new UserRegistry([new User(access.User, access.Privacy)]) : new UserRegistry();
    }

    public static async Task<SnmpSession> OpenAsync(IPEndPoint agent, SnmpAccess access, int timeoutMs, CancellationToken ct)
    {
        var session = new SnmpSession(agent, access, timeoutMs);
        if (access.Version == VersionCode.V3)
            session._report = await session.ExchangeAsync(Messenger.GetNextDiscovery(SnmpType.GetRequestPdu).ToBytes(), null, new UserRegistry(), ct);
        return session;
    }

    /// <summary>GET : valeurs texte des OID qui existent ; une erreur de l'agent lève <see cref="SnmpException"/>.</summary>
    public async Task<Dictionary<string, string>> GetAsync(IEnumerable<string> oids, CancellationToken ct)
    {
        var variables = oids.Select(o => new Variable(new ObjectIdentifier(o))).ToList();
        ISnmpMessage request = _access.Version == VersionCode.V3
            ? new GetRequestMessage(VersionCode.V3, Messenger.NextMessageId, Messenger.NextRequestId, _access.User, OctetString.Empty,
                variables, _access.Privacy, Messenger.MaxMessageSize, _report!)
            : new GetRequestMessage(Messenger.NextRequestId, _access.Version, _access.Community, variables);
        var values = new Dictionary<string, string>();
        foreach (var v in Values(await ExchangeAsync(request, ct)))
            values[v.Id.ToString()] = v.Data is OctetString s ? Printable(s) : v.Data.ToString();
        return values;
    }

    /// <summary>GET dont un refus de l'agent (OID inconnu d'un ancien agent v2c) donne une réponse vide.</summary>
    public async Task<Dictionary<string, string>> TryGetAsync(IEnumerable<string> oids, CancellationToken ct)
    {
        try { return await GetAsync(oids, ct); }
        catch (SnmpException) { return []; }
    }

    /// <summary>Parcours d'une colonne par GETBULK : index de ligne → valeur, <paramref name="max"/> lignes au plus.</summary>
    public async Task<Dictionary<int, ISnmpData>> WalkAsync(string column, int max, CancellationToken ct)
    {
        var root = new ObjectIdentifier(column);
        var rows = new Dictionary<int, ISnmpData>();
        var next = root;
        while (rows.Count < max)
        {
            var variables = new List<Variable> { new(next) };
            ISnmpMessage request = _access.Version == VersionCode.V3
                ? new GetBulkRequestMessage(VersionCode.V3, Messenger.NextMessageId, Messenger.NextRequestId, _access.User, OctetString.Empty,
                    0, SnmpCollector.MaxRepetitions, variables, _access.Privacy, Messenger.MaxMessageSize, _report!)
                : new GetBulkRequestMessage(Messenger.NextRequestId, _access.Version, _access.Community, 0, SnmpCollector.MaxRepetitions, variables);
            var page = Values(await ExchangeAsync(request, ct)).ToList();
            var inside = page.TakeWhile(v => v.Id.ToNumerical().Length == root.ToNumerical().Length + 1
                && v.Id.ToNumerical().Take(root.ToNumerical().Length).SequenceEqual(root.ToNumerical())).ToList();
            foreach (var v in inside.Take(max - rows.Count)) rows[(int)v.Id.ToNumerical()[^1]] = v.Data;
            if (inside.Count == 0 || inside.Count < page.Count) break;
            next = inside[^1].Id;
        }
        return rows;
    }

    /// <summary>Variables d'une réponse, sans les valeurs absentes (noSuchObject, endOfMibView…).</summary>
    private static IEnumerable<Variable> Values(ISnmpMessage response)
    {
        if (response.TypeCode() == SnmpType.ReportPdu)
            throw new SnmpException("Accès SNMP v3 refusé par l'agent (utilisateur, phrase secrète ou protocole).");
        var pdu = response.Pdu();
        if (pdu.ErrorStatus.ToInt32() != 0) throw new SnmpException($"Erreur SNMP signalée par l'agent ({(ErrorCode)pdu.ErrorStatus.ToInt32()}).");
        return pdu.Variables.Where(v => v.Data.TypeCode is not (SnmpType.NoSuchObject or SnmpType.NoSuchInstance or SnmpType.EndOfMibView));
    }

    private Task<ISnmpMessage> ExchangeAsync(ISnmpMessage request, CancellationToken ct) =>
        ExchangeAsync(request.ToBytes(), request.RequestId(), _users, ct);

    /// <summary>Envoi et réponse ; sans <paramref name="requestId"/> (découverte v3), la première réponse.</summary>
    private async Task<ISnmpMessage> ExchangeAsync(byte[] request, int? requestId, UserRegistry users, CancellationToken ct)
    {
        using var udp = new UdpClient(_agent.AddressFamily);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_timeoutMs);
        await udp.SendAsync(request, _agent, timeout.Token);
        while (true)
        {
            var reply = await udp.ReceiveAsync(timeout.Token);
            var message = MessageFactory.ParseMessages(reply.Buffer, users).FirstOrDefault();
            // Une réponse tardive à une autre requête est ignorée.
            if (message is not null && (requestId is null || message.RequestId() == requestId)) return message;
        }
    }

    private static string Printable(OctetString s) =>
        new(Encoding.UTF8.GetString(s.GetRaw()).Where(ch => !char.IsControl(ch) || ch is '\n' or '\t').ToArray());
}
