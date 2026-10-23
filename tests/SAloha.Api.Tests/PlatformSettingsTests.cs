using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace SAloha.Api.Tests;

/// <summary>Serveur SMTP factice : accepte une authentification PLAIN et garde les messages reçus.</summary>
public sealed class FakeSmtpServer : IAsyncLifetime
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public ConcurrentQueue<string> Messages { get; } = new();
    public ConcurrentQueue<string> Logins { get; } = new();

    public Task InitializeAsync()
    {
        _listener.Start();
        _ = Task.Run(async () =>
        {
            while (!_stop.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await _listener.AcceptTcpClientAsync(_stop.Token); } catch { return; }
                _ = Task.Run(() => Serve(client));
            }
        });
        return Task.CompletedTask;
    }

    private async Task Serve(TcpClient client)
    {
        using var _ = client;
        var stream = client.GetStream();
        var reader = new StreamReader(stream, Encoding.ASCII);
        var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
        await writer.WriteLineAsync("220 smtp-test ESMTP");
        while (await reader.ReadLineAsync() is { } line)
        {
            var verb = line.Split(' ')[0].ToUpperInvariant();
            switch (verb)
            {
                case "EHLO": await writer.WriteLineAsync("250-smtp-test\r\n250 AUTH PLAIN"); break;
                case "AUTH":
                    var plain = Encoding.UTF8.GetString(Convert.FromBase64String(line.Split(' ')[2])).Split('\0');
                    Logins.Enqueue($"{plain[1]}:{plain[2]}");
                    await writer.WriteLineAsync("235 2.7.0 Authentication successful"); break;
                case "DATA":
                    await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                    var data = new StringBuilder();
                    while (await reader.ReadLineAsync() is { } l && l != ".") data.AppendLine(l);
                    Messages.Enqueue(data.ToString());
                    await writer.WriteLineAsync("250 2.0.0 Ok"); break;
                case "QUIT": await writer.WriteLineAsync("221 Bye"); return;
                default: await writer.WriteLineAsync("250 Ok"); break;
            }
        }
    }

    public Task DisposeAsync() { _stop.Cancel(); _listener.Stop(); return Task.CompletedTask; }
}

/// <summary>Stockage S3 factice, adressage par chemin, comme Garage : objets en mémoire, clé d'accès vérifiée.</summary>
public sealed class FakeS3Server : IAsyncLifetime
{
    public const string AccessKey = "GKessai0123", Bucket = "pieces-jointes";
    private WebApplication _app = null!;
    public string Url { get; private set; } = "";
    public ConcurrentDictionary<string, byte[]> Objects { get; } = new();
    public int Puts;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0));
        _app = builder.Build();
        bool Signed(HttpContext c) => c.Request.Headers.Authorization.ToString().Contains($"Credential={AccessKey}/");
        IResult Denied() => Results.Content("<Error><Code>InvalidAccessKeyId</Code><Message>Clé d'accès inconnue</Message></Error>", "application/xml", statusCode: 403);
        _app.MapPut("/{bucket}/{**key}", async (HttpContext c, string bucket, string key) =>
        {
            if (!Signed(c)) return Denied();
            using var ms = new MemoryStream();
            await c.Request.Body.CopyToAsync(ms);
            Objects[$"{bucket}/{key}"] = ms.ToArray();
            Interlocked.Increment(ref Puts);
            c.Response.Headers.ETag = "\"essai\"";
            return Results.Ok();
        });
        _app.MapGet("/{bucket}/{**key}", (HttpContext c, string bucket, string key) =>
            !Signed(c) ? Denied()
            : Objects.TryGetValue($"{bucket}/{key}", out var data) ? Results.Bytes(data, "application/octet-stream")
            : Results.Content("<Error><Code>NoSuchKey</Code></Error>", "application/xml", statusCode: 404));
        _app.MapDelete("/{bucket}/{**key}", (HttpContext c, string bucket, string key) =>
        {
            if (!Signed(c)) return Denied();
            Objects.TryRemove($"{bucket}/{key}", out var _);
            return Results.NoContent();
        });
        await _app.StartAsync();
        Url = _app.Urls.Single();
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();
}

/// <summary>Messagerie et stockage, réglés dans la console d'administration (ADM-60 à ADM-63, ADR-0020).</summary>
[Collection("api")]
public class PlatformSettingsTests(ApiFixture api, FakeSmtpServer smtp, FakeS3Server s3) : IClassFixture<FakeSmtpServer>, IClassFixture<FakeS3Server>
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"HTTP {(int)res.StatusCode} : {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private HttpClient Admin => api.Client("Admin", "admin.adm60");

    private object Mail(string? password, bool enabled = true, string security = "Aucun", string host = "127.0.0.1") => new
    {
        enabled, host, port = smtp.Port, security, username = "relais", password, fromAddress = "itsm@corp.local", fromName = "S-Aloha"
    };

    [Fact]
    public async Task ADM_60_62_smtp_regle_par_l_admin_mot_de_passe_chiffre_et_courriel_d_essai()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client("Manager").GetAsync("/api/admin/settings/mail")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.PutAsJsonAsync("/api/admin/settings/mail", Mail("x", security: "SSLv3"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.PutAsJsonAsync("/api/admin/settings/mail", Mail("x", host: ""))).StatusCode);

        var saved = await Json(await Admin.PutAsJsonAsync("/api/admin/settings/mail", Mail("mot-de-passe-smtp")));
        Assert.True(saved.GetProperty("hasPassword").GetBoolean());
        Assert.False(saved.TryGetProperty("password", out _));
        Assert.DoesNotContain("mot-de-passe-smtp", await (await Admin.GetAsync("/api/admin/settings/mail")).Content.ReadAsStringAsync());
        await using (var db = api.Database())
        {
            Assert.StartsWith("v1:", (await db.MailSettings.SingleAsync()).EncryptedPassword);
            var audit = await db.AuditEntries.Where(a => a.EntityType == "parametre" && a.Reference == "Messagerie").ToListAsync();
            Assert.Contains(audit, a => a.Field == "password" && a.NewValue == "modifié");
            Assert.DoesNotContain(audit, a => (a.NewValue ?? "").Contains("mot-de-passe-smtp"));
        }

        // Courriel d'essai : authentification avec le mot de passe déchiffré.
        var test = await Json(await Admin.PostAsJsonAsync("/api/admin/settings/mail/test", new { to = "admin@corp.local" }));
        Assert.True(test.GetProperty("ok").GetBoolean(), test.GetProperty("message").GetString());
        Assert.Contains("relais:mot-de-passe-smtp", smtp.Logins);
        Assert.Contains(smtp.Messages, m => m.Contains("courriel d'essai", StringComparison.OrdinalIgnoreCase) && m.Contains("admin@corp.local"));
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.PostAsJsonAsync("/api/admin/settings/mail/test", new { to = "pas-une-adresse" })).StatusCode);

        // Mot de passe absent : inchangé ; vide : effacé. Serveur injoignable : échec expliqué, pas d'erreur 500.
        Assert.True((await Json(await Admin.PutAsJsonAsync("/api/admin/settings/mail", Mail(null)))).GetProperty("hasPassword").GetBoolean());
        await Json(await Admin.PutAsJsonAsync("/api/admin/settings/mail", Mail("", security: "TLS")));
        var failed = await Json(await Admin.PostAsJsonAsync("/api/admin/settings/mail/test", new { to = "admin@corp.local" }));
        Assert.False(failed.GetProperty("ok").GetBoolean());
        Assert.False((await Json(await Admin.PutAsJsonAsync("/api/admin/settings/mail", Mail(null, enabled: false)))).GetProperty("hasPassword").GetBoolean());
    }

    private object Storage(string? secret, bool enabled = true, string? bucket = FakeS3Server.Bucket, string accessKey = FakeS3Server.AccessKey) => new
    {
        enabled, endpoint = s3.Url, region = "garage", bucket, accessKey, secretKey = secret, pathStyle = true, maxFileMb = 10
    };

    [Fact]
    public async Task ADM_61_63_stockage_s3_regle_par_l_admin_et_essai_de_depot()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.PutAsJsonAsync("/api/admin/settings/storage", Storage("s", bucket: "Pièces"))).StatusCode);
        // Base partagée : un autre test a pu laisser une clé secrète ; on part sans.
        await Json(await Admin.PutAsJsonAsync("/api/admin/settings/storage", Storage("", enabled: false)));
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.PutAsJsonAsync("/api/admin/settings/storage", Storage(null))).StatusCode);   // pas de clé secrète
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.PutAsJsonAsync("/api/admin/settings/storage",
            new { enabled = false, endpoint = "file:///etc", bucket = "b-1", accessKey = "", maxFileMb = 10 })).StatusCode);

        var saved = await Json(await Admin.PutAsJsonAsync("/api/admin/settings/storage", Storage("cle-secrete-s3")));
        Assert.True(saved.GetProperty("hasSecretKey").GetBoolean());
        Assert.DoesNotContain("cle-secrete-s3", saved.GetRawText());
        var test = await Json(await Admin.PostAsync("/api/admin/settings/storage/test", null));
        Assert.True(test.GetProperty("ok").GetBoolean(), test.GetProperty("message").GetString());
        Assert.True(s3.Puts > 0);
        Assert.Empty(s3.Objects);   // l'objet d'essai est supprimé

        var health = await Json(await Admin.GetAsync("/api/admin/health"));
        Assert.Contains(health.GetProperty("components").EnumerateArray(), c =>
            c.GetProperty("name").GetString() == "Stockage des pièces jointes (S3)" && c.GetProperty("message").GetString()!.Contains(FakeS3Server.Bucket));

        // Clé d'accès refusée par le stockage : échec expliqué.
        await Json(await Admin.PutAsJsonAsync("/api/admin/settings/storage", Storage(null, accessKey: "GKinconnue")));
        Assert.False((await Json(await Admin.PostAsync("/api/admin/settings/storage/test", null))).GetProperty("ok").GetBoolean());
        await Json(await Admin.PutAsJsonAsync("/api/admin/settings/storage", Storage(null, enabled: false)));
    }
}
