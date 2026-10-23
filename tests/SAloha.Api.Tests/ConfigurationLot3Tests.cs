using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SAloha.Api.Tests;

/// <summary>Configuration, lot 3 : attributs par type (CFG-16), import en masse (CFG-30).</summary>
[Collection("api")]
public class ConfigurationLot3Tests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();

    [Fact]
    public async Task CFG_16_attributs_propres_au_type_verifies_et_gardes()
    {
        var m = api.Client("Manager", "chef.cfg16");
        Assert.Contains("memoryGb", (await Json(await m.GetAsync("/api/configuration-items/attribute-types"))).GetProperty("Serveur").GetRawText());
        object Ci(string type, object? attributes) => new { title = $"srv-cfg16-{type.Length}", ciType = type, environment = "Recette", status = "Planifié", attributes };
        Assert.Equal(HttpStatusCode.BadRequest, (await m.PostAsJsonAsync("/api/configuration-items", Ci("Serveur", new { couleur = "bleu" }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await m.PostAsJsonAsync("/api/configuration-items", Ci("Serveur", new { memoryGb = "beaucoup" }))).StatusCode);
        var ci = await Json(await m.PostAsJsonAsync("/api/configuration-items", Ci("Serveur", new { cpu = "8", memoryGb = "31,5", role = "  ", hypervisor = "pve1" })));
        var attrs = ci.GetProperty("attributes");
        Assert.Equal(("8", "31.5", "pve1"), (attrs.GetProperty("cpu").GetString(), attrs.GetProperty("memoryGb").GetString(), attrs.GetProperty("hypervisor").GetString()));
        Assert.False(attrs.TryGetProperty("role", out _));   // valeur vide : retirée

        // Saisie sans attributs : gardés ; nouveau type : seuls ceux qu'il définit restent.
        var url = $"/api/configuration-items/{Id(ci)}";
        Assert.Equal(3, (await Json(await m.PutAsJsonAsync(url, Ci("Serveur", null)))).GetProperty("attributes").EnumerateObject().Count());
        var desk = await Json(await m.PutAsJsonAsync(url, new { title = "srv-cfg16-7", ciType = "Poste de travail", environment = "Recette", status = "Planifié" }));
        Assert.Equal(new[] { "cpu", "memoryGb" }, desk.GetProperty("attributes").EnumerateObject().Select(p => p.Name).OrderBy(x => x));
    }

    [Fact]
    public async Task CFG_30_import_en_masse_ligne_a_ligne_sans_enregistrement_partiel()
    {
        var admin = api.Client("Admin", "admin.cfg30");
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client("Manager").PostAsJsonAsync("/api/configuration-items/import", new { csv = "nom;type\nx;Serveur" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/configuration-items/import", new { csv = "hostname;kind\nx;Serveur" })).StatusCode);

        var tag = Guid.NewGuid().ToString("N")[..6];
        var bad = $"Nom;Type;Environnement;Emplacement;Propriétaire;Statut\nsrv-a-{tag};Serveur;Production;Salle 1;;Planifié\nsrv-b-{tag};Frigo;Production;;;Planifié\n"
                  + $"srv-a-{tag};Serveur;Production;;;Planifié\nsrv-c-{tag};Serveur;Production;;;En service\n";
        var refused = await admin.PostAsJsonAsync("/api/configuration-items/import", new { csv = bad });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var report = JsonDocument.Parse(await refused.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(new[] { 3, 4, 5 }, report.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("line").GetInt32()));   // type inconnu, doublon, sans propriétaire
        await using (var db = api.Database())
            Assert.False(await db.ConfigurationItems.AnyAsync(c => c.Title.EndsWith(tag)));   // rien de créé

        var good = $"﻿nom,type,environnement,propriétaire,statut\nsrv-a-{tag},Serveur,Production,,Planifié\n\"srv-b-{tag}, secours\",Serveur,Recette,hery.rakoto,En service\n";
        var dry = await Json(await admin.PostAsJsonAsync("/api/configuration-items/import?dryRun=true", new { csv = good }));
        Assert.Equal(2, dry.GetProperty("ready").GetInt32());
        var done = await Json(await admin.PostAsJsonAsync("/api/configuration-items/import", new { csv = good }));
        Assert.Equal(2, done.GetProperty("created").GetInt32());
        await using var check = api.Database();
        var b = await check.ConfigurationItems.SingleAsync(c => c.Title == $"srv-b-{tag}, secours");
        Assert.Equal(("En service", "Recette", "hery.rakoto"), (b.Status, b.Environment, b.OwnerId));
        Assert.True(await check.AuditEntries.AnyAsync(a => a.EntityType == "ci" && a.EntityId == b.Id && a.Action == "Création"));
    }
}
