using System.Text.Json;
using System.Text.Json.Serialization;

namespace SAloha.Api.Core.Data;

/// <summary>
/// A2 (ADR-0016) : une entité EF n'est jamais sérialisée par l'API. Ce convertisseur, branché
/// sur le JSON de MVC, refuse tout type du modèle (les <c>DbSet</c> d'<see cref="AppDbContext"/>) :
/// une réponse qui en renverrait une échoue dès le premier test qui l'appelle.
/// </summary>
public sealed class EntityGuard : JsonConverterFactory
{
    private static readonly HashSet<Type> Entities = typeof(AppDbContext).GetProperties()
        .Select(p => p.PropertyType)
        .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Microsoft.EntityFrameworkCore.DbSet<>))
        .Select(t => t.GetGenericArguments()[0]).ToHashSet();

    public static bool IsEntity(Type type) => Entities.Contains(type);

    public override bool CanConvert(Type typeToConvert) => Entities.Contains(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        throw new InvalidOperationException(
            $"Entité EF « {typeToConvert.Name} » sérialisée par l'API : renvoyer ou recevoir un DTO (ADR-0016).");
}
