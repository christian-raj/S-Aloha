using SAloha.Api.Core.Itil;

namespace SAloha.Api.Tests;

/// <summary>SLM-14 : heures de service structurées (fonctions pures, sans base).</summary>
public class ServiceCalendarTests
{
    private static ServiceCalendar Cal(string days = "1,2,3,4,5", string? open = "08:00", string? close = "18:00",
        string holidays = "", string? zone = null)
    {
        var (c, error) = ServiceCalendar.Parse(days, open, close, holidays, zone);
        Assert.Null(error);
        return c!;
    }

    private static DateTime Utc(int y, int mo, int d, int h, int mi = 0) => new(y, mo, d, h, mi, 0, DateTimeKind.Utc);

    [Fact]
    public void Scenario_SLM_14_vendredi_17_h_plus_24_h_de_service_donne_mercredi_11_h()
    {
        // 16/10/2026 est un vendredi : 1 h vendredi + 10 h lundi + 10 h mardi + 3 h mercredi.
        Assert.Equal(Utc(2026, 10, 21, 11), Cal().Add(Utc(2026, 10, 16, 17), TimeSpan.FromHours(24)));
    }

    [Fact]
    public void Hors_plage_le_decompte_part_de_l_ouverture_suivante_et_saute_les_feries()
    {
        // Samedi 10 h : rien avant lundi 8 h ; lundi 19/10 férié → mardi 8 h + 2 h.
        Assert.Equal(Utc(2026, 10, 20, 10), Cal(holidays: "2026-10-19").Add(Utc(2026, 10, 17, 10), TimeSpan.FromHours(2)));
        // Avant l'ouverture un jour ouvré : départ à 8 h.
        Assert.Equal(Utc(2026, 10, 16, 9), Cal().Add(Utc(2026, 10, 16, 6), TimeSpan.FromHours(1)));
    }

    [Fact]
    public void Sans_calendrier_24_7_et_jours_entiers_sans_plage()
    {
        Assert.True(ServiceCalendar.AlwaysOpen.IsAlwaysOpen);
        Assert.Equal(Utc(2026, 10, 18, 5), ServiceCalendar.AlwaysOpen.Add(Utc(2026, 10, 17, 5), TimeSpan.FromHours(24)));
        // Jours ouvrés sans plage : journées entières, week-end exclu.
        Assert.Equal(Utc(2026, 10, 19, 12), Cal(open: null, close: null).Add(Utc(2026, 10, 16, 12), TimeSpan.FromHours(24)));
    }

    [Fact]
    public void Between_compte_les_seules_heures_de_service()
    {
        // Vendredi 17 h → lundi 9 h : 1 h + 1 h.
        Assert.Equal(TimeSpan.FromHours(2), Cal().Between(Utc(2026, 10, 16, 17), Utc(2026, 10, 19, 9)));
        Assert.Equal(TimeSpan.Zero, Cal().Between(Utc(2026, 10, 17, 9), Utc(2026, 10, 17, 12)));   // samedi
        Assert.Equal(TimeSpan.FromHours(3), ServiceCalendar.AlwaysOpen.Between(Utc(2026, 10, 17, 9), Utc(2026, 10, 17, 12)));
    }

    [Fact]
    public void Fuseau_horaire_et_heure_d_ete()
    {
        // Paris (UTC+2 en octobre) : 9 h locale = 7 h UTC.
        var paris = Cal(zone: "Europe/Paris");
        Assert.Equal(Utc(2026, 10, 16, 7), paris.Add(Utc(2026, 10, 16, 4), TimeSpan.FromHours(1)));
        // Vendredi 23/10, 18 h locale (fermé) ; heure d'hiver le 25/10 : lundi 26, 8 h locale = 7 h UTC, + 1 h.
        Assert.Equal(Utc(2026, 10, 26, 8), paris.Add(Utc(2026, 10, 23, 16), TimeSpan.FromHours(1)));
        // Service ouvert 24 h sur un jour de passage à l'heure d'été (29/03/2026, 2 h → 3 h) : pas d'exception.
        var always = Cal(days: "1,2,3,4,5,6,7", open: "00:00", close: "23:59", zone: "Europe/Paris");
        _ = always.Add(Utc(2026, 3, 29, 0, 30), TimeSpan.FromHours(3));
    }

    [Theory]
    [InlineData("8", null, null, "", null)]
    [InlineData("1", "08:00", null, "", null)]
    [InlineData("1", "18:00", "08:00", "", null)]
    [InlineData("1", "8h", "18h", "", null)]
    [InlineData("1", null, null, "25/12/2026", null)]
    [InlineData("1", null, null, "", "Mars/Olympus")]
    public void Saisies_invalides_refusees(string days, string? open, string? close, string holidays, string? zone)
    {
        var (c, error) = ServiceCalendar.Parse(days, open, close, holidays, zone);
        Assert.Null(c);
        Assert.NotNull(error);
    }
}
