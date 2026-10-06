using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;

namespace SAloha.Api.Tests;

/// <summary>
/// L'API réelle sur un PostgreSQL jetable : l'index unique et la concurrence
/// sont précisément ce que ces tests vérifient, une base en mémoire ne les
/// reproduirait pas. L'AD n'est pas sollicité : les jetons sont signés ici,
/// avec la clé de configuration, comme le ferait TokenService.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private const string JwtKey = "cle-de-test-uniquement-0123456789abcdef0123456789abcdef0123";

    // Image déjà présente localement : aucun téléchargement au lancement.
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:18-alpine").Build();

    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("ConnectionStrings:Default", _db.GetConnectionString())
            .UseSetting("Jwt:Key", JwtKey)
            .UseSetting("Jwt:Issuer", "S-Aloha"));
    }

    public HttpClient Client(string role = "Admin", string username = "hery.rakoto")
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(username, role));
        return client;
    }

    private static string Token(string username, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var token = new JwtSecurityToken(
            issuer: "S-Aloha",
            claims: [new Claim(ClaimTypes.Name, username), new Claim("displayName", username), new Claim(ClaimTypes.Role, role)],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _db.DisposeAsync();
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFixture>;
