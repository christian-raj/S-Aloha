using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SAloha.Api.Tests;

/// <summary>Pièces jointes (SOC-25) dans le stockage compatible S3 factice.</summary>
[Collection("api")]
public class AttachmentsTests(ApiFixture api, FakeS3Server s3) : IClassFixture<FakeS3Server>
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();

    private Task<HttpResponseMessage> Storage(bool enabled, int maxFileMb = 1) => api.Client("Admin", "admin.soc25").PutAsJsonAsync("/api/admin/settings/storage", new
    {
        enabled, endpoint = s3.Url, region = "garage", bucket = FakeS3Server.Bucket, accessKey = FakeS3Server.AccessKey, secretKey = "cle-secrete-s3", pathStyle = true, maxFileMb
    });

    private static Task<HttpResponseMessage> Upload(HttpClient c, int incident, string name, byte[] bytes)
    {
        var form = new MultipartFormDataContent { { new StringContent("incident"), "type" }, { new StringContent(incident.ToString()), "id" } };
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/html");   // le type du navigateur est ignoré
        form.Add(file, "file", name);
        return c.PostAsync("/api/attachments", form);
    }

    [Fact]
    public async Task SOC_25_pieces_jointes_deposees_verifiees_telechargees_et_retirees()
    {
        var agent = api.Client("User", "agent.soc25");
        var inc = await Json(await agent.PostAsJsonAsync("/api/incidents", new { title = "Écran bleu au démarrage", impact = "Faible", urgency = "Faible" }));
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n% rapport de plantage\n");
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(agent, Id(inc), "rapport.pdf", pdf)).StatusCode);   // stockage non activé
        await Json(await Storage(true));
        try
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await Upload(agent, Id(inc), "page.html", Encoding.UTF8.GetBytes("<script>alert(1)</script>"))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Upload(agent, Id(inc), "faux.pdf", Encoding.UTF8.GetBytes("MZ exécutable"))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Upload(agent, Id(inc), "gros.txt", new byte[2 * 1024 * 1024].Select(_ => (byte)'a').ToArray())).StatusCode);   // 1 Mo au plus
            Assert.Equal(HttpStatusCode.NotFound, (await Upload(agent, 999999, "rapport.pdf", pdf)).StatusCode);

            var a = await Json(await Upload(agent, Id(inc), "../../Rapport de plantage.pdf", pdf));
            Assert.Equal(("Rapport de plantage.pdf", "application/pdf"), (a.GetProperty("fileName").GetString(), a.GetProperty("contentType").GetString()));
            var list = await Json(await agent.GetAsync($"/api/attachments?type=incident&id={Id(inc)}"));
            Assert.True(list.GetProperty("enabled").GetBoolean());
            Assert.Single(list.GetProperty("items").EnumerateArray());

            var download = await api.Client("User", "lecteur.soc25").GetAsync($"/api/attachments/{Id(a)}/content");
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            Assert.Equal(pdf, await download.Content.ReadAsByteArrayAsync());
            Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
            Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());

            // Retrait : ni un autre utilisateur, mais l'auteur ; l'objet quitte le stockage ; le journal garde la trace.
            Assert.Equal(HttpStatusCode.Forbidden, (await api.Client("User", "lecteur.soc25").DeleteAsync($"/api/attachments/{Id(a)}")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await agent.DeleteAsync($"/api/attachments/{Id(a)}")).StatusCode);
            Assert.DoesNotContain(s3.Objects.Keys, k => k.Contains($"/incident/{Id(inc)}/"));
            await using (var db = api.Database())
                Assert.Equal(2, await db.AuditEntries.CountAsync(x => x.EntityType == "incident" && x.EntityId == Id(inc) && x.Field == "pièce jointe"));

            // Supprimer l'enregistrement emporte ses pièces jointes, en base et dans le stockage.
            await Json(await Upload(agent, Id(inc), "capture.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3]));
            var admin = api.Client("Admin", "admin.soc25");
            var preview = await Json(await admin.GetAsync($"/api/incidents/{Id(inc)}/deletion"));
            Assert.Contains(preview.GetProperty("impacts").EnumerateArray(), i => i.GetProperty("label").GetString() == "pièces jointes");
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/incidents/{Id(inc)}")).StatusCode);
            await using var check = api.Database();
            Assert.False(await check.Attachments.AnyAsync(x => x.TargetType == "incident" && x.TargetId == Id(inc)));
            Assert.DoesNotContain(s3.Objects.Keys, k => k.Contains($"/incident/{Id(inc)}/"));
        }
        finally { await Json(await Storage(false)); }
    }
}
