using System.Net;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Records;

namespace SAloha.Api.Modules.ServiceConfigurationManagement;

/// <summary>
/// Règles d'un CI, communes à sa saisie (registre) et à sa création ou mise à jour depuis le
/// sas de staging (CFG-40) : un CI venu de la découverte obéit aux mêmes contrôles.
/// </summary>
public static class CiRules
{
    /// <summary>Correspondance du cycle applicatif et du statut du CI (CFG-48, § 4.3).</summary>
    public static readonly Dictionary<string, string[]> LifecycleByStatus = new()
    {
        ["Planifié"] = ["Étude", "Développement", "Homologation"],
        ["En service"] = ["En production", "Dépréciée"],
        ["Hors service"] = ["En production", "Dépréciée"],
        ["Retiré"] = ["Retirée"],
    };

    /// <summary>Cycle applicatif par défaut d'un statut (alignement quand seul le statut change).</summary>
    private static string DefaultLifecycle(string status) => LifecycleByStatus.GetValueOrDefault(status, ["Étude"])[0];

    /// <summary>
    /// Recopie les champs propres au CI : message d'erreur (400) ou null. <paramref name="status"/> est
    /// le statut visé (celui de création, ou le nouveau statut demandé).
    /// </summary>
    public static string? Apply(ConfigurationItem e, ConfigurationItemDto dto, string status)
    {
        var error = Allowed.Check("Type de CI", dto.CiType, ConfigurationItem.Types)
                    ?? Allowed.Check("Environnement", dto.Environment, ConfigurationItem.Environments)
                    ?? Allowed.Check("Criticité", Blank(dto.Criticality), ConfigurationItem.Criticalities, optional: true)
                    ?? Allowed.Check("Cycle applicatif", Blank(dto.AppLifecycle), ConfigurationItem.AppLifecycles, optional: true)
                    ?? Allowed.Check("Type de propriétaire métier", Blank(dto.BusinessOwnerType), ["User", "Group"], optional: true)
                    ?? Allowed.Check("Type de responsable DSI", Blank(dto.TechnicalOwnerType), ["User", "Group"], optional: true);
        if (error is not null) return error;
        if (dto.RtoHours < 0 || dto.RpoHours < 0) return "Le RTO et le RPO sont des durées positives, en heures.";

        var ip = Blank(dto.IpAddress);
        if (ip is not null && !IPAddress.TryParse(ip, out _)) return $"Adresse IP invalide : « {ip} ».";
        var mac = Blank(dto.MacAddress);
        string? normalizedMac = null;
        if (mac is not null && (normalizedMac = NormalizeMac(mac)) is null)
            return $"Adresse MAC invalide : « {mac} » (attendu : six octets hexadécimaux, ex. 00:1A:2B:3C:4D:5E).";

        var previousStatus = e.Status;
        var previousLifecycle = e.AppLifecycle;
        e.CiType = dto.CiType!; e.Environment = dto.Environment!;
        e.Location = dto.Location?.Trim() ?? "";
        e.IpAddress = ip; e.MacAddress = normalizedMac; e.OperatingSystem = Blank(dto.OperatingSystem);

        var app = e.CiType == "Application";
        e.AppCode = app ? Blank(dto.AppCode)?.ToUpperInvariant() : null;
        e.BusinessUnit = app ? Blank(dto.BusinessUnit) : null;
        (e.BusinessOwnerType, e.BusinessOwnerId, e.BusinessOwnerDisplayName) =
            app ? Person(dto.BusinessOwnerType, dto.BusinessOwnerId, dto.BusinessOwnerDisplayName) : (null, null, null);
        (e.TechnicalOwnerType, e.TechnicalOwnerId, e.TechnicalOwnerDisplayName) =
            app ? Person(dto.TechnicalOwnerType, dto.TechnicalOwnerId, dto.TechnicalOwnerDisplayName) : (null, null, null);
        e.Criticality = app ? Blank(dto.Criticality) : null;
        e.RtoHours = app ? dto.RtoHours : null;
        e.RpoHours = app ? dto.RpoHours : null;
        e.GdprSensitive = app && dto.GdprSensitive;
        e.Nis2Scope = app && dto.Nis2Scope;
        // CFG-48 : sans cycle saisi, celui du statut ; quand seul le statut change, le cycle s'aligne.
        var lifecycle = app ? Blank(dto.AppLifecycle) : null;
        if (app && (lifecycle is null
                    || (status != previousStatus && lifecycle == previousLifecycle && !Compatible(lifecycle, status))))
            lifecycle = DefaultLifecycle(status);
        e.AppLifecycle = lifecycle;

        var hardware = ConfigurationItem.HardwareTypes.Contains(e.CiType);
        e.Vendor = hardware ? Blank(dto.Vendor) : null;
        e.Model = hardware ? Blank(dto.Model) : null;
        e.SerialNumber = hardware ? Blank(dto.SerialNumber) : null;
        e.PurchaseDate = hardware ? Dates.Utc(dto.PurchaseDate) : null;
        e.WarrantyExpiry = hardware ? Dates.Utc(dto.WarrantyExpiry) : null;
        e.EndOfSupport = hardware ? Dates.Utc(dto.EndOfSupport) : null;

        // CFG-16 : attributs du type ; absents de la saisie, ils sont gardés (sauf ceux qu'un nouveau type ne définit pas).
        if (dto.Attributes is null) e.Attributes = CiAttributes.Keep(e.CiType, e.Attributes);
        else
        {
            var (attrError, values) = CiAttributes.Check(e.CiType, dto.Attributes);
            if (attrError is not null) return attrError;
            e.Attributes = values;
        }
        return null;
    }

    private static bool Compatible(string lifecycle, string status) =>
        LifecycleByStatus.GetValueOrDefault(status, []).Contains(lifecycle);

    /// <summary>
    /// Conditions d'un statut propres au CI : CFG-11 (propriétaire), CFG-43 (portefeuille
    /// applicatif), CFG-44 (actifs matériels), CFG-48 (cycle applicatif). Message (400) ou null.
    /// </summary>
    public static string? CheckStatus(ConfigurationItem e, string status)
    {
        if (status == "En service" && string.IsNullOrWhiteSpace(e.OwnerId))
            return "Désigner un propriétaire : il est obligatoire pour un CI en service.";

        var operating = ConfigurationItem.InOperation.Contains(status);
        if (e.CiType == "Application")
        {
            if (e.AppLifecycle is { } lc && !Compatible(lc, status))
                return $"Cycle applicatif « {lc} » incompatible avec le statut « {status} » : "
                       + string.Join(", ", LifecycleByStatus[status].Select(s => $"« {s} »")) + ".";
            if (operating)
            {
                var missing = new List<string>();
                if (e.AppCode is null) missing.Add("code applicatif");
                if (e.BusinessUnit is null) missing.Add("direction utilisatrice");
                if (e.BusinessOwnerId is null) missing.Add("propriétaire métier");
                if (e.TechnicalOwnerId is null) missing.Add("responsable DSI");
                if (e.Criticality is null) missing.Add("criticité");
                if (missing.Count > 0)
                    return $"Une application {status.ToLowerInvariant()} porte son {string.Join(", ", missing)} (CFG-43).";
            }
        }
        if (operating && ConfigurationItem.PhysicalTypes.Contains(e.CiType))
        {
            var missing = new List<string>();
            if (e.Vendor is null) missing.Add("constructeur");
            if (e.SerialNumber is null) missing.Add("numéro de série");
            if (string.IsNullOrWhiteSpace(e.Location)) missing.Add("emplacement");
            if (missing.Count > 0)
                return $"Un équipement {e.CiType.ToLowerInvariant()} {status.ToLowerInvariant()} porte son {string.Join(", ", missing)} (CFG-44).";
        }
        return null;
    }

    /// <summary>
    /// Doublons interdits entre CI non retirés (409) : même nom dans un environnement (CFG-17),
    /// même code applicatif (CFG-43), même numéro de série (CFG-44).
    /// </summary>
    public static async Task<string?> ConflictAsync(AppDbContext db, ConfigurationItem e)
    {
        if (e.Status == "Retiré") return null;
        var others = db.ConfigurationItems.AsNoTracking().Where(c => c.Id != e.Id && c.Status != "Retiré");
        var name = e.Title.ToLower();
        var twin = await others.Where(c => c.Environment == e.Environment && c.Title.ToLower() == name)
            .Select(c => c.Reference).FirstOrDefaultAsync();
        if (twin is not null) return $"Un CI « {e.Title} » existe déjà en {e.Environment} ({twin}).";
        if (e.AppCode is { } code)
        {
            var same = await others.Where(c => c.AppCode == code).Select(c => c.Reference).FirstOrDefaultAsync();
            if (same is not null) return $"Le code applicatif « {code} » est déjà porté par {same}.";
        }
        if (e.SerialNumber is { } serial)
        {
            var key = serial.ToLower();
            var same = await others.Where(c => c.SerialNumber != null && c.SerialNumber.ToLower() == key)
                .Select(c => c.Reference).FirstOrDefaultAsync();
            if (same is not null) return $"Le numéro de série « {serial} » est déjà porté par {same}.";
        }
        return null;
    }

    /// <summary>Adresse MAC au format 00:1A:2B:3C:4D:5E, ou null si la saisie n'en est pas une.</summary>
    public static string? NormalizeMac(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var hex = new string(raw.Where(Uri.IsHexDigit).ToArray());
        var separators = raw.Count(ch => !Uri.IsHexDigit(ch));
        if (hex.Length != 12 || separators is not (0 or 2 or 5)) return null;
        return string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2))).ToUpperInvariant();
    }

    /// <summary>Adresse IPv4 ou IPv6 valide, sous sa forme canonique ; null sinon.</summary>
    public static string? NormalizeIp(string? raw) =>
        IPAddress.TryParse(raw?.Trim(), out var ip) && ip.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6
            ? ip.ToString() : null;

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static (string?, string?, string?) Person(string? type, string? id, string? display) =>
        string.IsNullOrWhiteSpace(id) ? (null, null, null) : (Blank(type) ?? "User", id.Trim(), Blank(display) ?? id.Trim());
}
