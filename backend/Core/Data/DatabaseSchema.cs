using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SAloha.Api.Core.Data;

/// <summary>
/// Schéma tenu par les migrations EF (Core/Data/Migrations), appliquées au démarrage ou par
/// une étape de déploiement (O1).
/// </summary>
public static class DatabaseSchema
{
    /// <summary>
    /// Met la base au niveau du modèle. Une base créée avant les migrations
    /// par <c>EnsureCreated</c> n'a pas d'historique : on y inscrit les
    /// migrations dont les tables existent déjà (Initial : problèmes ;
    /// ItilModules : incidents et autres processus), puis on applique le reste.
    /// Sans cela, Migrate recréerait des tables existantes et échouerait.
    /// </summary>
    public static void Migrate(AppDbContext db, ILogger logger)
    {
        var history = db.GetService<IHistoryRepository>();
        // Table d'historique vérifiée en SQL : avec EF Core 10, IHistoryRepository.Exists() répond
        // vrai même quand elle manque. Une base sans migration inscrite mais dont les tables
        // existent est une base créée par EnsureCreated (qui peut laisser un historique vide).
        var exists = TableExists(db, "__EFMigrationsHistory");
        if ((!exists || !history.GetAppliedMigrations().Any()) && TableExists(db, "Problems"))
        {
            var baseline = db.Database.GetMigrations()
                .Where(m => m.EndsWith("_Initial") || (m.EndsWith("_ItilModules") && TableExists(db, "Incidents")))
                .ToList();
            logger.LogWarning("Base créée sans migrations : inscription de {Migrations} comme déjà appliquées.",
                string.Join(", ", baseline));
            if (!exists) db.Database.ExecuteSqlRaw(history.GetCreateScript());
            foreach (var id in baseline)
                db.Database.ExecuteSqlRaw(history.GetInsertScript(new HistoryRow(id, ProductInfo.GetVersion())));
        }
        db.Database.Migrate();
    }

    /// <summary>
    /// O1 : préparation de la base au démarrage, ou seule (option <c>--migrate</c> : étape de
    /// déploiement, puis arrêt). <c>Database:MigrateOnStartup</c> (vrai par défaut) faux : l'API
    /// n'applique pas les migrations et refuse de démarrer s'il en reste, plutôt que de servir
    /// un schéma périmé. Puis alignement des données de référence embarquées (NIS 2).
    /// </summary>
    public static void Prepare(AppDbContext db, IConfiguration config, ILogger logger, bool migrateOnly)
    {
        if (migrateOnly || config.GetValue("Database:MigrateOnStartup", true)) Migrate(db, logger);
        else if (db.Database.GetPendingMigrations().ToList() is { Count: > 0 } pending)
            throw new InvalidOperationException(
                $"{pending.Count} migration(s) en attente (dernière : {pending[^1]}) et Database:MigrateOnStartup désactivé : " +
                "appliquer d'abord les migrations (dotnet SAloha.Api.dll --migrate).");
        // NIS-18 : figer les évaluations validées avant d'aligner la structure embarquée (ADR-0012).
        Modules.ComplianceAssessment.AssessmentReferential.FreezeValidated(db);
        Modules.ComplianceAssessment.ReferentialStore.EnsureStructure(db);
    }

    private static bool TableExists(AppDbContext db, string table) =>
        db.Database.SqlQueryRaw<bool>(
            "SELECT to_regclass({0}) IS NOT NULL AS \"Value\"", $"public.\"{table}\"").Single();
}
