using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;

namespace SAloha.Api.Core.Records;

/// <summary>
/// SOC-14 : ce qu'une suppression emporte, lu dans le modèle EF (dépendants supprimés en
/// cascade) plutôt que tenu à la main par pratique ; les liens, commentaires et notifications,
/// sans clé étrangère, sont comptés à part par le socle.
/// </summary>
public static class Deletion
{
    /// <summary>Libellé d'un type de dépendant, au pluriel.</summary>
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["RcaAnalysis"] = "analyses de cause racine", ["CorrectiveAction"] = "actions correctives",
        ["CiRelationship"] = "relations entre CI", ["ServiceLevelAgreement"] = "accords (SLA)",
        ["AgreementReview"] = "revues de SLA", ["AssessmentResponse"] = "réponses au questionnaire",
        ["AssessmentEvidence"] = "preuves", ["ChangeApproval"] = "décisions du CAB",
        ["ProjectPhase"] = "phases et étapes", ["Milestone"] = "jalons", ["Deliverable"] = "livrables",
        ["Sprint"] = "sprints", ["WorkItem"] = "éléments du backlog", ["ProjectRisk"] = "risques",
        ["ProjectIssue"] = "incidents projet",
    };

    public record Impact(string Label, int Count);

    /// <summary>Dépendants supprimés en cascade avec l'enregistrement <paramref name="id"/> de type <typeparamref name="T"/>.</summary>
    public static async Task<List<Impact>> CascadeAsync<T>(AppDbContext db, int id) where T : class
    {
        var counts = new Dictionary<string, int>();
        var entity = db.Model.FindEntityType(typeof(T))!;
        foreach (var fk in entity.GetReferencingForeignKeys().Where(f => f.DeleteBehavior == DeleteBehavior.Cascade))
        {
            var dependent = fk.DeclaringEntityType.ClrType;
            var property = fk.Properties.Single().Name;
            var n = await (Task<int>)CountMethod.MakeGenericMethod(dependent).Invoke(null, [db, property, id])!;
            if (n == 0) continue;
            var label = Labels.GetValueOrDefault(dependent.Name, dependent.Name);
            counts[label] = counts.GetValueOrDefault(label) + n;
        }
        return counts.Select(c => new Impact(c.Key, c.Value)).ToList();
    }

    private static readonly System.Reflection.MethodInfo CountMethod =
        typeof(Deletion).GetMethod(nameof(CountAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

    private static Task<int> CountAsync<TDependent>(AppDbContext db, string property, int id) where TDependent : class =>
        db.Set<TDependent>().CountAsync(x => EF.Property<int>(x, property) == id);
}
