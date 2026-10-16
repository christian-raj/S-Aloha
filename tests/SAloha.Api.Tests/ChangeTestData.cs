using System.Net.Http.Json;
using System.Text.Json;

namespace SAloha.Api.Tests;

/// <summary>Données communes des tests de changements.</summary>
public static class ChangeTestData
{
    private static int? _standardModel;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>CHG-26 : un modèle standard actif, créé une fois pour la base de test.</summary>
    public static async Task<int> StandardModelIdAsync(ApiFixture api)
    {
        await Gate.WaitAsync();
        try
        {
            if (_standardModel is int id) return id;
            var res = await api.Client().PostAsJsonAsync("/api/standard-changes", new
            {
                name = "Modèle standard de test", description = "Tests", implementationPlan = "Appliquer", backoutPlan = "Restaurer", risk = "Faible"
            });
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Modèle standard : {(int)res.StatusCode} {body}");
            return (_standardModel = JsonDocument.Parse(body).RootElement.GetProperty("id").GetInt32()).Value;
        }
        finally { Gate.Release(); }
    }

    /// <summary>CHG-16 : relie au changement un CI créé pour l'occasion, condition pour l'évaluer.</summary>
    public static async Task LinkCiAsync(HttpClient client, int changeId)
    {
        var ci = await client.PostAsJsonAsync("/api/configuration-items", new
        {
            title = $"CI test {Guid.NewGuid():N}"[..20], ciType = "Serveur", environment = "Production", location = "Test",
            status = "En service", ownerType = "User", ownerId = "ci.owner", ownerDisplayName = "Owner"
        });
        var body = await ci.Content.ReadAsStringAsync();
        if (!ci.IsSuccessStatusCode) throw new InvalidOperationException($"CI : {(int)ci.StatusCode} {body}");
        var reference = JsonDocument.Parse(body).RootElement.GetProperty("reference").GetString();
        var link = await client.PostAsJsonAsync("/api/links", new { fromType = "change", fromId = changeId, toReference = reference });
        if (!link.IsSuccessStatusCode) throw new InvalidOperationException($"Lien : {(int)link.StatusCode} {await link.Content.ReadAsStringAsync()}");
    }
}
