using System.ComponentModel.DataAnnotations;

namespace SAloha.Api.Core.Notifications;

/// <summary>
/// Notification dans l'application (SOC-21). Le destinataire est un principal — un
/// compte, un groupe AD, ou les gestionnaires d'un périmètre — résolu à la lecture :
/// l'appartenance aux groupes et les affectations restent celles du moment. Elle porte la
/// référence, le titre, l'événement et le lien vers la fiche (SOC-23).
/// </summary>
public class Notification
{
    public const string ToUser = "User";
    public const string ToGroup = "Group";
    public const string ToManagers = "Managers";

    public long Id { get; set; }
    [MaxLength(10)] public string RecipientType { get; set; } = ToUser;
    [MaxLength(200)] public string RecipientId { get; set; } = "";     // compte ou CN du groupe, en minuscules ; vide pour Managers
    public int PerimeterId { get; set; }                               // périmètre de l'enregistrement (SOC-23, gestionnaires)
    [MaxLength(20)] public string TargetType { get; set; } = "";
    public int TargetId { get; set; }
    [MaxLength(20)] public string Reference { get; set; } = "";
    [MaxLength(200)] public string Title { get; set; } = "";
    [MaxLength(200)] public string Event { get; set; } = "";
    [MaxLength(100)] public string Actor { get; set; } = "";           // auteur de l'événement : jamais notifié
    [MaxLength(200)] public string ActorDisplayName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Lecture d'une notification par un compte (une notification de groupe se lit membre par membre).</summary>
public class NotificationRead
{
    public long NotificationId { get; set; }
    [MaxLength(100)] public string User { get; set; } = "";
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;
}
