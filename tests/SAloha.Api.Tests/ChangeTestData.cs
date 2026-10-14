using System.Net.Http.Json;
using System.Text.Json;

namespace SAloha.Api.Tests;

/// <summary>Données communes des tests de changements.</summary>
public static class ChangeTestData
{
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
