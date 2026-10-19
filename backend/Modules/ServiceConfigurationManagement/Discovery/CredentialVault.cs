using System.Security.Cryptography;
using System.Text;

namespace SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

/// <summary>
/// Coffre d'identifiants (CFG-45) : AES-256-GCM, clé dérivée (SHA-256) de
/// <c>Discovery:VaultKey</c>, tenue hors de la base comme <c>Jwt:Key</c>. Un secret chiffré
/// est « v1: » suivi du nonce, de l'étiquette d'authentification et du chiffré, en base64 :
/// une valeur altérée en base est refusée au déchiffrement. Sans clé configurée, le coffre
/// refuse d'enregistrer (le secret ne doit jamais être stocké en clair).
/// </summary>
public class CredentialVault(IConfiguration config)
{
    private const string Prefix = "v1:";
    private const int NonceSize = 12, TagSize = 16;

    private byte[]? Key
    {
        get
        {
            var raw = config["Discovery:VaultKey"];
            return string.IsNullOrWhiteSpace(raw) || raw.Length < 32 ? null : SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        }
    }

    /// <summary>Clé configurée (au moins 32 caractères) : le coffre peut chiffrer et déchiffrer.</summary>
    public bool Configured => Key is not null;

    public const string NotConfigured =
        "Coffre d'identifiants non configuré : définir Discovery:VaultKey (32 caractères au moins) dans la configuration de l'API.";

    public string Protect(string secret)
    {
        var key = Key ?? throw new InvalidOperationException(NotConfigured);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plain = Encoding.UTF8.GetBytes(secret);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(key, TagSize)) aes.Encrypt(nonce, plain, cipher, tag);
        return Prefix + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public string Unprotect(string stored)
    {
        var key = Key ?? throw new InvalidOperationException(NotConfigured);
        if (!stored.StartsWith(Prefix)) throw new CryptographicException("Secret du coffre dans un format inconnu.");
        var data = Convert.FromBase64String(stored[Prefix.Length..]);
        var plain = new byte[data.Length - NonceSize - TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(data.AsSpan(0, NonceSize), data.AsSpan(NonceSize + TagSize), data.AsSpan(NonceSize, TagSize), plain);
        return Encoding.UTF8.GetString(plain);
    }
}
