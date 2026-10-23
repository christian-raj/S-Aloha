using System.Text;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Perimeters;
using SAloha.Api.Core.Platform;

namespace SAloha.Api.Core.Notifications;

/// <summary>
/// SOC-22 : envoi par courriel des notifications, au relevé périodique. « Immédiat » : les
/// nouvelles notifications depuis le dernier envoi, à chaque relevé ; « Résumé quotidien » :
/// une fois par jour au plus. Mêmes notifications que la cloche (SOC-21, SOC-23), une par
/// événement, 24 heures au plus en arrière. SMTP non activé : rien ne part, rien n'est perdu.
/// Un envoi en échec est réessayé au relevé suivant.
/// </summary>
public class MailDispatch(AppDbContext db, MailSender mail, ILogger<MailDispatch> logger)
{
    public const int MaxItems = 50;

    public async Task<int> RunAsync(DateTime now)
    {
        var settings = await mail.SettingsAsync();
        if (!MailSender.Ready(settings)) return 0;
        var sent = 0;
        foreach (var p in await db.MailPreferences.Where(x => x.Mode != MailPreference.None && x.Address != null).ToListAsync())
        {
            if (p.Mode == MailPreference.Daily && now - p.SentUntil < TimeSpan.FromHours(24)) continue;
            var since = p.SentUntil > now.AddHours(-24) ? p.SentUntil : now.AddHours(-24);
            var groups = p.GroupList();
            var all = p.Role == "Admin";
            var roles = all ? new Dictionary<int, string>() : await PerimeterAccess.RolesAsync(db, p.Username, groups, p.Role == "Manager" ? "Manager" : "User");
            var rows = await Notification.VisibleTo(db, p.Username, groups, all, roles.Keys.ToList(),
                    roles.Where(r => r.Value == "Manager").Select(r => r.Key).ToList(), since)
                .Where(n => n.CreatedAt < now).OrderBy(n => n.CreatedAt).ToListAsync();
            var items = rows.GroupBy(n => (n.TargetType, n.TargetId, n.Event)).Select(g => g.First()).ToList();
            if (items.Count == 0) { if (p.Mode == MailPreference.Daily) p.SentUntil = now; continue; }
            try
            {
                await mail.SendAsync(settings!, [p.Address!], Subject(p, items.Count), Body(settings!, p, items));
                p.SentUntil = now;
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Le message d'erreur de MailKit ne porte pas le mot de passe ; l'adresse du destinataire n'est pas journalisée.
                logger.LogWarning("Courriel de notifications non envoyé ({Mode}) : {Error}", p.Mode, ex.Message.Split('\n')[0]);
            }
        }
        await db.SaveChangesAsync();
        return sent;
    }

    private static string Subject(MailPreference p, int count) => p.Mode == MailPreference.Daily
        ? $"S-Aloha : résumé du jour, {count} notification(s)"
        : $"S-Aloha : {count} nouvelle(s) notification(s)";

    private static readonly Dictionary<string, string> Paths = new()
    {
        ["incident"] = "incidents", ["request"] = "requests", ["problem"] = "problems", ["change"] = "changes", ["ci"] = "configuration",
        ["agreement"] = "agreements", ["article"] = "knowledge", ["improvement"] = "improvements", ["assessment"] = "assessments",
        ["project"] = "projects", ["notification"] = "regulatory-notifications", ["service"] = "services",
    };

    private static string Body(MailSettings s, MailPreference p, List<Notification> items)
    {
        var b = new StringBuilder();
        b.AppendLine($"Bonjour {p.DisplayName},").AppendLine();
        b.AppendLine(p.Mode == MailPreference.Daily ? "Vos notifications des dernières 24 heures :" : "Vos nouvelles notifications :").AppendLine();
        foreach (var n in items.Take(MaxItems))
        {
            b.AppendLine($"- {n.Reference} « {n.Title} » : {n.Event} ({n.ActorDisplayName}, {n.CreatedAt:dd/MM/yyyy HH:mm} UTC)");
            if (!string.IsNullOrEmpty(s.PublicUrl) && Paths.TryGetValue(n.TargetType, out var path)) b.AppendLine($"  {s.PublicUrl}/{path}/{n.TargetId}");
        }
        if (items.Count > MaxItems) b.AppendLine($"… et {items.Count - MaxItems} autre(s), dans l'application.");
        b.AppendLine().AppendLine("Ces courriels se règlent dans S-Aloha, menu « Mes préférences ».");
        return b.ToString();
    }
}
