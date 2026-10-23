using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
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

    /// <summary>PRB-32 : jours sans activité au-delà desquels un problème ouvert remonte aux gestionnaires.</summary>
    public const int IdleDays = 30;

    /// <summary>
    /// PRB-32 : dernière activité de chaque problème ouvert (ni Résolu ni Clos) — modification du
    /// problème, analyse, action créée ou terminée, commentaire, ligne du journal d'audit —, pour
    /// ceux qui n'en ont aucune depuis <see cref="IdleDays"/> jours.
    /// </summary>
    public static async Task<List<(Problem Problem, DateTime LastActivity)>> IdleAsync(AppDbContext db, DateTime now, Func<int, bool> inScope)
    {
        var limit = now.AddDays(-IdleDays);
        var candidates = (await db.Problems.AsNoTracking()
            .Where(p => p.Status != "Résolu" && p.Status != "Clos" && p.UpdatedAt < limit).ToListAsync()).Where(p => inScope(p.PerimeterId)).ToList();
        if (candidates.Count == 0) return [];
        var ids = candidates.Select(p => p.Id).ToList();
        var dates = new List<(int Id, DateTime At)>();
        dates.AddRange((await db.Analyses.AsNoTracking().Where(a => ids.Contains(a.ProblemId)).Select(a => new { a.ProblemId, a.UpdatedAt }).ToListAsync())
            .Select(a => (a.ProblemId, a.UpdatedAt)));
        dates.AddRange((await db.Actions.AsNoTracking().Where(a => ids.Contains(a.ProblemId)).Select(a => new { a.ProblemId, a.CreatedAt, a.CompletedAt }).ToListAsync())
            .Select(a => (a.ProblemId, a.CompletedAt ?? a.CreatedAt)));
        dates.AddRange((await db.Comments.AsNoTracking().Where(c => c.TargetType == "problem" && ids.Contains(c.TargetId)).Select(c => new { c.TargetId, c.CreatedAt }).ToListAsync())
            .Select(c => (c.TargetId, c.CreatedAt)));
        dates.AddRange((await db.AuditEntries.AsNoTracking().Where(a => a.EntityType == "problem" && ids.Contains(a.EntityId)).GroupBy(a => a.EntityId)
                .Select(g => new { Id = g.Key, At = g.Max(a => a.At) }).ToListAsync())
            .Select(a => (a.Id, a.At)));
        var latest = dates.GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.Max(d => d.At));
        return candidates.Select(p => (p, Last: latest.TryGetValue(p.Id, out var at) && at > p.UpdatedAt ? at : p.UpdatedAt))
            .Where(x => x.Last < limit).OrderBy(x => x.Last).ToList();
    }

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
