using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ServiceConfigurationManagement;

[Route("api/configuration-items")]
public class ConfigurationItemsController(AppDbContext db) : RecordController<ConfigurationItem, ConfigurationItemDto>(db)
{
    protected override string Code => "CI";
    protected override string LinkType => "ci";
    protected override string[] Statuses => ConfigurationItem.Statuses;
    protected override IReadOnlyDictionary<string, string[]> Transitions => ConfigurationItem.Transitions;
    protected override bool StatusChosenAtCreation => true;
    protected override string InitialStatus(ConfigurationItem e) => "En service";

    protected override string? Apply(ConfigurationItem e, ConfigurationItemDto dto, bool creating)
    {
        var error = Allowed.Check("Type de CI", dto.CiType, ConfigurationItem.Types)
                    ?? Allowed.Check("Environnement", dto.Environment, ConfigurationItem.Environments);
        if (error is not null) return error;
        e.CiType = dto.CiType!; e.Environment = dto.Environment!;
        e.Location = dto.Location ?? "";
        return null;
    }

    // CFG-12 : retirer un CI est réservé aux gestionnaires.
    protected override bool RequiresManager(string status) => status == "Retiré";

    // CFG-11 : un CI en service a un propriétaire.
    // CFG-13 : un CI relié à un incident ou changement ouvert ne peut pas être retiré.
    protected override async Task<string?> CheckStatusAsync(ConfigurationItem e, string status)
    {
        if (status == "En service" && string.IsNullOrWhiteSpace(e.OwnerId))
            return "Désigner un propriétaire : il est obligatoire pour un CI en service.";

        if (status == "Retiré")
        {
            var openIncidents = await (
                from link in Db.ItemLinks
                where (link.FromType == "ci" && link.FromId == e.Id && link.ToType == "incident")
                   || (link.ToType == "ci" && link.ToId == e.Id && link.FromType == "incident")
                let incId = link.FromType == "incident" ? link.FromId : link.ToId
                join inc in Db.Incidents on incId equals inc.Id
                where !IncidentManagement.Incident.Done.Contains(inc.Status)
                select inc.Reference
            ).ToListAsync();

            var openChanges = await (
                from link in Db.ItemLinks
                where (link.FromType == "ci" && link.FromId == e.Id && link.ToType == "change")
                   || (link.ToType == "ci" && link.ToId == e.Id && link.FromType == "change")
                let chgId = link.FromType == "change" ? link.FromId : link.ToId
                join chg in Db.Changes on chgId equals chg.Id
                where !ChangeEnablement.Change.Done.Contains(chg.Status)
                select chg.Reference
            ).ToListAsync();

            var openRefs = openIncidents.Concat(openChanges).ToList();
            if (openRefs.Count > 0)
                return $"Ce CI est relié à des enregistrements encore ouverts : {string.Join(", ", openRefs)}.";
        }

        return null;
    }

    // CFG-17 : deux CI non retirés ne portent pas le même nom dans un même environnement.
    protected override async Task<string?> ConflictAsync(ConfigurationItem e)
    {
        if (e.Status == "Retiré") return null;
        var name = e.Title.ToLower();
        var twin = await Db.ConfigurationItems.AsNoTracking()
            .Where(c => c.Id != e.Id && c.Status != "Retiré" && c.Environment == e.Environment && c.Title.ToLower() == name)
            .Select(c => c.Reference).FirstOrDefaultAsync();
        return twin is null ? null : $"Un CI « {e.Title} » existe déjà en {e.Environment} ({twin}).";
    }

    protected override IQueryable<ConfigurationItem> Filter(IQueryable<ConfigurationItem> q)
    {
        var type = Request.Query["type"].ToString();
        return string.IsNullOrEmpty(type) ? q : q.Where(c => c.CiType == type);
    }

    protected override IQueryable<ConfigurationItem> WithDetails(IQueryable<ConfigurationItem> q) =>
        q.Include(c => c.Outgoing).ThenInclude(r => r.Target)
         .Include(c => c.Incoming).ThenInclude(r => r.Source);

    /// <summary>
    /// Vue d'impact transitive : en aval, ce qui est touché si ce CI tombe ;
    /// en amont, ce dont il dépend. Parcours en largeur sur tout le graphe des
    /// relations : chaque CI n'apparaît qu'une fois, à sa plus courte distance,
    /// ce qui arrête aussi le parcours en cas de cycle.
    /// </summary>
    [HttpGet("{id:int}/impact")]
    public async Task<IActionResult> Impact(int id)
    {
        if (!await VisibleAsync(id)) return NotFound();
        var relations = await Db.CiRelationships.AsNoTracking().ToListAsync();
        // Arcs « la panne de From touche To », avec la relation d'origine.
        var impacts = relations.Select(r => CiRelationship.FailureFlowsFromTarget(r.Type)
            ? (From: r.TargetId, To: r.SourceId, r.Type)
            : (From: r.SourceId, To: r.TargetId, r.Type)).ToList();

        var downstream = Traverse(id, impacts.ToLookup(a => a.From, a => (Next: a.To, a.Type)));
        var upstream = Traverse(id, impacts.ToLookup(a => a.To, a => (Next: a.From, a.Type)));

        var ids = downstream.Concat(upstream).SelectMany(n => new[] { n.Id, n.Via }).Distinct().ToList();
        var cis = await Db.ConfigurationItems.AsNoTracking().Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Reference, c.Title, c.CiType, c.Status, c.PerimeterId }).ToDictionaryAsync(c => c.Id);
        // PER-08 : le parcours suit tout le graphe, mais un CI d'un autre périmètre n'est pas décrit.
        var scope = await ScopeAsync();
        string Name(int ciId, string text) => scope.Sees(cis[ciId].PerimeterId) ? text : "CI d'un autre périmètre";
        object Describe(ImpactNode n) => new
        {
            Id = scope.Sees(cis[n.Id].PerimeterId) ? n.Id : 0,
            Reference = Name(n.Id, cis[n.Id].Reference), Title = Name(n.Id, cis[n.Id].Title), cis[n.Id].CiType, cis[n.Id].Status,
            n.Depth, n.Relation,
            Via = new { Id = scope.Sees(cis[n.Via].PerimeterId) ? n.Via : 0, Reference = Name(n.Via, cis[n.Via].Reference), Title = Name(n.Via, cis[n.Via].Title) }
        };
        return Ok(new { Downstream = downstream.Select(Describe), Upstream = upstream.Select(Describe) });
    }

    private record ImpactNode(int Id, int Depth, int Via, string Relation);

    private static List<ImpactNode> Traverse(int start, ILookup<int, (int Next, string Type)> edges)
    {
        var seen = new HashSet<int> { start };
        var result = new List<ImpactNode>();
        var queue = new Queue<(int Id, int Depth)>([(start, 0)]);
        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            foreach (var (next, type) in edges[current])
            {
                if (!seen.Add(next)) continue;
                result.Add(new ImpactNode(next, depth + 1, current, type));
                queue.Enqueue((next, depth + 1));
            }
        }
        return result;
    }

    /// <summary>Relie ce CI à un autre, désigné par sa référence.</summary>
    [HttpPost("{id:int}/relations")]
    public async Task<IActionResult> AddRelation(int id, CiRelationshipDto dto)
    {
        var error = Allowed.Check("Type de relation", dto.Type, CiRelationship.Types);
        if (error is not null) return BadRequest(new { message = error });
        var source = await Db.ConfigurationItems.FirstOrDefaultAsync(c => c.Id == id);
        if (source is null || !await VisibleAsync(source)) return NotFound();
        var reference = dto.TargetReference.Trim().ToUpper();
        var target = await Db.ConfigurationItems.FirstOrDefaultAsync(c => c.Reference == reference);
        // PER-08 : relier suppose de voir les deux CI.
        if (target is null || !await VisibleAsync(target)) return BadRequest(new { message = $"Aucun CI de référence « {dto.TargetReference} »." });
        if (target.Id == id) return BadRequest(new { message = "Un CI ne peut pas être relié à lui-même." });
        // CFG-14 : un CI retiré ne peut plus être relié.
        if (source.Status == "Retiré" || target.Status == "Retiré")
            return BadRequest(new { message = "Un CI retiré ne peut plus être relié." });
        if (await Db.CiRelationships.AnyAsync(r => r.SourceId == id && r.TargetId == target.Id && r.Type == dto.Type))
            return Conflict(new { message = "Cette relation existe déjà." });

        var rel = new CiRelationship { SourceId = id, TargetId = target.Id, Type = dto.Type };
        Db.CiRelationships.Add(rel);
        await Db.SaveChangesAsync();
        return Ok(new { rel.Id, rel.Type, Target = new { target.Id, target.Reference, target.Title } });
    }

    [HttpDelete("relations/{relationId:int}")]
    public async Task<IActionResult> RemoveRelation(int relationId)
    {
        var rel = await Db.CiRelationships.FindAsync(relationId);
        if (rel is null || !await VisibleAsync(rel.SourceId)) return NotFound();
        Db.CiRelationships.Remove(rel);
        await Db.SaveChangesAsync();
        return NoContent();
    }
}
