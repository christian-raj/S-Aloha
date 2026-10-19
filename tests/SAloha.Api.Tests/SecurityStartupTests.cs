using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using SAloha.Api.Core.Auth;

namespace SAloha.Api.Tests;

/// <summary>Durcissement issu de l'audit du 2026-10-16 : secrets d'exemple refusés (S7), audience du jeton (S8).</summary>
[Collection("api")]
public class SecurityStartupTests(ApiFixture api)
{
    private static IConfiguration Config(string jwtKey, string bindPassword, string db, bool allow = false) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = jwtKey, ["Ldap:BindPassword"] = bindPassword, ["ConnectionStrings:Default"] = db,
            ["Security:AllowSampleSecrets"] = allow ? "true" : null
        }).Build();

    private static HostingEnvironment Env(string name) => new() { EnvironmentName = name };

    [Fact]
    public void S7_la_cle_d_exemple_du_coffre_est_refusee_comme_les_autres_secrets()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = new string('x', 64), ["Ldap:BindPassword"] = "vrai-secret", ["ConnectionStrings:Default"] = "Host=db;Password=autre",
            ["Discovery:VaultKey"] = SampleSecrets.SampleVaultKey
        }).Build();
        Assert.Contains("Discovery:VaultKey", Assert.Single(SampleSecrets.Problems(config)));
        // La valeur livrée dans appsettings.json est bien celle que le contrôle reconnaît.
        var shipped = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json")).Build();
        Assert.Equal(SampleSecrets.SampleVaultKey, shipped["Discovery:VaultKey"]);
    }

    [Fact]
    public void S7_valeurs_d_exemple_detectees_et_refusees_hors_developpement()
    {
        var sample = Config(SampleSecrets.SampleJwtKey, "MotDePasseServiceAccount", "Host=db;Password=ChangeMe_Str0ng!");
        Assert.Equal(3, SampleSecrets.Problems(sample).Count);
        Assert.Contains("moins de 64", Assert.Single(SampleSecrets.Problems(Config("courte", "vrai-secret", "Host=db;Password=autre"))));
        Assert.Empty(SampleSecrets.Problems(Config(new string('x', 64), "vrai-secret", "Host=db;Password=autre")));

        var refused = Assert.Throws<InvalidOperationException>(() => SampleSecrets.Check(sample, Env("Production"), NullLogger.Instance));
        Assert.Contains("Démarrage refusé", refused.Message);
        Assert.DoesNotContain("ChangeMe_Str0ng!", refused.Message);   // le message nomme le réglage, pas sa valeur
        SampleSecrets.Check(sample, Env("Development"), NullLogger.Instance);   // poste de développement : accepté
        SampleSecrets.Check(Config(SampleSecrets.SampleJwtKey, "MotDePasseServiceAccount", "x", allow: true), Env("Production"), NullLogger.Instance);   // démonstration
    }

    private static string Token(string? audience, string key = ApiFixture.JwtKey)
    {
        var token = new JwtSecurityToken(issuer: "S-Aloha", audience: audience,
            claims: [new Claim(ClaimTypes.Name, "s8.user"), new Claim(ClaimTypes.Role, "User")],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task S8_un_jeton_sans_l_audience_S_Aloha_est_refuse()
    {
        async Task<HttpStatusCode> Get(string token)
        {
            var c = api.Client();
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return (await c.GetAsync("/api/incidents")).StatusCode;
        }
        Assert.Equal(HttpStatusCode.OK, await Get(Token("S-Aloha")));
        Assert.Equal(HttpStatusCode.Unauthorized, await Get(Token(null)));
        Assert.Equal(HttpStatusCode.Unauthorized, await Get(Token("autre-service")));
    }
}
