using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.Internal;
using SAloha.Api.Core.Observability;

namespace SAloha.Api.Tests;

/// <summary>O2 (ADR-0017) : identifiant de trace renvoyé au client, format du journal.</summary>
[Collection("api")]
public class ObservabilityTests(ApiFixture api)
{
    [Fact]
    public async Task O2_chaque_reponse_porte_son_identifiant_de_trace_W3C()
    {
        var res = await api.Client("User", "o2.user").GetAsync("/api/incidents");
        var traceparent = Assert.Single(res.Headers.GetValues("traceparent"));
        Assert.Matches(new Regex("^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$"), traceparent);
        // Deux requêtes, deux traces.
        var other = Assert.Single((await api.Client("User", "o2.user").GetAsync("/api/incidents")).Headers.GetValues("traceparent"));
        Assert.NotEqual(traceparent.Split('-')[1], other.Split('-')[1]);
        // Une réponse refusée aussi : c'est souvent celle qu'on signale.
        Assert.True((await api.AnonymousClient().GetAsync("/api/incidents")).Headers.Contains("traceparent"));
    }

    [Theory]
    [InlineData(null, "Production", true)]
    [InlineData(null, "Development", false)]
    [InlineData("json", "Development", true)]
    [InlineData("TEXT", "Production", false)]
    public void O2_journal_JSON_hors_developpement_sauf_reglage(string? format, string env, bool json)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:Format"] = format }).Build();
        Assert.Equal(json, Telemetry.JsonLogs(config, new HostingEnvironment { EnvironmentName = env }));
    }
}
