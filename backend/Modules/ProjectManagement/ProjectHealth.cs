namespace SAloha.Api.Modules.ProjectManagement;

/// <summary>
/// Santé d'un projet : tolérances de délai et de coût (PRJ-31, PRJ-32) et prévision de la date
/// de fin (PRJ-22). Fonctions pures, partagées par la synthèse et la console (PRJ-34).
/// </summary>
public static class ProjectHealth
{
    public const string Within = "Dans la cible", Watch = "À surveiller", Out = "Hors tolérance", NotApplicable = "Sans objet";

    /// <summary>PRJ-31 : tolérance de délai à la date du jour.</summary>
    public static string TimeTolerance(Project p, DateTime today) =>
        p.TargetEndDate is null || Project.Done.Contains(p.Status) ? NotApplicable : Against(p, today);

    /// <summary>PRJ-32 : tolérance de coût.</summary>
    public static string CostTolerance(Project p)
    {
        if (p.Budget is not { } b || p.ActualCost is not { } c) return NotApplicable;
        if (c <= b) return Within;
        return p.ToleranceCostPercent is { } t && c <= b * (1 + t / 100) ? Watch : Out;
    }

    /// <summary>Une date comparée à la fin cible et à la tolérance de délai du projet.</summary>
    private static string Against(Project p, DateTime date)
    {
        var target = p.TargetEndDate!.Value.Date;
        if (date.Date <= target) return Within;
        return p.ToleranceDays is { } d && date.Date <= target.AddDays(d) ? Watch : Out;
    }

    public record Forecast(DateTime? Date, string Method, string Basis, string Tolerance);

    /// <summary>
    /// PRJ-22 : prévision de la date de fin, comparée à la tolérance de délai.
    /// Vélocité (sprints clos : points terminés par sprint, durée moyenne d'un sprint) ; sinon
    /// phases ou étapes (fin planifiée de la dernière non terminée, décalée du retard de la phase
    /// en cours) ; sinon avancement (part des points, ou des éléments, terminés depuis le début).
    /// </summary>
    public static Forecast ForecastEnd(Project p, IReadOnlyCollection<WorkItem> items, IReadOnlyCollection<Sprint> sprints,
        IReadOnlyCollection<ProjectPhase> phases, DateTime today)
    {
        today = today.Date;
        Forecast Done(DateTime? date, string method, string basis) =>
            new(date?.Date, method, basis, date is null || p.TargetEndDate is null || Project.Done.Contains(p.Status) ? NotApplicable : Against(p, date.Value));
        if (Project.Done.Contains(p.Status)) return new(null, "Aucune", "Projet terminé.", NotApplicable);

        int total = items.Sum(w => w.Points ?? 0), done = items.Where(w => w.Status == "Terminé").Sum(w => w.Points ?? 0);
        var closed = sprints.Where(s => s.Status == "Clos" && s.StartDate is not null && s.EndDate is not null).ToList();
        if (closed.Count > 0 && total > 0)
        {
            var velocity = closed.Average(s => items.Where(w => w.SprintId == s.Id && w.Status == "Terminé").Sum(w => w.Points ?? 0));
            var length = Math.Max(1, closed.Average(s => (s.EndDate!.Value.Date - s.StartDate!.Value.Date).TotalDays));
            var remaining = total - done;
            if (remaining <= 0) return Done(today, "Vélocité", "Tous les points sont terminés.");
            if (velocity > 0)
            {
                var sprintsLeft = Math.Ceiling(remaining / velocity);
                return Done(today.AddDays(sprintsLeft * length), "Vélocité",
                    $"{remaining} point(s) restant(s), {velocity:0.#} point(s) par sprint de {length:0} jour(s) : {sprintsLeft} sprint(s).");
            }
        }

        var open = phases.Where(x => x.Status != "Terminée").ToList();
        if (phases.Count > 0 && open.Count == 0) return Done(today, "Phases", "Toutes les phases sont terminées.");
        if (open.Count > 0 && open.All(x => x.PlannedEnd is not null))
        {
            var current = open.Where(x => x.Status == "En cours").OrderBy(x => x.Order).FirstOrDefault();
            var slip = current?.PlannedEnd is { } end && end.Date < today ? today - end.Date : TimeSpan.Zero;
            var last = open.Max(x => x.PlannedEnd!.Value.Date) + slip;
            return Done(last, "Phases", slip > TimeSpan.Zero
                ? $"Fin planifiée de la dernière phase, décalée de {slip.TotalDays:0} jour(s) de retard de « {current!.Name} »."
                : "Fin planifiée de la dernière phase non terminée.");
        }

        var (part, whole) = total > 0 ? (done, total) : (items.Count(w => w.Status == "Terminé"), items.Count);
        if (p.StartDate is { } start && whole > 0 && part > 0 && today > start.Date)
        {
            var elapsed = (today - start.Date).TotalDays;
            var forecast = start.Date.AddDays(Math.Ceiling(elapsed * whole / part));
            return Done(forecast, "Avancement", $"{part} sur {whole} terminé(s) en {elapsed:0} jour(s), au même rythme.");
        }
        return new(null, "Aucune", "Données insuffisantes : ni sprint clos, ni phases planifiées, ni avancement.", NotApplicable);
    }
}
