namespace SAloha.Api.Modules.ComplianceAssessment;

/// <summary>
/// Calcul du score de conformité (règles NIS-10 à NIS-14,
/// docs/reference/processus/conformite-nis2.md). Fonctions pures : testées
/// sans base.
/// </summary>
public static class Scoring
{
    public const int Max = 3;

    public record Maturity(int Level, string Name);

    private static readonly Maturity[] Levels =
    [
        new(1, "Initial"), new(2, "Géré"), new(3, "Défini"), new(4, "Maîtrisé et mesuré"), new(5, "Optimisé")
    ];

    /// <summary>NIS-12 : niveau de maturité selon le pourcentage.</summary>
    public static Maturity MaturityOf(double percent) => percent switch
    {
        >= 90 => Levels[4],
        >= 75 => Levels[3],
        >= 50 => Levels[2],
        >= 25 => Levels[1],
        _ => Levels[0]
    };

    /// <summary>
    /// NIS-10/11 : Σ scores / (3 × nombre de réponses notées), en % arrondi au
    /// dixième ; null sans réponse notée (non applicable et sans réponse
    /// exclus). Appliqué tel quel à une thématique, un objectif, un pilier ou
    /// l'évaluation : c'est une moyenne pondérée par le nombre d'exigences
    /// notées, chaque exigence pèse autant.
    /// </summary>
    public static double? Percent(IEnumerable<int?> scores)
    {
        var valid = scores.Where(s => s.HasValue).Select(s => s!.Value).ToList();
        if (valid.Count == 0) return null;
        return Math.Round(valid.Sum() * 100.0 / (Max * valid.Count), 1);
    }

    /// <summary>NIS-13 : écart à la cible (3) d'une exigence notée.</summary>
    public static int Gap(int score) => Max - score;
}
