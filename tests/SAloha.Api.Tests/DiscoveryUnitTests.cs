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

    [Fact]
    public void CFG_51_interfaces_ifTable_nature_et_adresse()
    {
        Assert.Equal(("ethernet", "boucle locale", "agrégat", "autre"),
            (SnmpCollector.Kind("6"), SnmpCollector.Kind("24"), SnmpCollector.Kind("161"), SnmpCollector.Kind(null)));
        Assert.Equal("00:1A:2B:3C:4D:5E", SnmpCollector.Mac(new Lextm.SharpSnmpLib.OctetString([0x00, 0x1A, 0x2B, 0x3C, 0x4D, 0x5E])));
        Assert.Null(SnmpCollector.Mac(new Lextm.SharpSnmpLib.OctetString(new byte[6])));
        Assert.Null(SnmpCollector.Mac(new Lextm.SharpSnmpLib.OctetString("")));
        Assert.Equal("Gi0/1 · ethernet · active · 1000 Mb/s · 00:1A:2B:3C:4D:5E",
            new SnmpInterface(1, "Gi0/1", "ethernet", true, 1000, "00:1A:2B:3C:4D:5E").ToString());
    }

    [Fact]
    public void CFG_51_secret_SNMP_v3_les_deux_phrases_ensemble()
    {
        var s = new SnmpV3Secret("phrase-auth", "phrase-chiffrement");
        Assert.Equal(s, SnmpV3Secret.Parse(s.Serialize()));
        Assert.Throws<InvalidOperationException>(() => SnmpAccess.From(null, "x"));
        Assert.DoesNotContain("MD5", SnmpAccess.AuthProtocols);
        Assert.DoesNotContain("DES", SnmpAccess.PrivProtocols);
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

    [Fact]
    public void CFG_52_lecture_de_l_inventaire_SSH()
    {
        const string output = "##os\nPRETTY_NAME=\"Ubuntu 22.04.4 LTS\"\nNAME=\"Ubuntu\"\n##uname\nLinux 5.15.0 x86_64\n##hostname\nsrv-app.corp.local\n"
            + "##dmi\nsys_vendor=Dell Inc.\nproduct_name=PowerEdge R650\nproduct_serial=\n"
            + "##pkg\nopenssh-server\t1:8.9p1-3ubuntu0.6\nnginx\t1.18.0-6ubuntu14\n"
            + "##svc\nnginx.service loaded active running A high performance web server\nssh.service loaded active running OpenBSD Secure Shell server\n"
            + "##listen\nLISTEN 0 511 0.0.0.0:80 0.0.0.0:*\nLISTEN 0 128 127.0.0.1:5432 0.0.0.0:*\nLISTEN 0 128 [::]:22 [::]:*\nLISTEN 0 128 [::1]:631 [::]:*\n";
        var o = SshInventory.Parse(output, "10.0.0.5");
        Assert.Equal(("Ubuntu 22.04.4 LTS", "srv-app.corp.local", "Dell", "PowerEdge R650"), (o.Os, o.Hostname, o.Vendor, o.Model));
        Assert.Null(o.SerialNumber);   // illisible sans droits root
        Assert.Equal([new SoftwareItem("openssh-server", "1:8.9p1-3ubuntu0.6"), new SoftwareItem("nginx", "1.18.0-6ubuntu14")], o.Software);
        Assert.Equal("nginx\nssh", o.Services);
        Assert.Equal("22,80", o.ListeningPorts);
    }

    [Fact]
    public void CFG_52_paquets_apk_et_rpm_ports_netstat_systeme_par_uname()
    {
        Assert.Equal([new SoftwareItem("musl", "1.2.5-r1"), new SoftwareItem("ca-certificates-bundle", "20240705-r0")],
            SshInventory.Packages("musl-1.2.5-r1\nca-certificates-bundle-20240705-r0\nsans-version\n"));
        Assert.Equal(new SoftwareItem("kernel", "5.14.0-427.el9"), SshInventory.Packages("kernel\t5.14.0-427.el9")[0]);
        Assert.Equal([22, 443], SshInventory.ListeningPorts("tcp 0 0 0.0.0.0:443 0.0.0.0:* LISTEN\ntcp 0 0 10.0.0.5:22 0.0.0.0:* LISTEN\ntcp 0 0 127.0.0.1:25 0.0.0.0:* LISTEN"));
        Assert.Equal("Linux 6.1.0 aarch64", SshInventory.OsName("", "Linux 6.1.0 aarch64\n"));
        Assert.Equal(22, SshCollector.SshPort(""));
        Assert.Equal(2222, SshCollector.SshPort("2222"));
    }

    [Fact]
    public void CFG_53_confiance_au_premier_contact()
    {
        var known = new System.Collections.Concurrent.ConcurrentDictionary<string, SshHostKey>();
        Assert.True(SshCollector.Trust(known, 1, "10.0.0.5", 22, "ssh-ed25519", "AAAA"));
        Assert.True(SshCollector.Trust(known, 1, "10.0.0.5", 22, "ssh-ed25519", "AAAA"));
        Assert.False(SshCollector.Trust(known, 1, "10.0.0.5", 22, "ssh-ed25519", "BBBB"));
        Assert.Equal(("AAAA", "BBBB"), (known["10.0.0.5"].Fingerprint, known["10.0.0.5"].PendingFingerprint));
    }

    [Fact]
    public void CFG_56_connexions_etablies_sens_et_port_de_service()
    {
        const string ss = "0 0 10.0.0.5:51234 10.0.0.9:5432\n0 0 10.0.0.5:443 10.0.0.30:60000\n0 0 10.0.0.5:22 10.0.0.99:50022\n"
            + "0 0 127.0.0.1:40000 127.0.0.1:6379\n0 0 [::ffff:10.0.0.5]:51235 [::ffff:10.0.0.9]:5432\n";
        var flows = SshInventory.Flows(ss, [22, 443], 22);
        Assert.Equal([new Flow(true, "10.0.0.9", 5432), new Flow(false, "10.0.0.30", 443)], flows);
        // netstat : même lecture.
        Assert.Equal([new Flow(true, "10.0.0.9", 3306)],
            SshInventory.Flows("tcp 0 0 10.0.0.5:41000 10.0.0.9:3306 ESTABLISHED", [], 22));
        Assert.Equal([new Flow(true, "10.0.0.9", 5432)], SshInventory.Parse("##listen\n##conn\n" + ss, "10.0.0.5").Flows!.Take(1));
    }
}
