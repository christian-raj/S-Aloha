using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SAloha.Api.Modules.ServiceConfigurationManagement;
using SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

namespace SAloha.Api.Tests;

/// <summary>
/// Serveur SSH réel (OpenSSH dans un conteneur, en local) : un compte de service avec mot de
/// passe et clé ed25519, sans droits d'administration.
/// </summary>
public sealed class SshServerFixture : IAsyncLifetime
{
    public const string User = "inventaire", Password = "mot-de-passe-ssh";
    private const string Script = """
        apk add --no-cache openssh >/dev/null && ssh-keygen -A >/dev/null
        adduser -D inventaire && echo 'inventaire:mot-de-passe-ssh' | chpasswd
        mkdir -p /home/inventaire/.ssh && ssh-keygen -q -t ed25519 -N '' -f /cle
        cp /cle.pub /home/inventaire/.ssh/authorized_keys && chown -R inventaire /home/inventaire/.ssh
        chmod 700 /home/inventaire/.ssh && chmod 600 /home/inventaire/.ssh/authorized_keys
        sed -i 's/^#\?PasswordAuthentication.*/PasswordAuthentication yes/' /etc/ssh/sshd_config
        exec /usr/sbin/sshd -D -e -p 2222
        """;

    private readonly IContainer _server = new ContainerBuilder("alpine:3.20")
        .WithCommand("sh", "-c", Script)
        .WithPortBinding(2222, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server listening"))
        .Build();

    public int Port { get; private set; }
    public string PrivateKey { get; private set; } = "";

    public async Task InitializeAsync()
    {
        await _server.StartAsync();
        Port = _server.GetMappedPublicPort(2222);
        PrivateKey = (await _server.ExecAsync(["cat", "/cle"])).Stdout;
    }

    public Task DisposeAsync() => _server.DisposeAsync().AsTask();
}

/// <summary>Collecteur SSH contre un serveur réel : inventaire (CFG-52), clé d'hôte (CFG-53).</summary>
[Collection("api")]
public class SshAgentTests(ApiFixture api, SshServerFixture ssh) : IClassFixture<SshServerFixture>
{
    private readonly SshServerFixture _ssh = ssh;

    private async Task<int> NewPerimeter(string tag)
    {
        var res = await api.Client("Admin", $"admin.{tag}").PostAsJsonAsync("/api/admin/perimeters", new { name = $"Site {tag}" });
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt32();
    }

    private async Task<List<Observation>> Collect(int perimeter, SshSecret secret, string user = SshServerFixture.User)
    {
        await using var db = api.Database();
        var collector = new SshCollector(db, NullLogger<SshCollector>.Instance);
        var job = new DiscoveryJob { ScanType = "Ssh", TargetRange = "127.0.0.1", PortList = _ssh.Port.ToString(), PerimeterId = perimeter };
        var credential = new DiscoveryCredential { CredentialType = "Ssh", Username = user, PerimeterId = perimeter };
        return await collector.CollectAsync(new CollectContext(job, credential, secret.Serialize(), 4), CancellationToken.None);
    }

    [Fact]
    public async Task CFG_52_inventaire_par_mot_de_passe_sans_droits_d_administration()
    {
        var seen = Assert.Single(await Collect(await NewPerimeter("cfg52a"), new SshSecret(SshServerFixture.Password, null, null)));
        Assert.Equal(("Ssh", "Serveur"), (seen.Source, seen.Type));
        Assert.StartsWith("Alpine Linux", seen.Os);
        Assert.Contains(seen.Software!, s => s.Name == "openssh-server" && s.Version is not null);
        Assert.Contains("2222", seen.ListeningPorts);
    }

    [Fact]
    public async Task CFG_52_inventaire_par_cle_privee()
    {
        Assert.Null(SshSecret.CheckKey(_ssh.PrivateKey, null));
        Assert.NotNull(SshSecret.CheckKey("pas une clé", null));
        Assert.Single(await Collect(await NewPerimeter("cfg52b"), new SshSecret(null, _ssh.PrivateKey, null)));
    }

    [Fact]
    public async Task CFG_52_mauvais_mot_de_passe_aucun_inventaire() =>
        Assert.Empty(await Collect(await NewPerimeter("cfg52c"), new SshSecret("mauvais", null, null)));

    [Fact]
    public async Task CFG_53_cle_retenue_au_premier_contact_puis_cle_changee_refusee_jusqu_a_acceptation()
    {
        var perimeter = await NewPerimeter("cfg53");
        var secret = new SshSecret(SshServerFixture.Password, null, null);
        Assert.Single(await Collect(perimeter, secret));
        int keyId;
        await using (var db = api.Database())
        {
            var key = await db.SshHostKeys.SingleAsync(k => k.PerimeterId == perimeter);
            Assert.Equal(("127.0.0.1", _ssh.Port), (key.Host, key.Port));
            // L'hôte présente désormais une autre clé que celle retenue (usurpation possible).
            key.Fingerprint = "empreinte-retenue-differente";
            await db.SaveChangesAsync();
            keyId = key.Id;
        }
        Assert.Empty(await Collect(perimeter, secret));
        var manager = api.Client("Manager", "chef.cfg53");
        var listed = JsonDocument.Parse(await (await api.Client("Admin", "admin.cfg53").GetAsync("/api/discovery/ssh-host-keys")).Content.ReadAsStringAsync())
            .RootElement.EnumerateArray().Single(k => k.GetProperty("id").GetInt32() == keyId);
        Assert.True(listed.GetProperty("changed").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"/api/discovery/ssh-host-keys/{keyId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Client("Admin", "admin.cfg53").PostAsync($"/api/discovery/ssh-host-keys/{keyId}/accept", null)).StatusCode);
        Assert.Single(await Collect(perimeter, secret));
    }
}
