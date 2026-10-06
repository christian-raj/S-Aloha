using System.Net;
using System.Net.Http.Json;

namespace SAloha.Api.Tests;

/// <summary>Références PRB-AAAA-NNNN — constats B2 et B3 de la revue du 2026-10-06.</summary>
[Collection("api")]
public class ProblemReferenceTests(ApiFixture api)
{
    private record Created(int Id, string Reference);

    private static object NewProblem(string title) => new
    {
        title, description = "d", impact = "Moyen", urgency = "Moyenne",
        category = "Test", affectedService = "Test"
    };

    private static async Task<Created> CreateAsync(HttpClient client, string title)
    {
        var res = await client.PostAsJsonAsync("/api/problems", NewProblem(title));
        Assert.True(res.StatusCode == HttpStatusCode.Created, $"HTTP {(int)res.StatusCode} : {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<Created>())!;
    }

    [Fact]
    public async Task La_declaration_reste_possible_apres_une_suppression()
    {
        var client = api.Client();
        var a = await CreateAsync(client, "B2 premier");
        var b = await CreateAsync(client, "B2 deuxième");
        await CreateAsync(client, "B2 troisième");

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/problems/{b.Id}")).StatusCode);

        // Avant correction : la référence recalculée existait déjà → 500, et
        // toutes les déclarations suivantes échouaient de même.
        var next = await CreateAsync(client, "B2 après suppression");
        var again = await CreateAsync(client, "B2 encore après");
        Assert.NotEqual(next.Reference, again.Reference);
        Assert.NotEqual(a.Reference, next.Reference);
    }

    [Fact]
    public async Task Des_declarations_simultanees_aboutissent_toutes()
    {
        var client = api.Client();
        var results = await Task.WhenAll(Enumerable.Range(1, 8)
            .Select(i => client.PostAsJsonAsync("/api/problems", NewProblem($"B3 simultané {i}"))));

        // Avant correction : une seule sur cinq aboutissait, les autres en 500.
        Assert.All(results, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var refs = await Task.WhenAll(results.Select(async r => (await r.Content.ReadFromJsonAsync<Created>())!.Reference));
        Assert.Equal(refs.Length, refs.Distinct().Count());
    }
}
