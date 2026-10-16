using System.Text.Json;

namespace SAloha.Api.Tests;

/// <summary>Description OpenAPI servie par Swashbuckle (après le passage à .NET 10 et Swashbuckle 10).</summary>
[Collection("api")]
public class OpenApiTests(ApiFixture api)
{
    [Fact]
    public async Task La_description_OpenAPI_est_servie_et_liste_les_routes()
    {
        var res = await api.Client().GetAsync("/swagger/v1/swagger.json");
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body[..Math.Min(body.Length, 300)]}");
        var doc = JsonDocument.Parse(body).RootElement;
        Assert.StartsWith("3.", doc.GetProperty("openapi").GetString());
        var paths = doc.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        Assert.Contains("/api/problems", paths);
        Assert.Contains("/api/changes/{id}/approvals", paths);
    }
}
