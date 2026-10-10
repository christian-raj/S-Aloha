using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Links;
using SAloha.Api.Modules.ChangeEnablement;
using SAloha.Api.Modules.ComplianceAssessment;
using SAloha.Api.Modules.ContinualImprovement;
using SAloha.Api.Modules.IncidentManagement;
using SAloha.Api.Modules.KnowledgeManagement;
using SAloha.Api.Modules.ProblemManagement;
using SAloha.Api.Modules.ServiceConfigurationManagement;
using SAloha.Api.Modules.ServiceLevelManagement;
using SAloha.Api.Modules.ServiceRequestManagement;

namespace SAloha.Api.Core.Audit;

/// <summary>
/// Journal d'audit (SOC-20) tenu à chaque enregistrement EF : créations,
/// champs modifiés (ancienne et nouvelle valeur), transitions, suppressions,
/// liens et relations. Les lignes sont écrites une fois l'enregistrement réussi
/// (l'identifiant d'une création n'est connu qu'après).
/// </summary>
public class AuditInterceptor(IHttpContextAccessor http, ILogger<AuditInterceptor> logger) : SaveChangesInterceptor
{
    /// <summary>Clé de HttpContext.Items : motif d'une transition forcée par un Admin.</summary>
    public const string ForcedReasonKey = "audit.forced-reason";
    private const int MaxValue = 2000;

    /// <summary>Ligne en attente ; <c>Entity</c> donne l'identifiant d'une création après l'enregistrement.</summary>
    private sealed record Draft(string Type, object? Entity, int? Id, string? Reference, string Action, string? Field,
        string? Old, string? New, string? Reason, (string Type, int Id)? Other = null);

    private readonly ConditionalWeakTable<DbContext, List<Draft>> _pending = new();

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> result, CancellationToken ct = default)
    {
        Collect(e.Context);
        return base.SavingChangesAsync(e, result, ct);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData e, InterceptionResult<int> result)
    {
        Collect(e.Context);
        return base.SavingChanges(e, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData e, int result, CancellationToken ct = default)
    {
        await FlushAsync(e.Context, ct);
        return await base.SavedChangesAsync(e, result, ct);
    }

    public override int SavedChanges(SaveChangesCompletedEventData e, int result)
    {
        FlushAsync(e.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(e, result);
    }

    /// <summary>Type d'un enregistrement dans les liens et le journal (incident, change…), ou null.</summary>
    private static string? TypeOf(object entity) => entity switch
    {
        Problem => "problem",
        Incident => "incident",
        ServiceRequest => "request",
        Change => "change",
        ConfigurationItem => "ci",
        ItService => "service",
        ServiceLevelAgreement => "agreement",
        KnowledgeArticle => "article",
        Improvement => "improvement",
        Assessment => "assessment",
        _ => null
    };

    private void Collect(DbContext? context)
    {
        if (context is null) return;
        var reason = http.HttpContext?.Items[ForcedReasonKey] as string;
        var drafts = new List<Draft>();
        foreach (var entry in context.ChangeTracker.Entries()
                     .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            switch (entry.Entity)
            {
                case ItemLink l:
                    var linkAction = entry.State == EntityState.Deleted ? AuditEntry.LinkRemoved
                        : entry.State == EntityState.Added ? AuditEntry.LinkAdded : null;
                    if (linkAction is null) break;
                    drafts.Add(new Draft(l.FromType, null, l.FromId, null, linkAction, null, null, null, null, (l.ToType, l.ToId)));
                    drafts.Add(new Draft(l.ToType, null, l.ToId, null, linkAction, null, null, null, null, (l.FromType, l.FromId)));
                    break;
                case CiRelationship r:
                    var relAction = entry.State == EntityState.Deleted ? AuditEntry.LinkRemoved
                        : entry.State == EntityState.Added ? AuditEntry.LinkAdded : null;
                    if (relAction is null) break;
                    drafts.Add(new Draft("ci", null, r.SourceId, null, relAction, r.Type, null, null, null, ("ci", r.TargetId)));
                    break;
                case CorrectiveAction a:
                    drafts.AddRange(Child(entry, "problem", a.ProblemId, $"action « {Cut(a.Title, 60)} »"));
                    break;
                case RcaAnalysis an:
                    drafts.AddRange(Child(entry, "problem", an.ProblemId, $"analyse {an.Method}"));
                    break;
                default:
                    var type = TypeOf(entry.Entity);
                    if (type is not null) drafts.AddRange(Own(entry, type, reason));
                    break;
            }
        }
        if (drafts.Count > 0) _pending.AddOrUpdate(context, drafts);
    }

    /// <summary>Enregistrement d'une pratique : création, suppression, transition, champs modifiés.</summary>
    private static IEnumerable<Draft> Own(EntityEntry entry, string type, string? forcedReason)
    {
        var reference = entry.Property("Reference").CurrentValue as string;
        var id = entry.State == EntityState.Added ? (int?)null : (int)entry.Property("Id").CurrentValue!;
        if (entry.State == EntityState.Added)
        {
            yield return new Draft(type, entry.Entity, null, null, AuditEntry.Creation, null, null,
                entry.Property("Status").CurrentValue as string, null);
            yield break;
        }
        if (entry.State == EntityState.Deleted)
        {
            yield return new Draft(type, null, id, reference, AuditEntry.Deletion, null,
                entry.Property("Status").OriginalValue as string, null, null);
            yield break;
        }
        foreach (var (name, old, now) in Changes(entry))
            yield return name == "status"
                ? new Draft(type, null, id, reference, forcedReason is null ? AuditEntry.Transition : AuditEntry.ForcedTransition,
                    name, old, now, forcedReason)
                : new Draft(type, null, id, reference, AuditEntry.Modification, name, old, now, null);
    }

    /// <summary>Élément rattaché à un enregistrement (action, analyse) : tracé sur l'enregistrement parent.</summary>
    private static IEnumerable<Draft> Child(EntityEntry entry, string type, int parentId, string subject)
    {
        if (entry.State == EntityState.Added)
            yield return new Draft(type, null, parentId, null, AuditEntry.Creation, subject, null, null, null);
        else if (entry.State == EntityState.Deleted)
            yield return new Draft(type, null, parentId, null, AuditEntry.Deletion, subject, null, null, null);
        else
            foreach (var (name, old, now) in Changes(entry))
                yield return new Draft(type, null, parentId, null, AuditEntry.Modification, Cut($"{subject} · {name}", 100),
                    old, now, null);
    }

    /// <summary>
    /// Champs réellement modifiés, hors champs techniques et effets automatiques
    /// (horodatages *At, auteurs *By, identifiants *Id, propriétaire technique).
    /// </summary>
    private static IEnumerable<(string Name, string? Old, string? New)> Changes(EntityEntry entry)
    {
        foreach (var p in entry.Properties.Where(p => p.IsModified))
        {
            var name = p.Metadata.Name;
            if (name is "Reference" or "DataJson" or "OwnerType" || name.EndsWith("At") || name.EndsWith("By")
                || name.EndsWith("Id") || name.EndsWith("ByDisplayName")) continue;
            var (old, now) = (Format(p.OriginalValue), Format(p.CurrentValue));
            if (old == now) continue;
            yield return (char.ToLowerInvariant(name[0]) + name[1..], old, now);
        }
    }

    private static string? Format(object? value) => value switch
    {
        null => null,
        DateTime d => d.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
        bool b => b ? "oui" : "non",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => string.IsNullOrEmpty(value.ToString()) ? null : Cut(value.ToString()!, MaxValue)
    };

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    private async Task FlushAsync(DbContext? context, CancellationToken ct)
    {
        if (context is not AppDbContext db || !_pending.TryGetValue(context, out var drafts)) return;
        _pending.Remove(context);
        try
        {
            var user = http.HttpContext?.User;
            var author = user?.Identity?.Name ?? "système";
            var authorDisplay = user?.FindFirst("displayName")?.Value ?? author;

            var resolved = drafts.Select(d => d with
            {
                Id = d.Id ?? (int?)context.Entry(d.Entity!).Property("Id").CurrentValue,
                Reference = d.Reference ?? (d.Entity is null ? null : context.Entry(d.Entity).Property("Reference").CurrentValue as string)
            }).ToList();
            // Références manquantes (parent d'une action, extrémités d'un lien) : lues dans la base.
            var wanted = resolved.Where(d => d.Reference is null).Select(d => (d.Type, d.Id!.Value))
                .Concat(resolved.Where(d => d.Other is not null).Select(d => d.Other!.Value)).Distinct().ToList();
            var refs = new Dictionary<(string, int), string>();
            foreach (var group in wanted.GroupBy(w => w.Item1))
            {
                var kind = ItemLinks.ByType(group.Key);
                if (kind is null) continue;
                var ids = group.Select(g => g.Item2).ToList();
                foreach (var t in await kind.Query(db).Where(t => ids.Contains(t.Id)).ToListAsync(ct))
                    refs[(group.Key, t.Id)] = t.Reference;
            }

            db.AuditEntries.AddRange(resolved.Select(d =>
            {
                var other = d.Other is null ? null : refs.GetValueOrDefault(d.Other.Value, $"{d.Other.Value.Type} #{d.Other.Value.Id}");
                var removed = d.Action == AuditEntry.LinkRemoved;
                return new AuditEntry
                {
                    EntityType = d.Type, EntityId = d.Id!.Value,
                    Reference = d.Reference ?? refs.GetValueOrDefault((d.Type, d.Id.Value), ""),
                    Action = d.Action, Field = d.Field,
                    OldValue = removed ? other : d.Old, NewValue = removed ? d.New : other ?? d.New,
                    Reason = d.Reason, Author = author, AuthorDisplayName = authorDisplay
                };
            }));
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // L'enregistrement métier est déjà fait : un échec du journal est signalé, pas propagé.
            logger.LogError(ex, "Journal d'audit : écriture en échec.");
        }
    }
}
