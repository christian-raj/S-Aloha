using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Modules.ServiceConfigurationManagement;
using SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

namespace SAloha.Api.Tests;

/// <summary>
/// Hyperviseur factice servi en HTTPS local, avec un certificat autosigné : les points d'API de
/// Proxmox VE et de vCenter qu'utilisent les collecteurs, et leurs contrôles d'accès.
/// </summary>
public sealed class FakeHypervisorFixture : IAsyncLifetime
{
    public const string ProxmoxToken = "PVEAPIToken=inventaire@pve!saloha=secret-jeton";
    public const string VCenterUser = "inventaire@vsphere.local", VCenterPassword = "mot-de-passe-vc", Session = "jeton-session";
    private WebApplication _app = null!;
    public string Url { get; private set; } = "";
    public int Logouts;

    private static X509Certificate2 SelfSigned()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=hyperviseur-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(san.Build());
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null);
    }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        var certificate = SelfSigned();
        builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0, o => o.UseHttps(certificate)));
        _app = builder.Build();

        IResult Proxmox(HttpContext c, object data) =>
            c.Request.Headers.Authorization == ProxmoxToken ? Results.Json(new { data }) : Results.Unauthorized();
        _app.MapGet("/api2/json/version", (HttpContext c) => Proxmox(c, new { version = "8.2.4" }));
        _app.MapGet("/api2/json/cluster/status", (HttpContext c) => Proxmox(c, new object[]
        {
            new { type = "cluster", name = "grappe" }, new { type = "node", name = "pve1", ip = "10.54.0.1", online = 1 }
        }));
        _app.MapGet("/api2/json/cluster/resources", (HttpContext c) => Proxmox(c, new object[]
        {
            new { vmid = 101, name = "srv-web-54", node = "pve1", status = "running", type = "qemu" },
            new { vmid = 200, name = "ct-dns-54", node = "pve1", status = "running", type = "lxc" },
            new { type = "storage", storage = "local", node = "pve1" },
        }));
        _app.MapGet("/api2/json/nodes/pve1/qemu/101/config", (HttpContext c) => Proxmox(c, new { net0 = "virtio=BC:24:11:54:01:01,bridge=vmbr0", ostype = "l26" }));
        _app.MapGet("/api2/json/nodes/pve1/lxc/200/config", (HttpContext c) => Proxmox(c, new { net0 = "name=eth0,bridge=vmbr0,hwaddr=BC:24:11:54:02:00,ip=dhcp", ostype = "debian" }));

        var basic = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{VCenterUser}:{VCenterPassword}"));
        bool Session(HttpContext c) => c.Request.Headers["vmware-api-session-id"] == FakeHypervisorFixture.Session;
        _app.MapPost("/api/session", (HttpContext c) => c.Request.Headers.Authorization == basic ? Results.Json(FakeHypervisorFixture.Session) : Results.Unauthorized());
        _app.MapDelete("/api/session", (HttpContext c) => { Interlocked.Increment(ref Logouts); return Results.NoContent(); });
        _app.MapGet("/api/vcenter/host", (HttpContext c) => Session(c)
            ? Results.Json(new[] { new { host = "host-10", name = "esx1.corp.local", connection_state = "CONNECTED" } }) : Results.Unauthorized());
        _app.MapGet("/api/vcenter/vm", (HttpContext c) => !Session(c) ? Results.Unauthorized()
            : c.Request.Query["hosts"] == "host-10" ? Results.Json(new[] { new { vm = "vm-42", name = "VM-App", power_state = "POWERED_ON" } }) : Results.Json(Array.Empty<object>()));
        _app.MapGet("/api/vcenter/vm/vm-42", (HttpContext c) => Session(c)
            ? Results.Json(new { guest_OS = "UBUNTU_64", nics = new Dictionary<string, object> { ["4000"] = new { mac_address = "00:50:56:aa:bb:42" } } })
            : Results.Unauthorized());
        _app.MapGet("/api/vcenter/vm/vm-42/guest/identity", (HttpContext c) => Session(c)
            ? Results.Json(new { host_name = "app-42.corp.local", ip_address = "10.55.0.42", full_name = new { default_message = "Ubuntu Linux (64-bit)" } })
            : Results.Unauthorized());

        await _app.StartAsync();
        Url = _app.Urls.Single();
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();
}

/// <summary>Collecteurs Proxmox VE et vCenter (CFG-54) contre l'hyperviseur factice.</summary>
[Collection("api")]
public class HypervisorTests(ApiFixture api, FakeHypervisorFixture hypervisor) : IClassFixture<FakeHypervisorFixture>
{
    private async Task<List<Observation>> Collect(IDiscoveryCollector collector, int perimeter, string credentialType, string? user, string secret)
    {
        var job = new DiscoveryJob { ScanType = collector.ScanType, TargetRange = hypervisor.Url, PerimeterId = perimeter };
        var credential = new DiscoveryCredential { CredentialType = credentialType, Username = user, PerimeterId = perimeter };
        return await collector.CollectAsync(new CollectContext(job, credential, secret, 4), CancellationToken.None);
    }

    private async Task<int> NewPerimeter(string tag)
    {
        var res = await api.Client("Admin", $"admin.{tag}").PostAsJsonAsync("/api/admin/perimeters", new { name = $"Site {tag}" });
        return System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt32();
    }

    private async Task<List<Observation>> Proxmox(int perimeter, string secret = "secret-jeton")
    {
        await using var db = api.Database();
        return await Collect(new ProxmoxCollector(db), perimeter, "ProxmoxToken", "inventaire@pve!saloha", secret);
    }

    [Fact]
    public async Task CFG_54_Proxmox_noeuds_VM_et_conteneurs_avec_leur_hote()
    {
        var perimeter = await NewPerimeter("cfg54a");
        var seen = await Proxmox(perimeter);
        var node = seen.Single(o => o.Hostname == "pve1");
        Assert.Equal(("10.54.0.1", "Proxmox VE 8.2.4", null), (node.IpAddress, node.Os, node.HostedBy));
        var vm = seen.Single(o => o.Hostname == "srv-web-54");
        Assert.Equal(("BC:24:11:54:01:01", "Linux", "pve1"), (vm.MacAddress, vm.Os, vm.HostedBy));
        var ct = seen.Single(o => o.Hostname == "ct-dns-54");
        Assert.Equal(("BC:24:11:54:02:00", "Debian"), (ct.MacAddress, ct.Os));
        Assert.Equal(3, seen.Count);   // le stockage n'est pas une machine
        // Certificat autosigné : retenu au premier contact.
        await using var db = api.Database();
        var key = await db.SshHostKeys.SingleAsync(k => k.PerimeterId == perimeter);
        Assert.Equal((SshHostKey.Tls, "x509", "127.0.0.1"), (key.Protocol, key.Algorithm, key.Host));
    }

    [Fact]
    public async Task CFG_54_jeton_refuse_la_tache_echoue_avec_son_motif()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Proxmox(await NewPerimeter("cfg54b"), "mauvais-secret"));
        Assert.Contains("Accès refusé", error.Message);
        Assert.DoesNotContain("mauvais-secret", error.Message);
    }

    [Fact]
    public async Task CFG_54_certificat_change_connexion_refusee_avant_l_identifiant()
    {
        var perimeter = await NewPerimeter("cfg54c");
        await Proxmox(perimeter);
        await using (var db = api.Database())
        {
            var key = await db.SshHostKeys.SingleAsync(k => k.PerimeterId == perimeter);
            key.Fingerprint = "empreinte-retenue-differente";
            await db.SaveChangesAsync();
        }
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Proxmox(perimeter));
        Assert.Contains("Certificat", error.Message);
        await using var check = api.Database();
        Assert.NotNull((await check.SshHostKeys.SingleAsync(k => k.PerimeterId == perimeter)).PendingFingerprint);
    }

    [Fact]
    public async Task CFG_54_vCenter_hotes_et_VM_session_fermee()
    {
        var before = hypervisor.Logouts;
        await using var db = api.Database();
        var seen = await Collect(new VCenterCollector(db), await NewPerimeter("cfg54d"), "VCenter",
            FakeHypervisorFixture.VCenterUser, FakeHypervisorFixture.VCenterPassword);
        var host = seen.Single(o => o.Hostname == "esx1.corp.local");
        Assert.Equal(("VMware", "VMware ESXi"), (host.Vendor, host.Os));
        var vm = seen.Single(o => o.Hostname == "app-42.corp.local");
        Assert.Equal(("10.55.0.42", "00:50:56:AA:BB:42", "Ubuntu Linux (64-bit)", "esx1.corp.local"), (vm.IpAddress, vm.MacAddress, vm.Os, vm.HostedBy));
        Assert.Equal(before + 1, hypervisor.Logouts);
    }

    [Theory]
    [InlineData("https://pve.corp.local:8006", true)]
    [InlineData("https://vcenter.corp.local", true)]
    [InlineData("http://pve.corp.local:8006", false)]
    [InlineData("https://pve.corp.local:8006/api2/json", false)]
    [InlineData("10.0.0.0/24", false)]
    public void CFG_54_cible_URL_https_de_l_API(string target, bool valid) => Assert.Equal(valid, HypervisorApi.Validate(target) is null);
}
