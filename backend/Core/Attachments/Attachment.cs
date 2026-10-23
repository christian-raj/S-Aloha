using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SAloha.Api.Core.Attachments;

/// <summary>
/// SOC-25 : pièce jointe d'un enregistrement. Le fichier est dans le stockage objet (ADR-0020,
/// ADM-61) sous <see cref="StorageKey"/> ; la base n'en garde que la description.
/// </summary>
public class Attachment
{
    public int Id { get; set; }
    [MaxLength(20)] public string TargetType { get; set; } = "";
    public int TargetId { get; set; }
    [MaxLength(255)] public string FileName { get; set; } = "";
    [MaxLength(100)] public string ContentType { get; set; } = "";
    public long Size { get; set; }
    [MaxLength(64)] public string Sha256 { get; set; } = "";
    [MaxLength(100)] public string StorageKey { get; set; } = "";
    [MaxLength(200)] public string CreatedBy { get; set; } = "";
    [MaxLength(250)] public string CreatedByDisplayName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public record AttachmentView(int Id, string FileName, string ContentType, long Size, string CreatedBy, string CreatedByDisplayName, DateTime CreatedAt)
{
    public static AttachmentView From(Attachment a) => new(a.Id, a.FileName, a.ContentType, a.Size, a.CreatedBy, a.CreatedByDisplayName, a.CreatedAt);
}

/// <summary>
/// SOC-25 : types admis — documents bureautiques, texte et images matricielles. Le type servi
/// vient de l'extension, jamais du navigateur ; la signature du contenu est vérifiée. HTML, SVG,
/// scripts et exécutables sont refusés (ils s'exécuteraient dans le navigateur ou sur le poste).
/// </summary>
public static class AttachmentTypes
{
    private enum Sig { Pdf, Zip, Ole, Png, Jpeg, Gif, Webp, Text }

    private static readonly Dictionary<string, (string Type, Sig Sig)> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ("application/pdf", Sig.Pdf),
        [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", Sig.Zip),
        [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Sig.Zip),
        [".pptx"] = ("application/vnd.openxmlformats-officedocument.presentationml.presentation", Sig.Zip),
        [".odt"] = ("application/vnd.oasis.opendocument.text", Sig.Zip),
        [".ods"] = ("application/vnd.oasis.opendocument.spreadsheet", Sig.Zip),
        [".odp"] = ("application/vnd.oasis.opendocument.presentation", Sig.Zip),
        [".doc"] = ("application/msword", Sig.Ole),
        [".xls"] = ("application/vnd.ms-excel", Sig.Ole),
        [".ppt"] = ("application/vnd.ms-powerpoint", Sig.Ole),
        [".msg"] = ("application/vnd.ms-outlook", Sig.Ole),
        [".zip"] = ("application/zip", Sig.Zip),
        [".png"] = ("image/png", Sig.Png),
        [".jpg"] = ("image/jpeg", Sig.Jpeg),
        [".jpeg"] = ("image/jpeg", Sig.Jpeg),
        [".gif"] = ("image/gif", Sig.Gif),
        [".webp"] = ("image/webp", Sig.Webp),
        [".txt"] = ("text/plain; charset=utf-8", Sig.Text),
        [".log"] = ("text/plain; charset=utf-8", Sig.Text),
        [".csv"] = ("text/csv; charset=utf-8", Sig.Text),
        [".json"] = ("application/json", Sig.Text),
        [".eml"] = ("message/rfc822", Sig.Text),
    };

    public static IEnumerable<string> Extensions => ByExtension.Keys.Order();

    /// <summary>Type servi pour ce nom de fichier et ce début de contenu, ou l'erreur (400).</summary>
    public static (string? Type, string? Error) Check(string fileName, byte[] head)
    {
        var ext = Path.GetExtension(fileName);
        if (!ByExtension.TryGetValue(ext, out var t))
            return (null, $"Type de fichier non admis ({(ext == "" ? "sans extension" : ext)}). Admis : {string.Join(", ", Extensions)}.");
        bool Starts(params byte[] magic) => head.Length >= magic.Length && head.AsSpan(0, magic.Length).SequenceEqual(magic);
        var ok = t.Sig switch
        {
            Sig.Pdf => Starts("%PDF-"u8.ToArray()),
            Sig.Zip => Starts(0x50, 0x4B, 0x03, 0x04) || Starts(0x50, 0x4B, 0x05, 0x06),
            Sig.Ole => Starts(0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1),
            Sig.Png => Starts(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
            Sig.Jpeg => Starts(0xFF, 0xD8, 0xFF),
            Sig.Gif => Starts("GIF87a"u8.ToArray()) || Starts("GIF89a"u8.ToArray()),
            Sig.Webp => head.Length >= 12 && Starts("RIFF"u8.ToArray()) && head.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => !head.Contains((byte)0),   // texte : pas d'octet nul
        };
        return ok ? (t.Type, null) : (null, $"Le contenu ne correspond pas à un fichier {ext} : refusé.");
    }

    /// <summary>Nom de fichier assaini : sans chemin ni caractère de contrôle, 255 caractères au plus.</summary>
    public static string SafeName(string name)
    {
        var file = Path.GetFileName(name.Replace('\\', '/'));
        var clean = new string(file.Where(c => !char.IsControl(c) && c is not ('"' or '/' or ':' or '*' or '?' or '<' or '>' or '|')).ToArray()).Trim();
        if (clean.Length > 255) clean = clean[..(255 - Path.GetExtension(clean).Length)] + Path.GetExtension(clean);
        return clean == "" ? "fichier" : clean;
    }
}
