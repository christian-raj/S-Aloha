using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Modules.ComplianceAssessment;

namespace SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

/// <summary>
/// CFG-50 : constructeur d'un préfixe MAC (OUI, 24 bits) du registre public de l'IEEE. La base
/// n'est pas versionnée avec S-Aloha : l'Admin l'importe depuis le fichier <c>oui.csv</c>.
/// </summary>
public class MacVendor
{
    /// <summary>Six chiffres hexadécimaux en majuscules (« 001A2B »).</summary>
    [Key][MaxLength(6)] public string Prefix { get; set; } = "";
    [MaxLength(100)] public string Organization { get; set; } = "";
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}

public record MacVendorImportDto(string? Csv);
public record MacVendorImportReport(int Imported, int Skipped);
public record MacVendorStatus(int Count, DateTime? LastImport);

public static partial class MacVendors
{
    [GeneratedRegex("^[0-9A-F]{6}$")] private static partial Regex Hex6();

    /// <summary>
    /// Préfixe OUI d'une adresse MAC normalisée, ou null pour une adresse administrée
    /// localement (bit 0x02 du premier octet : adresses aléatoires des téléphones et des
    /// machines virtuelles) ou de groupe (bit 0x01) — elles n'ont pas de constructeur.
    /// </summary>
    public static string? PrefixOf(string? mac)
    {
        if (CiRules.NormalizeMac(mac) is not { } m) return null;
        var first = byte.Parse(m[..2], NumberStyles.HexNumber);
        return (first & 0x03) != 0 ? null : m[..8].Replace(":", "");
    }

    private static readonly string[] Suffixes =
    [
        ", inc.", ", inc", " inc.", " inc", " corporation", " corp.", " corp", " co., ltd.", " co., ltd", " co.,ltd.", " co.,ltd",
        " co. ltd.", " co ltd", " ltd.", " ltd", " limited", " gmbh", " s.a.", " sa", " ag", " b.v.", " llc", " l.p.", " oy", " ab", ","
    ];

    /// <summary>Nom d'organisation sans forme juridique (« Cisco Systems, Inc » → « Cisco Systems »).</summary>
    public static string ShortName(string organization)
    {
        var name = Regex.Replace(organization.Trim(), @"\s+", " ");
        for (var changed = true; changed;)
        {
            changed = false;
            foreach (var s in Suffixes)
                if (name.Length > s.Length + 1 && name.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                {
                    name = name[..^s.Length].TrimEnd();
                    changed = true;
                }
        }
        return name.Length > 100 ? name[..100] : name;
    }

    /// <summary>
    /// Import du registre MA-L de l'IEEE (colonnes « Assignment » et « Organization Name ») :
    /// remplace toute la base. Les lignes d'un autre registre (MA-M, MA-S, CID) ou mal formées
    /// sont écartées et comptées.
    /// </summary>
    public static async Task<MacVendorImportReport> ImportAsync(AppDbContext db, string csv)
    {
        var rows = Csv.Parse(csv);
        if (rows.Count < 2) throw new FormatException("Le fichier est vide ou ne contient que l'en-tête.");
        var header = rows[0].Select(h => h.Trim()).ToList();
        int Col(string name) => header.FindIndex(h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase));
        int registry = Col("Registry"), assignment = Col("Assignment"), organization = Col("Organization Name");
        if (assignment < 0 || organization < 0)
            throw new FormatException("En-tête attendu : celui du fichier oui.csv de l'IEEE (colonnes « Assignment » et « Organization Name »).");

        var now = DateTime.UtcNow;
        var vendors = new Dictionary<string, MacVendor>();
        var skipped = 0;
        foreach (var row in rows.Skip(1))
        {
            var prefix = row.Count > assignment ? row[assignment].Trim().ToUpperInvariant() : "";
            var org = row.Count > organization ? row[organization].Trim() : "";
            var reg = registry >= 0 && row.Count > registry ? row[registry].Trim() : "MA-L";
            if (!string.Equals(reg, "MA-L", StringComparison.OrdinalIgnoreCase) || !Hex6().IsMatch(prefix) || org.Length == 0
                || vendors.ContainsKey(prefix)) { skipped++; continue; }
            vendors[prefix] = new MacVendor { Prefix = prefix, Organization = ShortName(org), ImportedAt = now };
        }
        if (vendors.Count == 0) throw new FormatException("Aucun préfixe MA-L valide dans le fichier.");

        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Set<MacVendor>().ExecuteDeleteAsync();
        db.Set<MacVendor>().AddRange(vendors.Values);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        db.ChangeTracker.Clear();
        return new MacVendorImportReport(vendors.Count, skipped);
    }
}

/// <summary>CFG-50 : état de la base des constructeurs (tous), import du registre de l'IEEE (Admin).</summary>
[ApiController]
[Route("api/discovery/mac-vendors")]
[Authorize(Policy = "User")]
public class MacVendorsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<MacVendorStatus> Get() => new(
        await db.Set<MacVendor>().CountAsync(),
        await db.Set<MacVendor>().MaxAsync(v => (DateTime?)v.ImportedAt));

    [HttpPost("import")]
    [Authorize(Policy = "Admin")]
    [RequestSizeLimit(12_000_000)]
    public async Task<IActionResult> Import(MacVendorImportDto dto)
    {
        try { return Ok(await MacVendors.ImportAsync(db, dto.Csv ?? "")); }
        catch (FormatException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
