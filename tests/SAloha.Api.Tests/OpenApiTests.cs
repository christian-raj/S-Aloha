using System.Text.Json;
using System.Text.Json.Nodes;

namespace SAloha.Api.Tests;

/// <summary>
/// Description OpenAPI servie par Swashbuckle, et contrat des réponses (A2, ADR-0016) : aucun
/// schéma n'est une entité EF, et les champs des vues de réponse sont figés dans un instantané
/// versionné. Un champ ajouté ou retiré fait échouer le test ; l'accepter : relancer avec
/// <c>UPDATE_OPENAPI_SNAPSHOT=1</c> et relire la différence dans la PR.
/// </summary>
[Collection("api")]
public class OpenApiTests(ApiFixture api)
{
    private static readonly string Snapshot = Path.Combine(SourceDir(), "openapi-reponses.json");

    private static string SourceDir([System.Runtime.CompilerServices.CallerFilePath] string file = "") => Path.GetDirectoryName(file)!;

    private async Task<JsonElement> Document()
    {
        var res = await api.Client().GetAsync("/swagger/v1/swagger.json");
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body[..Math.Min(body.Length, 300)]}");
        return JsonDocument.Parse(body).RootElement;
    }

    [Fact]
    public async Task La_description_OpenAPI_est_servie_et_liste_les_routes()
    {
        var doc = await Document();
        Assert.StartsWith("3.", doc.GetProperty("openapi").GetString());
        var paths = doc.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        Assert.Contains("/api/problems", paths);
        Assert.Contains("/api/changes/{id}/approvals", paths);
    }

    [Fact]
    public async Task A2_les_reponses_sont_des_DTO_et_leur_contrat_est_fige()
    {
        var schemas = (await Document()).GetProperty("components").GetProperty("schemas").EnumerateObject().ToList();
        var entities = typeof(SAloha.Api.Core.Data.AppDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType).Select(p => p.PropertyType.GetGenericArguments()[0].Name).ToHashSet();
        Assert.Empty(schemas.Select(s => s.Name).Where(entities.Contains));

        // Vues de réponse : nom → champs (nom et type), triés pour une différence lisible.
        var contract = new JsonObject();
        foreach (var s in schemas.Where(s => s.Name.EndsWith("View") || s.Name.EndsWith("Summary")).OrderBy(s => s.Name, StringComparer.Ordinal))
        {
            var fields = new JsonObject();
            if (s.Value.TryGetProperty("properties", out var props))
                foreach (var p in props.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                    fields[p.Name] = Describe(p.Value);
            contract[s.Name] = fields;
        }
        var actual = contract.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI_SNAPSHOT") == "1") File.WriteAllText(Snapshot, actual);
        Assert.True(File.Exists(Snapshot), $"Instantané absent : {Snapshot} (UPDATE_OPENAPI_SNAPSHOT=1 pour le créer).");
        Assert.Equal(File.ReadAllText(Snapshot).ReplaceLineEndings("\n"), actual);
    }

    private static string Describe(JsonElement p)
    {
        if (p.TryGetProperty("$ref", out var r)) return r.GetString()!.Split('/')[^1];
        var type = p.TryGetProperty("type", out var t) ? (t.ValueKind == JsonValueKind.Array ? string.Join("|", t.EnumerateArray().Select(x => x.GetString())) : t.GetString()) : "objet";
        if (p.TryGetProperty("format", out var f)) type += ":" + f.GetString();
        if (p.TryGetProperty("items", out var i)) type += "[" + Describe(i) + "]";
        return type!;
    }
}
