using System.Globalization;
using System.Text.RegularExpressions;

namespace SAloha.Api.Core.Itil;

/// <summary>
/// SLM-14 : heures de service structurées d'un service — jours ouvrés (1 = lundi … 7 =
/// dimanche), plage horaire d'ouverture, jours fériés et fuseau horaire. Sans jours ni plage :
/// 24 h/24 et 7 j/7. Tous les délais SLA se comptent dans ces heures (INC-22). Fonctions
/// pures, testées sans base.
/// </summary>
public sealed partial class ServiceCalendar
{
    public static readonly ServiceCalendar AlwaysOpen = new([], null, null, [], TimeZoneInfo.Utc);

    private readonly HashSet<DayOfWeek> _days;
    private readonly TimeSpan? _open, _close;
    private readonly HashSet<DateOnly> _holidays;
    private readonly TimeZoneInfo _zone;

    private ServiceCalendar(HashSet<DayOfWeek> days, TimeSpan? open, TimeSpan? close, HashSet<DateOnly> holidays, TimeZoneInfo zone)
    {
        _days = days; _open = open; _close = close; _holidays = holidays; _zone = zone;
    }

    /// <summary>Ouvert en permanence : ni jours ouvrés, ni plage horaire, ni jours fériés.</summary>
    public bool IsAlwaysOpen => _days.Count == 0 && _open is null && _holidays.Count == 0;

    [GeneratedRegex(@"^([01]\d|2[0-3]):[0-5]\d$")] private static partial Regex Time();

    /// <summary>
    /// Lit et contrôle la saisie : jours « 1,2,3,4,5 », heures « HH:mm » (ouverture avant
    /// fermeture, les deux ou aucune), jours fériés « AAAA-MM-JJ » séparés par des espaces,
    /// virgules ou retours à la ligne, fuseau IANA (UTC par défaut).
    /// </summary>
    public static (ServiceCalendar? Calendar, string? Error) Parse(string? days, string? open, string? close, string? holidays, string? zone)
    {
        var set = new HashSet<DayOfWeek>();
        foreach (var d in (days ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(d, out var n) || n is < 1 or > 7) return (null, $"Jour ouvré invalide : « {d} » (1 = lundi … 7 = dimanche).");
            set.Add((DayOfWeek)(n % 7));
        }
        bool hasOpen = !string.IsNullOrWhiteSpace(open), hasClose = !string.IsNullOrWhiteSpace(close);
        if (hasOpen != hasClose) return (null, "Renseigner l'heure d'ouverture et l'heure de fermeture, ou aucune.");
        TimeSpan? o = null, c = null;
        if (hasOpen)
        {
            if (!Time().IsMatch(open!.Trim()) || !Time().IsMatch(close!.Trim())) return (null, "Heures au format HH:mm (ex. 08:00).");
            o = TimeSpan.Parse(open.Trim(), CultureInfo.InvariantCulture); c = TimeSpan.Parse(close.Trim(), CultureInfo.InvariantCulture);
            if (c <= o) return (null, "L'heure de fermeture doit suivre l'heure d'ouverture.");
        }
        var hol = new HashSet<DateOnly>();
        foreach (var h in (holidays ?? "").Split([',', ' ', '\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!DateOnly.TryParseExact(h, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return (null, $"Jour férié invalide : « {h} » (format AAAA-MM-JJ).");
            hol.Add(date);
        }
        TimeZoneInfo tz;
        try { tz = string.IsNullOrWhiteSpace(zone) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(zone.Trim()); }
        catch (Exception) { return (null, $"Fuseau horaire inconnu : « {zone} » (ex. Europe/Paris, Indian/Antananarivo)."); }
        return (new ServiceCalendar(set, o, c, hol, tz), null);
    }

    /// <summary>Plage ouverte d'un jour local, ou null si le service est fermé ce jour-là.</summary>
    private (DateTime From, DateTime To)? Window(DateTime localDay)
    {
        if (_holidays.Contains(DateOnly.FromDateTime(localDay))) return null;
        if (_days.Count > 0 && !_days.Contains(localDay.DayOfWeek)) return null;
        return (localDay + (_open ?? TimeSpan.Zero), localDay + (_close ?? TimeSpan.FromDays(1)));
    }

    private DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone);
    private DateTime Utc(DateTime local)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        // Heure locale inexistante (passage à l'heure d'été) : l'instant qui suit.
        while (_zone.IsInvalidTime(local)) local = local.AddMinutes(30);
        return TimeZoneInfo.ConvertTimeToUtc(local, _zone);
    }

    /// <summary>Instant UTC atteint après <paramref name="duration"/> d'heures de service depuis <paramref name="startUtc"/>.</summary>
    public DateTime Add(DateTime startUtc, TimeSpan duration)
    {
        if (IsAlwaysOpen || duration <= TimeSpan.Zero) return startUtc + duration;
        var cur = Local(startUtc);
        var left = duration;
        for (var i = 0; i < 3660; i++)   // dix ans de calendrier au plus : un calendrier sans jour ouvert ne boucle pas
        {
            if (Window(cur.Date) is { } w && cur < w.To)
            {
                if (cur < w.From) cur = w.From;
                var available = w.To - cur;
                if (left <= available) return Utc(cur + left);
                left -= available;
            }
            cur = cur.Date.AddDays(1);
        }
        return startUtc + duration;
    }

    /// <summary>Heures de service écoulées entre deux instants UTC (temps passé En attente, INC-22).</summary>
    public TimeSpan Between(DateTime fromUtc, DateTime toUtc)
    {
        if (toUtc <= fromUtc) return TimeSpan.Zero;
        if (IsAlwaysOpen) return toUtc - fromUtc;
        DateTime cur = Local(fromUtc), end = Local(toUtc);
        var total = TimeSpan.Zero;
        for (var i = 0; i < 3660 && cur < end; i++)
        {
            if (Window(cur.Date) is { } w)
            {
                var a = cur > w.From ? cur : w.From;
                var b = end < w.To ? end : w.To;
                if (b > a) total += b - a;
            }
            cur = cur.Date.AddDays(1);
        }
        return total;
    }
}
