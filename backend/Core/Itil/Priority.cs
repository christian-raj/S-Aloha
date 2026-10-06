namespace SAloha.Api.Core.Itil;

/// <summary>Priorité ITIL P1–P4 dérivée de la matrice impact × urgence.</summary>
public static class Priority
{
    public static readonly string[] Impacts = ["Faible", "Moyen", "Élevé"];
    public static readonly string[] Urgencies = ["Faible", "Moyenne", "Élevée"];

    public static string Compute(string impact, string urgency)
    {
        int i = impact switch { "Élevé" => 3, "Moyen" => 2, _ => 1 };
        int u = urgency switch { "Élevée" => 3, "Moyenne" => 2, _ => 1 };
        return (i + u) switch { 6 => "P1", 5 => "P2", 4 => "P3", _ => "P4" };
    }
}
