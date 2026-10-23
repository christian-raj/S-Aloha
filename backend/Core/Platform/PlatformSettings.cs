using System.ComponentModel.DataAnnotations;

namespace SAloha.Api.Core.Platform;

/// <summary>
/// ADM-60 : serveur d'envoi des courriels (SMTP), réglé par l'Admin dans la console
/// (ADR-0020). Une seule ligne (<c>Id</c> = 1) ; le mot de passe est chiffré par le coffre.
/// </summary>
public class MailSettings
{
    public const string NoTls = "Aucun", StartTls = "STARTTLS", Tls = "TLS";
    public static readonly string[] SecurityModes = [NoTls, StartTls, Tls];

    public int Id { get; set; } = 1;
    public bool Enabled { get; set; }
    [MaxLength(255)] public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    [MaxLength(10)] public string Security { get; set; } = StartTls;
    [MaxLength(255)] public string? Username { get; set; }
    public string? EncryptedPassword { get; set; }
    [MaxLength(255)] public string FromAddress { get; set; } = "";
    [MaxLength(100)] public string FromName { get; set; } = "S-Aloha";
    /// <summary>SOC-22 : adresse de l'interface (https://itsm.example.org), pour les liens des courriels ; vide : pas de lien.</summary>
    [MaxLength(255)] public string PublicUrl { get; set; } = "";
    public DateTime? UpdatedAt { get; set; }
    [MaxLength(200)] public string? UpdatedBy { get; set; }
}

/// <summary>
/// ADM-61 : stockage objet compatible S3 (Garage) des pièces jointes (ADR-0020). Une seule
/// ligne ; la clé secrète est chiffrée par le coffre.
/// </summary>
public class StorageSettings
{
    public int Id { get; set; } = 1;
    public bool Enabled { get; set; }
    [MaxLength(255)] public string Endpoint { get; set; } = "";
    [MaxLength(50)] public string Region { get; set; } = "garage";
    [MaxLength(63)] public string Bucket { get; set; } = "";
    [MaxLength(128)] public string AccessKey { get; set; } = "";
    public string? EncryptedSecretKey { get; set; }
    /// <summary>Adressage par chemin (<c>endpoint/bucket/clé</c>), celui de Garage ; sinon par sous-domaine.</summary>
    public bool PathStyle { get; set; } = true;
    /// <summary>Taille maximale d'une pièce jointe, en Mo (SOC-25).</summary>
    public int MaxFileMb { get; set; } = 10;
    public DateTime? UpdatedAt { get; set; }
    [MaxLength(200)] public string? UpdatedBy { get; set; }
}

/// <summary>Saisie du SMTP. <c>Password</c> absent : inchangé ; vide : effacé.</summary>
public record MailSettingsDto(bool Enabled, string? Host, int Port, string? Security, string? Username, string? Password,
    string? FromAddress, string? FromName, string? PublicUrl = null);

/// <summary>Le SMTP tel que l'interface le voit : jamais le mot de passe, seulement s'il est enregistré.</summary>
public record MailSettingsView(bool Enabled, string Host, int Port, string Security, string? Username, bool HasPassword,
    string FromAddress, string FromName, DateTime? UpdatedAt, string? UpdatedBy, string PublicUrl = "")
{
    public static MailSettingsView From(MailSettings s) => new(s.Enabled, s.Host, s.Port, s.Security, s.Username,
        s.EncryptedPassword is not null, s.FromAddress, s.FromName, s.UpdatedAt, s.UpdatedBy, s.PublicUrl);
}

/// <summary>Saisie du stockage. <c>SecretKey</c> absent : inchangée ; vide : effacée.</summary>
public record StorageSettingsDto(bool Enabled, string? Endpoint, string? Region, string? Bucket, string? AccessKey, string? SecretKey,
    bool PathStyle = true, int MaxFileMb = 10);

public record StorageSettingsView(bool Enabled, string Endpoint, string Region, string Bucket, string AccessKey, bool HasSecretKey,
    bool PathStyle, int MaxFileMb, DateTime? UpdatedAt, string? UpdatedBy)
{
    public static StorageSettingsView From(StorageSettings s) => new(s.Enabled, s.Endpoint, s.Region, s.Bucket, s.AccessKey,
        s.EncryptedSecretKey is not null, s.PathStyle, s.MaxFileMb, s.UpdatedAt, s.UpdatedBy);
}

/// <summary>Essai d'envoi : destinataire du courriel d'essai.</summary>
public record MailTestDto(string To);

/// <summary>Résultat d'un essai de connexion : le message de l'erreur, sans secret.</summary>
public record ConnectionTest(bool Ok, string Message);
