using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SAloha.Api.Tests;

/// <summary>Administration : santé (ADM-20, ADM-21), journal d'audit global et export (ADM-30, ADM-31).</summary>
[Collection("api")]
public class AdminHealthAuditTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    [Fact]
    public async Task ADM_20_la_sante_liste_chaque_composant_sans_secret()
    {
        var health = await Json(await api.Client("Admin", "admin.adm20").GetAsync("/api/admin/health"));
        var components = health.GetProperty("components").EnumerateArray()
            .ToDictionary(c => c.GetProperty("name").GetString()!, c => c.GetProperty("state").GetString()!);
        Assert.Equal("OK", components["Base de données"]);
        // L'annuaire de la configuration de test n'existe pas : hors service, sans faire échouer la page.
        Assert.Equal("Hors service", components["Annuaire"]);
        Assert.Equal("OK", components["Coffre de la découverte"]);
        Assert.Contains("Notifications datées", components.Keys);
        Assert.Equal("Hors service", health.GetProperty("state").GetString());
        Assert.False(string.IsNullOrEmpty(health.GetProperty("version").GetString()));
        var raw = health.GetRawText();
        Assert.DoesNotContain("Password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cle-du-coffre", raw);

        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client("Manager", "chef.adm20").GetAsync("/api/admin/health")).StatusCode);
    }

    [Fact]
    public async Task ADM_30_ADM_31_journal_global_filtre_pagine_et_exporte()
    {
        var author = api.Client("User", "auteur.adm30");
        var created = await Json(await author.PostAsJsonAsync("/api/incidents", new
        {
            title = "Journal adm30", description = "d", impact = "Faible", urgency = "Faible", category = "Test", affectedService = "Test"
        }));
        var reference = created.GetProperty("reference").GetString()!;
        var admin = api.Client("Admin", "admin.adm30");

        var page = await Json(await admin.GetAsync("/api/admin/audit?author=auteur.adm30&type=incident"));
        Assert.True(page.GetProperty("total").GetInt32() >= 1);
        Assert.All(page.GetProperty("items").EnumerateArray(), i => Assert.Equal("incident", i.GetProperty("entityType").GetString()));
        Assert.Contains(page.GetProperty("items").EnumerateArray(), i => i.GetProperty("reference").GetString() == reference
            && i.GetProperty("action").GetString() == "Création");
        Assert.Equal(0, (await Json(await admin.GetAsync("/api/admin/audit?author=auteur.adm30&type=change"))).GetProperty("total").GetInt32());
        // Période passée : rien.
        Assert.Equal(0, (await Json(await admin.GetAsync("/api/admin/audit?author=auteur.adm30&from=2020-01-01&to=2020-01-31"))).GetProperty("total").GetInt32());

        var export = await admin.GetAsync($"/api/admin/audit/export?reference={reference}");
        Assert.Equal("text/csv", export.Content.Headers.ContentType!.MediaType);
        var csv = await export.Content.ReadAsStringAsync();
        Assert.StartsWith("Date;Auteur;Type;Référence;Action", csv.TrimStart('﻿'));
        Assert.Contains(reference, csv);

        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client("Manager", "chef.adm30").GetAsync("/api/admin/audit")).StatusCode);
    }
}
