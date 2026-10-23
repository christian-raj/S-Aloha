using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SAloha.Api.Tests;

/// <summary>Notifications par courriel (SOC-22) : choix de l'utilisateur, envoi immédiat ou résumé quotidien.</summary>
[Collection("api")]
public class MailNotificationsTests(ApiFixture api, FakeSmtpServer smtp) : IClassFixture<FakeSmtpServer>
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static int Id(JsonElement e) => e.GetProperty("id").GetInt32();

    private Task<HttpResponseMessage> Smtp(bool enabled) => api.Client("Admin", "admin.soc22").PutAsJsonAsync("/api/admin/settings/mail", new
    {
        enabled, host = "127.0.0.1", port = smtp.Port, security = "Aucun", fromAddress = "itsm@corp.local", fromName = "S-Aloha", publicUrl = "https://itsm.corp.local/"
    });

    private async Task Run() => await Json(await api.Client("Admin", "admin.soc22").PostAsync("/api/notifications/run", null));
    private List<string> To(string address) => smtp.Messages.Where(m => m.Contains(address)).ToList();

    [Fact]
    public async Task SOC_22_courriels_immediats_ou_resume_quotidien_selon_le_choix_de_chacun()
    {
        var reader = api.Client("User", "lecteur.soc22", "GRP-SOC22");
        Assert.Equal(HttpStatusCode.BadRequest, (await reader.PutAsJsonAsync("/api/me/mail-preferences", new { mode = "Toujours", address = "a@b.c" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await reader.PutAsJsonAsync("/api/me/mail-preferences", new { mode = "Immédiat" })).StatusCode);
        Assert.Equal("Aucun", (await Json(await reader.GetAsync("/api/me/mail-preferences"))).GetProperty("mode").GetString());
        var saved = await Json(await reader.PutAsJsonAsync("/api/me/mail-preferences", new { mode = "Immédiat", address = "lecteur.soc22@corp.local" }));
        Assert.Equal(("Immédiat", "lecteur.soc22@corp.local"), (saved.GetProperty("mode").GetString(), saved.GetProperty("address").GetString()));

        await Json(await Smtp(true));
        try
        {
            // Confié à lui, puis à son groupe : deux notifications, envoyées au relevé suivant.
            var m = api.Client("Manager", "chef.soc22");
            object Inc(string title, string type, string owner) => new { title, impact = "Faible", urgency = "Faible", ownerType = type, ownerId = owner, ownerDisplayName = owner };
            var mine = await Json(await m.PostAsJsonAsync("/api/incidents", Inc("Imprimante du 2e étage", "User", "lecteur.soc22")));
            await Json(await m.PostAsJsonAsync("/api/incidents", Inc("Scanner du 2e étage", "Group", "GRP-SOC22")));
            await Run();
            var mails = To("lecteur.soc22@corp.local");
            var mail = Assert.Single(mails);
            Assert.Contains(mine.GetProperty("reference").GetString()!, mail);
            Assert.Contains($"https://itsm.corp.local/incidents/{Id(mine)}", mail);
            Assert.Contains("Scanner du 2e", mail);
            await Run();
            Assert.Single(To("lecteur.soc22@corp.local"));   // rien de nouveau : pas de second courriel

            // Résumé quotidien : rien avant 24 h, puis un résumé.
            var daily = api.Client("User", "resume.soc22");
            await Json(await daily.PutAsJsonAsync("/api/me/mail-preferences", new { mode = "Résumé quotidien", address = "resume.soc22@corp.local" }));
            await Json(await m.PostAsJsonAsync("/api/incidents", Inc("Badge refusé", "User", "resume.soc22")));
            await Run();
            Assert.Empty(To("resume.soc22@corp.local"));
            await using (var db = api.Database())
            {
                var p = await db.MailPreferences.SingleAsync(x => x.Username == "resume.soc22");
                p.SentUntil = DateTime.UtcNow.AddHours(-25);
                await db.SaveChangesAsync();
            }
            await Run();
            Assert.Contains("Badge refus", Assert.Single(To("resume.soc22@corp.local")));   // objet et accents encodés en MIME : on cherche une partie ASCII

            // « Aucun » : plus rien ; le choix sans adresse n'est pas gardé.
            await Json(await reader.PutAsJsonAsync("/api/me/mail-preferences", new { mode = "Aucun" }));
            await using var check = api.Database();
            Assert.False(await check.MailPreferences.AnyAsync(x => x.Username == "lecteur.soc22"));
        }
        finally { await Json(await Smtp(false)); }
    }
}
