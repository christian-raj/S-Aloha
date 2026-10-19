using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

/// <summary>
/// Collecteur SNMP v2c (commutateurs, routeurs, pare-feu, NAS, imprimantes) : une requête GET
/// en lecture seule par hôte, avec la communauté du coffre. MIB-II (sysDescr, sysName,
/// sysLocation) et ENTITY-MIB (numéro de série et modèle du châssis, quand l'équipement les
/// publie). Un hôte qui ne répond pas dans le délai est ignoré.
/// </summary>
public class SnmpCollector(ILogger<SnmpCollector> logger) : IDiscoveryCollector
{
    public string ScanType => "Snmp";
    private const int Port = 161, TimeoutMs = 1200;

    public const string SysDescr = "1.3.6.1.2.1.1.1.0";
    public const string SysName = "1.3.6.1.2.1.1.5.0";
    public const string SysLocation = "1.3.6.1.2.1.1.6.0";
    public const string EntSerial = "1.3.6.1.2.1.47.1.1.1.1.11.1";
    public const string EntModel = "1.3.6.1.2.1.47.1.1.1.1.13.1";
    private static readonly string[] MibII = [SysDescr, SysName, SysLocation];
    // Requête séparée : un agent qui ignore l'ENTITY-MIB peut refuser toute la requête.
    private static readonly string[] EntityMib = [EntSerial, EntModel];

    public async Task<List<Observation>> CollectAsync(CollectContext context, CancellationToken ct)
    {
        var hosts = IpRange.Parse(context.Job.TargetRange, out var error) ?? throw new InvalidOperationException(error);
        var community = context.Secret ?? throw new InvalidOperationException("Communauté SNMP absente : choisir un identifiant SnmpV2c du coffre.");
        var results = new List<Observation>();
        using var throttle = new SemaphoreSlim(Math.Max(1, context.MaxConcurrency));
        await Task.WhenAll(hosts.Select(async ip =>
        {
            await throttle.WaitAsync(ct);
            try
            {
                var values = await GetAsync(ip, community, MibII, ct);
                if (values is null) return;
                foreach (var (oid, v) in await GetAsync(ip, community, EntityMib, ct) ?? []) values[oid] = v;
                var (vendor, model, os, type) = Fingerprint.FromSysDescr(values.GetValueOrDefault(SysDescr));
                var seen = new Observation("Snmp", IpAddress: ip.ToString(),
                    Hostname: Blank(values.GetValueOrDefault(SysName))?.ToLowerInvariant(),
                    SerialNumber: Blank(values.GetValueOrDefault(EntSerial)), Vendor: vendor,
                    Model: Blank(values.GetValueOrDefault(EntModel)) ?? model, Os: os, Type: type,
                    Location: Blank(values.GetValueOrDefault(SysLocation)), Raw: values.GetValueOrDefault(SysDescr));
                lock (results) results.Add(seen);
            }
            finally { throttle.Release(); }
        }));
        return results;
    }

    /// <summary>GET SNMP v2c : valeurs texte des OID qui existent, ou null sans réponse.</summary>
    public async Task<Dictionary<string, string>?> GetAsync(IPAddress ip, string community, string[] oids, CancellationToken ct)
    {
        using var udp = new UdpClient(ip.AddressFamily);
        var requestId = Random.Shared.Next(1, int.MaxValue);
        var packet = Ber.GetRequest(community, requestId, oids);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeoutMs);
        try
        {
            await udp.SendAsync(packet, new IPEndPoint(ip, Port), timeout.Token);
            var reply = await udp.ReceiveAsync(timeout.Token);
            return Ber.ParseResponse(reply.Buffer, requestId);
        }
        catch (Exception ex) when (ex is SocketException or FormatException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogDebug("SNMP sans réponse de {Ip} : {Error}", ip, ex.Message);
            return null;
        }
    }

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

/// <summary>Encodage BER minimal d'un GET SNMP v2c et lecture de la réponse (RFC 1905, X.690).</summary>
public static class Ber
{
    private const byte Integer = 0x02, OctetString = 0x04, Null = 0x05, Oid = 0x06, Sequence = 0x30;
    private const byte GetRequestPdu = 0xA0, ResponsePdu = 0xA2;

    public static byte[] GetRequest(string community, int requestId, IEnumerable<string> oids)
    {
        var varbinds = Tlv(Sequence, oids.SelectMany(o => Tlv(Sequence, [.. Tlv(Oid, EncodeOid(o)), .. Tlv(Null, [])])).ToArray());
        var pdu = Tlv(GetRequestPdu, [.. Tlv(Integer, EncodeInt(requestId)), .. Tlv(Integer, [0]), .. Tlv(Integer, [0]), .. varbinds]);
        // Version 1 = SNMP v2c.
        return Tlv(Sequence, [.. Tlv(Integer, [1]), .. Tlv(OctetString, Encoding.ASCII.GetBytes(community)), .. pdu]);
    }

    /// <summary>Valeurs de la réponse, indexées par OID ; les OID absents (noSuchObject…) sont omis.</summary>
    public static Dictionary<string, string> ParseResponse(byte[] data, int requestId)
    {
        var pos = 0;
        var message = Read(data, ref pos, Sequence);
        var inner = 0;
        Read(message, ref inner, Integer);                       // version
        Read(message, ref inner, OctetString);                   // communauté
        var pdu = Read(message, ref inner, ResponsePdu);
        var p = 0;
        if (DecodeInt(Read(pdu, ref p, Integer)) != requestId) throw new FormatException("Réponse SNMP d'une autre requête.");
        if (DecodeInt(Read(pdu, ref p, Integer)) != 0) throw new FormatException("Erreur SNMP signalée par l'agent.");
        Read(pdu, ref p, Integer);                               // error-index
        var list = Read(pdu, ref p, Sequence);
        var values = new Dictionary<string, string>();
        var l = 0;
        while (l < list.Length)
        {
            var bind = Read(list, ref l, Sequence);
            var b = 0;
            var oid = DecodeOid(Read(bind, ref b, Oid));
            var (tag, value) = ReadAny(bind, ref b);
            var text = tag switch
            {
                OctetString => Printable(value),
                Integer or 0x41 or 0x42 or 0x43 => DecodeInt(value).ToString(),
                Oid => DecodeOid(value),
                _ => null                                        // noSuchObject (0x80), noSuchInstance (0x81)…
            };
            if (text is not null) values[oid] = text;
        }
        return values;
    }

    private static string Printable(byte[] v) =>
        new(Encoding.UTF8.GetString(v).Where(ch => !char.IsControl(ch) || ch is '\n' or '\t').ToArray());

    private static byte[] Tlv(byte tag, byte[] value) => [tag, .. EncodeLength(value.Length), .. value];

    private static byte[] EncodeLength(int n) => n < 0x80 ? [(byte)n]
        : n <= 0xFF ? [0x81, (byte)n] : [0x82, (byte)(n >> 8), (byte)n];

    private static byte[] EncodeInt(int v)
    {
        var bytes = BitConverter.GetBytes(v).Reverse().ToList();
        while (bytes.Count > 1 && (bytes[0] == 0 && bytes[1] < 0x80 || bytes[0] == 0xFF && bytes[1] >= 0x80)) bytes.RemoveAt(0);
        return [.. bytes];
    }

    private static long DecodeInt(byte[] v)
    {
        long n = v.Length > 0 && v[0] >= 0x80 ? -1 : 0;
        foreach (var b in v) n = (n << 8) | b;
        return n;
    }

    public static byte[] EncodeOid(string oid)
    {
        var arcs = oid.Split('.').Select(uint.Parse).ToArray();
        var bytes = new List<byte> { (byte)(arcs[0] * 40 + arcs[1]) };
        foreach (var arc in arcs.Skip(2))
        {
            var chunk = new Stack<byte>();
            var v = arc;
            chunk.Push((byte)(v & 0x7F));
            while ((v >>= 7) > 0) chunk.Push((byte)(0x80 | (v & 0x7F)));
            bytes.AddRange(chunk);
        }
        return [.. bytes];
    }

    public static string DecodeOid(byte[] v)
    {
        if (v.Length == 0) return "";
        var arcs = new List<ulong> { (ulong)(v[0] / 40), (ulong)(v[0] % 40) };
        ulong cur = 0;
        foreach (var b in v.Skip(1))
        {
            cur = (cur << 7) | (byte)(b & 0x7F);
            if ((b & 0x80) == 0) { arcs.Add(cur); cur = 0; }
        }
        return string.Join('.', arcs);
    }

    private static byte[] Read(byte[] data, ref int pos, byte expected)
    {
        var (tag, value) = ReadAny(data, ref pos);
        if (tag != expected) throw new FormatException($"Réponse SNMP inattendue (étiquette 0x{tag:X2}).");
        return value;
    }

    private static (byte Tag, byte[] Value) ReadAny(byte[] data, ref int pos)
    {
        if (pos + 2 > data.Length) throw new FormatException("Réponse SNMP tronquée.");
        var tag = data[pos++];
        int len = data[pos++];
        if (len >= 0x80)
        {
            var count = len & 0x7F;
            if (count is 0 or > 3 || pos + count > data.Length) throw new FormatException("Longueur BER invalide.");
            len = 0;
            for (var i = 0; i < count; i++) len = (len << 8) | data[pos++];
        }
        if (pos + len > data.Length) throw new FormatException("Réponse SNMP tronquée.");
        var value = data[pos..(pos + len)];
        pos += len;
        return (tag, value);
    }
}
