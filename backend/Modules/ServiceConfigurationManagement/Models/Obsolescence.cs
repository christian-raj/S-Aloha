namespace SAloha.Api.Modules.ServiceConfigurationManagement;

/// <summary>
/// CFG-47 : fin de support d'un actif — la date de sa fiche, ou celle du référentiel des fins
/// de support dont le produit apparaît dans son système, son constructeur ou son modèle ; la
/// plus proche l'emporte. État de garantie de la même façon. « Proche » : moins de 6 mois.
/// </summary>
public static class Obsolescence
{
    public const string Passed = "Fin de support dépassée";
    public const string Near = "Fin de support proche";
    public const string Supported = "Supporté";
    public const string Unknown = "Non renseigné";
    public const string WarrantyExpired = "Expirée";
    public const string WarrantyNear = "Expire bientôt";
    public const string WarrantyCovered = "Sous garantie";
    public static readonly TimeSpan Horizon = TimeSpan.FromDays(182);

    public record Support(string State, DateTime? Date, string? Source);

    public static Support Of(ConfigurationItem ci, IReadOnlyCollection<EndOfLifeRule> rules, DateTime today)
    {
        DateTime? date = ci.EndOfSupport?.Date;
        string? source = date is null ? null : "Fiche du CI";
        var text = $"{ci.OperatingSystem} {ci.Vendor} {ci.Model}";
        var rule = rules.Where(r => text.Contains(r.Product, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.EndOfSupport).FirstOrDefault();
        if (rule is not null && (date is null || rule.EndOfSupport < date)) (date, source) = (rule.EndOfSupport.Date, rule.Product);
        return new Support(State(date, today, Passed, Near, Supported), date, source);
    }

    public static string Warranty(ConfigurationItem ci, DateTime today) =>
        State(ci.WarrantyExpiry?.Date, today, WarrantyExpired, WarrantyNear, WarrantyCovered);

    private static string State(DateTime? date, DateTime today, string passed, string near, string ok) =>
        date is not { } d ? Unknown : d < today ? passed : d < today + Horizon ? near : ok;
}
