using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ProblemManagement;

/// <summary>Valeurs fermées (PRB-02) et règles RACI (PRB-17) du module Problèmes.</summary>
public static class ProblemRules
{
    public static readonly string[] ClosureCodes = ["Corrigé", "Erreur connue acceptée", "Doublon", "Non retenu"];
    public static readonly string[] ActionStatuses = ["À faire", "En cours", "Terminée", "Annulée"];
    public static readonly string[] OpenActionStatuses = ["À faire", "En cours"];
    public static readonly string[] Methods = ["FIVE_WHYS", "ISHIKAWA", "FTA"];
    public static readonly string[] RaciRoles = ["R", "A", "C", "I"];
    public static readonly string[] AssigneeTypes = ["User", "Group"];

    /// <summary>
    /// Codes de clôture permis selon le statut d'origine (PRB-13) : Corrigé depuis
    /// Résolu, Erreur connue acceptée depuis Erreur connue, Doublon ou Non retenu
    /// depuis Nouveau. Une transition forcée d'ailleurs admet tous les codes.
    /// </summary>
    public static string[] ClosureCodesFrom(string from) => from switch
    {
        "Résolu" => ["Corrigé"],
        "Erreur connue" => ["Erreur connue acceptée"],
        "Nouveau" => ["Doublon", "Non retenu"],
        _ => ClosureCodes
    };

    /// <summary>Matrice RACI d'une action, à la création comme à la modification (PRB-17).</summary>
    public static string? CheckRaci(IReadOnlyCollection<RaciDto> raci)
    {
        foreach (var r in raci)
        {
            var error = Allowed.Check("Rôle RACI", r.Role, RaciRoles)
                        ?? Allowed.Check("Type d'affectation", r.AssigneeType, AssigneeTypes);
            if (error is not null) return error;
        }
        if (!raci.Any(r => r.Role == "R")) return "Chaque action doit avoir au moins un Responsable (R).";
        if (raci.Count(r => r.Role == "A") != 1) return "Chaque action doit avoir exactement un Approbateur (A).";
        return null;
    }
}
