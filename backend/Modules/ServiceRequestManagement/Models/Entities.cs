using System.ComponentModel.DataAnnotations;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ServiceRequestManagement;

/// <summary>
/// Demande de service (ITIL 4, gestion des demandes de service) : demande
/// prédéfinie initiée par un utilisateur (accès, matériel, information…).
/// </summary>
public class ServiceRequest : Record
{
    public static readonly string[] Statuses = ["Soumise", "Approuvée", "Rejetée", "En cours", "Satisfaite", "Close"];
    public static readonly string[] Done = ["Rejetée", "Satisfaite", "Close"];

    [MaxLength(200)] public string RequestedItem { get; set; } = "";          // objet demandé
    [MaxLength(200)] public string? RequestedFor { get; set; }                // bénéficiaire (sAMAccountName)
    [MaxLength(250)] public string? RequestedForDisplayName { get; set; }
    public DateTime? DueDate { get; set; }
    [MaxLength(200)] public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? FulfilledAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public record ServiceRequestDto(string Title, string? Description, string? Status,
    string? OwnerType, string? OwnerId, string? OwnerDisplayName,
    string? RequestedItem, string? RequestedFor, string? RequestedForDisplayName,
    DateTime? DueDate) : IRecordDto;
