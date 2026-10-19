using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SAloha.Api.Modules.ServiceConfigurationManagement;
using SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

namespace SAloha.Api.Tests;

/// <summary>Briques de la découverte sans base ni réseau : plages, fenêtres, SNMP, coffre, empreintes.</summary>
public class DiscoveryUnitTests
{
    [Fact]
    public void CFG_46_plage_de_24_au_plus_large_sans_reseau_ni_diffusion()
    {
        Assert.Equal(254, IpRange.Parse("192.168.10.0/24", out _)!.Count);
        Assert.Equal("192.168.10.1", IpRange.Parse("192.168.10.77/24", out _)![0].ToString());
        Assert.Equal(2, IpRange.Parse("10.0.0.4/31", out _)!.Count);
        Assert.Single(IpRange.Parse("10.0.0.4", out _)!);
        Assert.Null(IpRange.Parse("10.0.0.0/23", out var wide));
        Assert.Contains("/24", wide);
        Assert.NotNull(IpRange.Validate("fe80::1"));
        Assert.Null(IpRange.Ports("22,80,99999", out var port));
        Assert.Contains("99999", port);
        Assert.Equal([22, 80], IpRange.Ports("22, 80,22", out _)!);
    }

    [Theory]
    [InlineData("20:00", "06:00", "23:30", true)]
    [InlineData("20:00", "06:00", "05:59", true)]
    [InlineData("20:00", "06:00", "12:00", false)]
    [InlineData("08:00", "12:00", "09:00", true)]
    [InlineData(null, null, "12:00", true)]
    public void CFG_46_fenetre_de_scan_y_compris_a_cheval_sur_minuit(string? start, string? end, string now, bool inside) =>
        Assert.Equal(inside, ScanWindow.Contains(start is null ? null : TimeOnly.Parse(start), end is null ? null : TimeOnly.Parse(end), TimeOnly.Parse(now)));

    [Fact]
    public void CFG_46_une_tache_est_echue_selon_sa_frequence_et_jamais_en_cours()
    {
        var now = new DateTime(2026, 10, 19, 22, 0, 0, DateTimeKind.Utc);
        var job = new DiscoveryJob { FrequencyHours = 24, LastRunAt = now.AddHours(-25), LastStatus = DiscoveryJob.Succeeded };
        Assert.True(ScanWindow.IsDue(job, now, TimeZoneInfo.Utc));
        Assert.False(ScanWindow.IsDue(new DiscoveryJob { FrequencyHours = 24, LastRunAt = now.AddHours(-2) }, now, TimeZoneInfo.Utc));
        Assert.False(ScanWindow.IsDue(new DiscoveryJob { FrequencyHours = 0 }, now, TimeZoneInfo.Utc));
        Assert.False(ScanWindow.IsDue(new DiscoveryJob { LastStatus = DiscoveryJob.Running }, now, TimeZoneInfo.Utc));
        Assert.False(ScanWindow.IsDue(new DiscoveryJob(), now.Date.AddHours(12), TimeZoneInfo.Utc));   // hors de 20:00–06:00
    }

    private static byte[] Tlv(byte tag, params byte[][] parts)
    {
        var value = parts.SelectMany(p => p).ToArray();
        return [tag, (byte)value.Length, .. value];
    }

    [Fact]
    public void SNMP_requete_GET_v2c_et_lecture_de_la_reponse()
    {
        var request = Ber.GetRequest("public", 1234, [SnmpCollector.SysName]);
        Assert.Equal(0x30, request[0]);
        Assert.Contains("public", Encoding.ASCII.GetString(request));
        // En-tête (2) + version v2c (3) + communauté (2 + 6) : vient le PDU GetRequest.
        Assert.Equal(0xA0, request[13]);
        Assert.Equal("1.3.6.1.2.1.47.1.1.1.1.11.1", Ber.DecodeOid(Ber.EncodeOid(SnmpCollector.EntSerial)));

        // Réponse : sysName = "sw-coeur", sysLocation absent (noSuchObject).
        var response = Tlv(0x30, Tlv(0x02, [1]), Tlv(0x04, Encoding.ASCII.GetBytes("public")),
            Tlv(0xA2, Tlv(0x02, [0x04, 0xD2]), Tlv(0x02, [0]), Tlv(0x02, [0]),
                Tlv(0x30,
                    Tlv(0x30, Tlv(0x06, Ber.EncodeOid(SnmpCollector.SysName)), Tlv(0x04, Encoding.ASCII.GetBytes("sw-coeur"))),
                    Tlv(0x30, Tlv(0x06, Ber.EncodeOid(SnmpCollector.SysLocation)), Tlv(0x80)))));
        var values = Ber.ParseResponse(response, 1234);
        Assert.Equal("sw-coeur", values[SnmpCollector.SysName]);
        Assert.False(values.ContainsKey(SnmpCollector.SysLocation));
        Assert.Throws<FormatException>(() => Ber.ParseResponse(response, 999));
        Assert.Throws<FormatException>(() => Ber.ParseResponse(response[..10], 1234));
    }

    [Theory]
    [InlineData("00-1a-2b-3c-4d-5e", "00:1A:2B:3C:4D:5E")]
    [InlineData("001a.2b3c.4d5e", "00:1A:2B:3C:4D:5E")]
    [InlineData("001A2B3C4D5E", "00:1A:2B:3C:4D:5E")]
    [InlineData("00:1a:2b:3c:4d", null)]
    [InlineData("zz:1a:2b:3c:4d:5e", null)]
    public void CFG_41_adresse_MAC_normalisee(string raw, string? expected) => Assert.Equal(expected, CiRules.NormalizeMac(raw));

    [Fact]
    public void CFG_45_coffre_chiffre_authentifie_et_refuse_sans_cle()
    {
        var vault = new CredentialVault(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Discovery:VaultKey"] = new string('k', 40) }).Build());
        var a = vault.Protect("secret");
        Assert.NotEqual(a, vault.Protect("secret"));   // nonce aléatoire
        Assert.Equal("secret", vault.Unprotect(a));
        var bytes = Convert.FromBase64String(a[3..]);
        bytes[^1] ^= 0xFF;
        Assert.ThrowsAny<CryptographicException>(() => vault.Unprotect("v1:" + Convert.ToBase64String(bytes)));
        var none = new CredentialVault(new ConfigurationBuilder().Build());
        Assert.False(none.Configured);
        Assert.Throws<InvalidOperationException>(() => none.Protect("secret"));
    }

    [Theory]
    [InlineData("srv-mail.corp.local.", "srv-mail.corp.local")]
    [InlineData("192-168-1-11.kubernetes.default.svc.cluster.local", null)]
    [InlineData("192-168-1-11.box.lan", null)]
    [InlineData("192.168.1.11", null)]
    public void CFG_41_un_nom_DNS_fabrique_n_est_pas_un_nom_d_hote(string name, string? expected) =>
        Assert.Equal(expected, NetworkScanner.UsefulHostname(name, System.Net.IPAddress.Parse("192.168.1.11")));

    [Fact]
    public void Empreinte_le_type_deduit_reste_un_type_de_CI()
    {
        Assert.Equal(("Autre", "Imprimante (micrologiciel)"), Fingerprint.FromPorts([9100, 80], 64));
        Assert.Equal("Serveur", Fingerprint.FromPorts([22], 64).Type);
        Assert.Equal("Stockage", Fingerprint.FromSysDescr("Linux DiskStation 4.4 Synology").Type);
        Assert.All(new[] { Fingerprint.FromPorts([135, 445], 128).Type, Fingerprint.FromSysDescr("Cisco IOS").Type },
            t => Assert.Contains(t, ConfigurationItem.Types));
    }

    [Fact]
    public void CFG_49_bannieres_SSH_et_HTTP_lues_et_bornees()
    {
        Assert.Equal("SSH-2.0-OpenSSH_9.6p1 Ubuntu-3ubuntu13.5", Banner.Ssh("SSH-2.0-OpenSSH_9.6p1 Ubuntu-3ubuntu13.5\r\n"));
        Assert.Null(Banner.Ssh("220 smtp ready\r\n"));
        Assert.Equal("nginx/1.24.0", Banner.HttpServer("HTTP/1.1 200 OK\r\nDate: x\r\nServer: nginx/1.24.0\r\n\r\n"));
        Assert.Null(Banner.HttpServer("HTTP/1.1 200 OK\r\n\r\nServer: dans le corps"));
        Assert.Null(Banner.HttpServer("SSH-2.0-x"));
        Assert.Equal(Banner.MaxLength, Banner.Clean(new string('a', 300) + "\u0001")!.Length);
    }

    [Fact]
    public void CFG_49_les_bannieres_precisent_systeme_constructeur_et_type()
    {
        Assert.Equal((null, "Linux Ubuntu", null), Fingerprint.FromBanners([(22, "SSH-2.0-OpenSSH_9.6p1 Ubuntu-3ubuntu13.5")]));
        Assert.Equal((null, "Microsoft Windows", "Serveur"), Fingerprint.FromBanners([(80, "Microsoft-IIS/10.0")]));
        Assert.Equal(("MikroTik", "RouterOS", "Réseau"), Fingerprint.FromBanners([(22, "SSH-2.0-ROSSSH")]));
        Assert.Equal(("HP", "Imprimante (micrologiciel)", "Autre"), Fingerprint.FromBanners([(8080, "HP HTTP Server; HP LaserJet")]));
        Assert.Equal((null, null, null), Fingerprint.FromBanners([(80, "Apache")]));
        Assert.All(new[] { "Réseau", "Serveur", "Stockage", "Autre" }, t => Assert.Contains(t, ConfigurationItem.Types));
    }

    [Theory]
    [InlineData("00:1A:2B:3C:4D:5E", "001A2B")]
    [InlineData("00-1a-2b-3c-4d-5e", "001A2B")]
    [InlineData("DA:A1:19:00:00:01", null)]   // administrée localement (adresse aléatoire)
    [InlineData("01:00:5E:00:00:01", null)]   // groupe (multicast)
    [InlineData("pas une MAC", null)]
    public void CFG_50_prefixe_OUI_d_une_adresse_MAC(string mac, string? expected) => Assert.Equal(expected, MacVendors.PrefixOf(mac));

    [Theory]
    [InlineData("Cisco Systems, Inc", "Cisco Systems")]
    [InlineData("Hewlett Packard Enterprise", "Hewlett Packard Enterprise")]
    [InlineData("HUAWEI TECHNOLOGIES CO.,LTD", "HUAWEI TECHNOLOGIES")]
    [InlineData("Synology Incorporated", "Synology Incorporated")]
    [InlineData("Siemens AG", "Siemens")]
    public void CFG_50_nom_du_constructeur_sans_forme_juridique(string raw, string expected) => Assert.Equal(expected, MacVendors.ShortName(raw));

    [Theory]
    [InlineData(true, "SSH-2.0-OpenSSH_9.6p1 Debian-5\r\n", "SSH-2.0-OpenSSH_9.6p1 Debian-5")]
    [InlineData(false, "HTTP/1.0 200 OK\r\nServer: lighttpd/1.4.59\r\n\r\n", "lighttpd/1.4.59")]
    [InlineData(false, "", null)]
    public async Task CFG_49_banniere_lue_sur_une_connexion_locale(bool ssh, string answer, string? expected)
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        string? request = null;
        var server = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync();
            var stream = socket.GetStream();
            if (!ssh)
            {
                var buffer = new byte[512];
                request = System.Text.Encoding.ASCII.GetString(buffer, 0, await stream.ReadAsync(buffer));
            }
            await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes(answer));
        });
        var banner = await NetworkScanner.ReadBannerAsync(System.Net.IPAddress.Loopback, port, ssh, CancellationToken.None);
        await server;
        listener.Stop();
        Assert.Equal(expected, banner);
        if (!ssh) Assert.StartsWith("HEAD / HTTP/1.0", request);
    }
}
