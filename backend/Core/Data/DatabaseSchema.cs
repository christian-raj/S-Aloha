using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SAloha.Api.Core.Data;

/// <summary>
/// Schéma tenu par les migrations EF (Core/Data/Migrations), appliquées au démarrage.
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
        if (!history.Exists() && TableExists(db, "Problems"))
        {
            var baseline = db.Database.GetMigrations()
                .Where(m => m.EndsWith("_Initial") || (m.EndsWith("_ItilModules") && TableExists(db, "Incidents")))
                .ToList();
            logger.LogWarning("Base créée sans migrations : inscription de {Migrations} comme déjà appliquées.",
                string.Join(", ", baseline));
            db.Database.ExecuteSqlRaw(history.GetCreateScript());
            foreach (var id in baseline)
                db.Database.ExecuteSqlRaw(history.GetInsertScript(new HistoryRow(id, ProductInfo.GetVersion())));
        }
        db.Database.Migrate();
    }

    private static bool TableExists(AppDbContext db, string table) =>
        db.Database.SqlQueryRaw<bool>(
            "SELECT to_regclass({0}) IS NOT NULL AS \"Value\"", $"public.\"{table}\"").Single();
}
