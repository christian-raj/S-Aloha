using System.Net;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using SAloha.Api.Modules.ServiceConfigurationManagement;
using SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

namespace SAloha.Api.Tests;

/// <summary>
/// Agent SNMP réel (net-snmp dans un conteneur, en local) : une communauté v2c et un
/// utilisateur v3 authentifié et chiffré (SHA-256, AES). Port non privilégié dans le conteneur.
/// </summary>
public sealed class SnmpAgentFixture : IAsyncLifetime
{
    public const string Community = "communaute-test", User = "saloha", Auth = "auth-pass-123", Priv = "priv-pass-123";
    private const string Script = """
        apk add --no-cache net-snmp >/dev/null && mkdir -p /var/lib/net-snmp && cat > /etc/snmp/snmpd.conf <<'C'
        agentAddress udp:1161
        rocommunity communaute-test
        createUser saloha SHA-256 "auth-pass-123" AES "priv-pass-123"
        rouser saloha priv
        sysName agent-test
        sysLocation Salle de test
        sysDescr Linux agent-test 6.1 test
        C
        exec snmpd -f -Lo -C -c /etc/snmp/snmpd.conf
        """;

    private readonly IContainer _agent = new ContainerBuilder("alpine:3.20")
        .WithCommand("sh", "-c", Script)
        .WithPortBinding("1161/udp", true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("NET-SNMP version"))
        .Build();

    public IPEndPoint Endpoint { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _agent.StartAsync();
        Endpoint = new IPEndPoint(IPAddress.Loopback, _agent.GetMappedPublicPort("1161/udp"));
    }

    public Task DisposeAsync() => _agent.DisposeAsync().AsTask();
}

/// <summary>Collecteur SNMP contre l'agent réel : v2c, v3 (USM), ifTable (CFG-51).</summary>
public class SnmpAgentTests(SnmpAgentFixture agent) : IClassFixture<SnmpAgentFixture>
{
    private readonly SnmpCollector _collector = new(NullLogger<SnmpCollector>.Instance);

    private static void AssertAgent(Observation? seen)
    {
        Assert.NotNull(seen);
        Assert.Equal(("agent-test", "Salle de test"), (seen.Hostname, seen.Location));
        Assert.StartsWith("Linux agent-test", seen.Os);
        // ifTable : l'interface eth0 du conteneur, la boucle locale exclue.
        Assert.Contains("eth0 · ethernet · active", seen.Interfaces);
        Assert.DoesNotContain("lo ·", seen.Interfaces);
    }

    [Fact]
    public async Task SNMP_v2c_lit_MIB_II_et_interfaces() =>
        AssertAgent(await _collector.ReadAsync(agent.Endpoint, SnmpAccess.V2c(SnmpAgentFixture.Community), CancellationToken.None));

    [Fact]
    public async Task CFG_51_SNMP_v3_authentifie_et_chiffre() =>
        AssertAgent(await _collector.ReadAsync(agent.Endpoint,
            SnmpAccess.V3(SnmpAgentFixture.User, "SHA256", SnmpAgentFixture.Auth, "AES", SnmpAgentFixture.Priv), CancellationToken.None));

    [Theory]
    [InlineData("saloha", "mauvaise-phrase", "priv-pass-123")]
    [InlineData("inconnu", "auth-pass-123", "priv-pass-123")]
    public async Task CFG_51_SNMP_v3_refuse_sans_le_bon_secret(string user, string auth, string priv) =>
        Assert.Null(await _collector.ReadAsync(agent.Endpoint, SnmpAccess.V3(user, "SHA256", auth, "AES", priv), CancellationToken.None));

    [Fact]
    public async Task SNMP_mauvaise_communaute_ou_agent_muet_sans_observation()
    {
        Assert.Null(await _collector.ReadAsync(agent.Endpoint, SnmpAccess.V2c("autre"), CancellationToken.None));
        Assert.Null(await _collector.ReadAsync(new IPEndPoint(IPAddress.Loopback, 9), SnmpAccess.V2c("x"), CancellationToken.None));
    }

    [Fact]
    public async Task CFG_51_collecte_d_une_tache_avec_un_identifiant_v3_du_coffre()
    {
        var collector = new SnmpCollector(NullLogger<SnmpCollector>.Instance) { AgentPort = agent.Endpoint.Port };
        var credential = new DiscoveryCredential { CredentialType = "SnmpV3", Username = SnmpAgentFixture.User, AuthProtocol = "SHA256", PrivProtocol = "AES" };
        var job = new DiscoveryJob { ScanType = "Snmp", TargetRange = "127.0.0.1" };
        var seen = Assert.Single(await collector.CollectAsync(
            new CollectContext(job, credential, new SnmpV3Secret(SnmpAgentFixture.Auth, SnmpAgentFixture.Priv).Serialize(), 4), CancellationToken.None));
        Assert.Equal(("Snmp", "127.0.0.1", "agent-test"), (seen.Source, seen.IpAddress, seen.Hostname));
        // L'interface du conteneur a une adresse administrée localement (Docker) : pas retenue comme MAC de l'actif.
        Assert.Null(seen.MacAddress);
        Assert.Contains("eth0 · ethernet", seen.Interfaces);
    }
}
