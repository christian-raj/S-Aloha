using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SAloha.Api.Modules.ServiceConfigurationManagement;
using SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

namespace SAloha.Api.Tests;

/// <summary>
/// Serveur WS-Management factice en HTTPS local (certificat autosigné), sur le modèle de
/// WinRM : échange NTLM (défi, puis lecture du message d'authentification du client, sans
/// vérifier la réponse cryptographique, faute du condensat du mot de passe), puis
/// Enumerate et Pull sur les classes CIM de l'inventaire. Ne remplace pas un Windows réel.
/// </summary>
public sealed class FakeWinRmFixture : IAsyncLifetime
{
    public const string Domain = "CORP", User = "inventaire";
    private WebApplication _app = null!;
    public int Port { get; private set; }
    public readonly List<string> Authenticated = [];

    private static readonly Dictionary<string, string> Items = new()
    {
        ["Win32_OperatingSystem"] = "<p:Caption>Microsoft Windows Server 2022 Standard</p:Caption><p:Version>10.0.20348</p:Version><p:CSName>SRV-WIN-57</p:CSName><p:ProductType>3</p:ProductType>",
        ["Win32_ComputerSystem"] = "<p:DNSHostName>srv-win-57</p:DNSHostName><p:Domain>corp.local</p:Domain><p:PartOfDomain>true</p:PartOfDomain><p:Manufacturer>Dell Inc.</p:Manufacturer><p:Model>PowerEdge R650</p:Model>",
        ["Win32_BIOS"] = "<p:SerialNumber>SN-WIN-57</p:SerialNumber>",
        ["Win32_NetworkAdapterConfiguration"] = "<p:IPEnabled>true</p:IPEnabled><p:MACAddress>00:15:5D:57:00:01</p:MACAddress><p:IPAddress>127.0.0.1</p:IPAddress><p:IPAddress>fe80::1</p:IPAddress>",
    };

    private static string Instance(string cls, string body) =>
        $"<p:{cls} xmlns:p=\"http://schemas.microsoft.com/wbem/wsman/1/wmi/root/cimv2/{cls}\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">{body}</p:{cls}>";

    private static string Envelope(string body) =>
        "<s:Envelope xmlns:s=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:n=\"http://schemas.xmlsoap.org/ws/2004/09/enumeration\" "
        + $"xmlns:w=\"http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd\"><s:Header/><s:Body>{body}</s:Body></s:Envelope>";

    /// <summary>Réponse à une requête : classe désignée par ResourceURI ; les programmes en deux pages (Pull).</summary>
    private static (int Status, string Body) Answer(XDocument request)
    {
        var uri = request.Descendants().First(e => e.Name.LocalName == "ResourceURI").Value;
        var cls = uri[(uri.LastIndexOf('/') + 1)..];
        var pull = request.Descendants().Any(e => e.Name.LocalName == "Pull");
        string items;
        var end = "<w:EndOfSequence/>";
        string context = "";
        switch (cls)
        {
            case "Win32_InstalledWin32Program" when !pull:
                items = Instance(cls, "<p:Name>7-Zip 23.01 (x64)</p:Name><p:Version>23.01</p:Version>");
                (end, context) = ("", "<n:EnumerationContext>ctx-programmes</n:EnumerationContext>");
                break;
            case "Win32_InstalledWin32Program":
                items = Instance(cls, "<p:Name>Microsoft SQL Server 2019</p:Name><p:Version>15.0.2000.5</p:Version>");
                break;
            case "Win32_Service":
                items = Instance(cls, "<p:Name>MSSQLSERVER</p:Name><p:State>Running</p:State>") + Instance(cls, "<p:Name>Spooler</p:Name><p:State>Stopped</p:State>");
                break;
            case "MSFT_NetTCPConnection":
                items = Instance(cls, "<p:LocalAddress>0.0.0.0</p:LocalAddress><p:LocalPort>1433</p:LocalPort><p:State>2</p:State>")
                        + Instance(cls, "<p:LocalAddress>0.0.0.0</p:LocalAddress><p:LocalPort>5986</p:LocalPort><p:State>2</p:State>")
                        + Instance(cls, "<p:LocalAddress>10.57.0.10</p:LocalAddress><p:LocalPort>1433</p:LocalPort><p:RemoteAddress>10.57.0.20</p:RemoteAddress><p:RemotePort>50001</p:RemotePort><p:State>5</p:State>")
                        + Instance(cls, "<p:LocalAddress>10.57.0.10</p:LocalAddress><p:LocalPort>5986</p:LocalPort><p:RemoteAddress>10.57.0.99</p:RemoteAddress><p:RemotePort>50002</p:RemotePort><p:State>5</p:State>");
                break;
            case var known when Items.ContainsKey(known):
                items = Instance(cls, Items[cls]);
                break;
            default:
                return (500, Envelope("<s:Fault><s:Reason><s:Text>The WS-Management service cannot process the request. The class was not found.</s:Text></s:Reason>"
                                      + "<s:Detail><w:WSManFault Code=\"2150858752\"/></s:Detail></s:Fault>"));
        }
        var tag = pull ? "n:PullResponse" : "n:EnumerateResponse";
        var wrapper = pull ? "n:Items" : "w:Items";
        return (200, Envelope($"<{tag}>{context}<{wrapper}>{items}</{wrapper}>{(pull ? end.Replace("w:", "n:") : end)}</{tag}>"));
    }

    /// <summary>Message NTLM de défi (type 2) : drapeaux du client, informations de cible avec horodatage.</summary>
    private static byte[] Challenge(byte[] negotiate)
    {
        var flags = BitConverter.ToUInt32(negotiate, 12) | 0x00800000u | 0x00010000u;   // TARGET_INFO, TARGET_TYPE_DOMAIN
        flags &= ~0x00000002u;                                                          // pas d'OEM
        var target = Encoding.Unicode.GetBytes(Domain);
        var info = new List<byte>();
        void Av(ushort id, byte[] value) { info.AddRange(BitConverter.GetBytes(id)); info.AddRange(BitConverter.GetBytes((ushort)value.Length)); info.AddRange(value); }
        Av(2, target);
        Av(1, Encoding.Unicode.GetBytes("SRV-WIN-57"));
        Av(7, BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()));
        Av(0, []);
        const int header = 56;
        var message = new List<byte>(Encoding.ASCII.GetBytes("NTLMSSP\0"));
        message.AddRange(BitConverter.GetBytes(2));
        message.AddRange(BitConverter.GetBytes((ushort)target.Length)); message.AddRange(BitConverter.GetBytes((ushort)target.Length));
        message.AddRange(BitConverter.GetBytes(header));
        message.AddRange(BitConverter.GetBytes(flags));
        message.AddRange(RandomNumberGenerator.GetBytes(8));
        message.AddRange(new byte[8]);
        message.AddRange(BitConverter.GetBytes((ushort)info.Count)); message.AddRange(BitConverter.GetBytes((ushort)info.Count));
        message.AddRange(BitConverter.GetBytes(header + target.Length));
        message.AddRange([10, 0, 0x7C, 0x4F, 0, 0, 0, 15]);   // version 10.0.20348, révision NTLM 15
        message.AddRange(target);
        message.AddRange(info);
        return [.. message];
    }

    /// <summary>Domaine et compte d'un message d'authentification NTLM (type 3).</summary>
    private static (string Domain, string User) Account(byte[] authenticate)
    {
        string Field(int at) => Encoding.Unicode.GetString(authenticate, BitConverter.ToInt32(authenticate, at + 4), BitConverter.ToUInt16(authenticate, at));
        return (Field(28), Field(36));
    }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=winrm-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var certificate = X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pfx), null);
        builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0, o => o.UseHttps(certificate)));
        _app = builder.Build();
        _app.MapPost("/wsman", async (HttpContext c) =>
        {
            var auth = c.Request.Headers.Authorization.ToString();
            if (!auth.StartsWith("NTLM ", StringComparison.Ordinal))
            {
                c.Response.Headers.WWWAuthenticate = "NTLM";
                return Results.StatusCode(401);
            }
            var token = Convert.FromBase64String(auth[5..]);
            if (BitConverter.ToInt32(token, 8) == 1)
            {
                c.Response.Headers.WWWAuthenticate = "NTLM " + Convert.ToBase64String(Challenge(token));
                return Results.StatusCode(401);
            }
            var (domain, user) = Account(token);
            lock (Authenticated) Authenticated.Add($"{domain}\\{user}");
            if (!string.Equals(user, User, StringComparison.OrdinalIgnoreCase)) return Results.StatusCode(401);
            var (status, body) = Answer(XDocument.Parse(await new StreamReader(c.Request.Body).ReadToEndAsync()));
            return Results.Content(body, "application/soap+xml;charset=UTF-8", Encoding.UTF8, status);
        });
        await _app.StartAsync();
        Port = new Uri(_app.Urls.Single()).Port;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();
}

/// <summary>Collecteur WinRM (CFG-57) contre le serveur WS-Management factice.</summary>
[Collection("api")]
public class WinRmTests(ApiFixture api, FakeWinRmFixture winrm) : IClassFixture<FakeWinRmFixture>
{
    private async Task<int> NewPerimeter(string tag)
    {
        var res = await api.Client("Admin", $"admin.{tag}").PostAsJsonAsync("/api/admin/perimeters", new { name = $"Site {tag}" });
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt32();
    }

    private async Task<List<Observation>> Collect(int perimeter, string user = FakeWinRmFixture.User)
    {
        await using var db = api.Database();
        var collector = new WinRmCollector(db, NullLogger<WinRmCollector>.Instance);
        var job = new DiscoveryJob { ScanType = "WinRm", TargetRange = "127.0.0.1", PortList = winrm.Port.ToString(), PerimeterId = perimeter };
        var credential = new DiscoveryCredential { CredentialType = "WindowsDomain", Username = user, Domain = FakeWinRmFixture.Domain, PerimeterId = perimeter };
        return await collector.CollectAsync(new CollectContext(job, credential, "mot-de-passe-57", 4), CancellationToken.None);
    }

    [Fact]
    public async Task CFG_57_inventaire_Windows_par_NTLM_et_WS_Management()
    {
        var seen = Assert.Single(await Collect(await NewPerimeter("cfg57a")));
        Assert.Contains($"{FakeWinRmFixture.Domain}\\{FakeWinRmFixture.User}", winrm.Authenticated, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(("WinRm", "Serveur", "srv-win-57.corp.local"), (seen.Source, seen.Type, seen.Hostname));
        Assert.Equal("Microsoft Windows Server 2022 Standard (10.0.20348)", seen.Os);
        Assert.Equal(("Dell", "PowerEdge R650", "SN-WIN-57", "00:15:5D:57:00:01"), (seen.Vendor, seen.Model, seen.SerialNumber, seen.MacAddress));
        // Programmes sur deux pages (Enumerate puis Pull) ; services actifs seulement.
        Assert.Equal(["7-Zip 23.01 (x64)", "Microsoft SQL Server 2019"], seen.Software!.Select(s => s.Name));
        Assert.Equal("MSSQLSERVER", seen.Services);
        Assert.Equal("1433,5986", seen.ListeningPorts);
        // Connexion entrante sur SQL Server gardée ; celle sur le port WinRM écartée.
        Assert.Equal([new Flow(false, "10.57.0.20", 1433)], seen.Flows);
    }

    [Fact]
    public async Task CFG_57_compte_refuse_la_tache_echoue_avec_son_motif()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Collect(await NewPerimeter("cfg57b"), "autre"));
        Assert.Contains("Accès refusé", error.Message);
        Assert.DoesNotContain("mot-de-passe-57", error.Message);
    }

    [Fact]
    public void CFG_57_un_poste_de_travail_et_les_valeurs_par_defaut_du_constructeur()
    {
        var seen = WinRmCollector.Build("10.0.0.7", 5986,
            new() { ["Caption"] = ["Microsoft Windows 11 Pro"], ["ProductType"] = ["1"], ["CSName"] = ["PC-07"] },
            new() { ["Model"] = ["System Product Name"], ["PartOfDomain"] = ["false"] },
            new() { ["SerialNumber"] = ["To be filled by O.E.M."] }, [], [], [], []);
        Assert.Equal(("Poste de travail", "pc-07", null, null), (seen.Type, seen.Hostname, seen.SerialNumber, seen.Model));
    }
}
