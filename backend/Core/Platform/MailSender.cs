using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using SAloha.Api.Core.Data;
using SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

namespace SAloha.Api.Core.Platform;

/// <summary>
/// Envoi des courriels par le SMTP réglé dans la console (ADM-60) : sans chiffrement,
/// STARTTLS ou TLS implicite ; authentification si un compte est saisi. Le certificat du
/// serveur est toujours vérifié.
/// </summary>
public class MailSender(AppDbContext db, CredentialVault vault)
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public async Task<MailSettings?> SettingsAsync() => await db.MailSettings.AsNoTracking().FirstOrDefaultAsync();

    /// <summary>SMTP activé et complet : les courriels partent.</summary>
    public static bool Ready(MailSettings? s) => s is { Enabled: true } && s.Host != "" && s.FromAddress != "";

    /// <summary>Envoie un message texte ; lève une exception si le serveur le refuse ou est injoignable.</summary>
    public async Task SendAsync(MailSettings s, IEnumerable<string> to, string subject, string body, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(s.FromName, s.FromAddress));
        foreach (var address in to) message.To.Add(MailboxAddress.Parse(address));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient { Timeout = (int)Timeout.TotalMilliseconds };
        var security = s.Security switch
        {
            MailSettings.Tls => SecureSocketOptions.SslOnConnect,
            MailSettings.StartTls => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.None,
        };
        await client.ConnectAsync(s.Host, s.Port, security, ct);
        if (!string.IsNullOrWhiteSpace(s.Username))
            await client.AuthenticateAsync(s.Username, s.EncryptedPassword is null ? "" : vault.Unprotect(s.EncryptedPassword), ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
