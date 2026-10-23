using System.ComponentModel.DataAnnotations;

namespace SAloha.Api.Modules.ServiceRequestManagement;

/// <summary>REQ-24 : tâche prédéfinie d'un modèle du catalogue, recopiée sur la demande à l'approbation.</summary>
public class RequestCatalogTask
{
    public int Id { get; set; }
    public int CatalogItemId { get; set; }
    [MaxLength(200)] public string Title { get; set; } = "";
    [MaxLength(10)] public string? OwnerType { get; set; }
    [MaxLength(200)] public string? OwnerId { get; set; }
    [MaxLength(250)] public string? OwnerDisplayName { get; set; }
    public int Order { get; set; }
}

/// <summary>REQ-24 : tâche d'exécution d'une demande, avec son responsable.</summary>
public class RequestTask
{
    public const string ToDo = "À faire", InProgress = "En cours", Done = "Terminée", Cancelled = "Annulée";
    public static readonly string[] Statuses = [ToDo, InProgress, Done, Cancelled];
    /// <summary>REQ-25 : statuts qui n'empêchent pas la demande d'être satisfaite.</summary>
    public static readonly string[] Finished = [Done, Cancelled];

    public int Id { get; set; }
    public int RequestId { get; set; }
    [MaxLength(200)] public string Title { get; set; } = "";
    [MaxLength(10)] public string? OwnerType { get; set; }
    [MaxLength(200)] public string? OwnerId { get; set; }
    [MaxLength(250)] public string? OwnerDisplayName { get; set; }
    [MaxLength(20)] public string Status { get; set; } = ToDo;
    public int Order { get; set; }
    /// <summary>Recopiée du modèle (sinon ajoutée sur la demande).</summary>
    public bool FromCatalog { get; set; }
    [MaxLength(200)] public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public record RequestCatalogTaskDto(string Title, string? OwnerType, string? OwnerId, string? OwnerDisplayName);
public record RequestTaskDto(string Title, string? OwnerType, string? OwnerId, string? OwnerDisplayName, string? Status);

/// <summary>REQ-32 : enquête de satisfaction à la clôture (facultative).</summary>
public record SatisfactionDto(int Score, string? Comment);

public record RequestTaskView(int Id, int RequestId, string Title, string? OwnerType, string? OwnerId, string? OwnerDisplayName,
    string Status, int Order, bool FromCatalog, string CreatedBy, DateTime CreatedAt, DateTime? CompletedAt)
{
    public static RequestTaskView From(RequestTask t) => new(t.Id, t.RequestId, t.Title, t.OwnerType, t.OwnerId, t.OwnerDisplayName,
        t.Status, t.Order, t.FromCatalog, t.CreatedBy, t.CreatedAt, t.CompletedAt);
}

public record RequestCatalogTaskView(int Id, string Title, string? OwnerType, string? OwnerId, string? OwnerDisplayName, int Order)
{
    public static RequestCatalogTaskView From(RequestCatalogTask t) => new(t.Id, t.Title, t.OwnerType, t.OwnerId, t.OwnerDisplayName, t.Order);
}
