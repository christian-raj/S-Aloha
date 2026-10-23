using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Data;
using SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

namespace SAloha.Api.Core.Platform;

/// <summary>
/// Stockage objet compatible S3 (Garage, ADR-0020) réglé dans la console (ADM-61) : dépôt,
/// lecture et suppression d'objets dans le bucket configuré.
/// </summary>
public class ObjectStorage(AppDbContext db, CredentialVault vault)
{
    public async Task<StorageSettings?> SettingsAsync() => await db.StorageSettings.AsNoTracking().FirstOrDefaultAsync();

    /// <summary>Stockage activé et complet : les pièces jointes sont acceptées.</summary>
    public static bool Ready(StorageSettings? s) =>
        s is { Enabled: true } && s.Endpoint != "" && s.Bucket != "" && s.AccessKey != "" && s.EncryptedSecretKey is not null;

    private AmazonS3Client Client(StorageSettings s) => new(
        new BasicAWSCredentials(s.AccessKey, vault.Unprotect(s.EncryptedSecretKey!)),
        new AmazonS3Config
        {
            ServiceURL = s.Endpoint,
            AuthenticationRegion = s.Region,
            ForcePathStyle = s.PathStyle,
            Timeout = TimeSpan.FromSeconds(30),
            MaxErrorRetry = 1,
            // Garage et la plupart des stockages compatibles n'acceptent pas les sommes de contrôle
            // ajoutées par défaut depuis la version 4 du SDK : seulement quand l'opération l'exige.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        });

    public async Task PutAsync(StorageSettings s, string key, Stream content, string contentType, CancellationToken ct = default)
    {
        using var client = Client(s);
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = s.Bucket, Key = key, InputStream = content, ContentType = contentType, AutoCloseStream = false,
            UseChunkEncoding = false,
        }, ct);
    }

    /// <summary>Le contenu de l'objet, à lire puis fermer par l'appelant.</summary>
    public async Task<Stream> GetAsync(StorageSettings s, string key, CancellationToken ct = default)
    {
        var client = Client(s);
        var response = await client.GetObjectAsync(s.Bucket, key, ct);
        return new OwnedStream(response.ResponseStream, response, client);
    }

    public async Task DeleteAsync(StorageSettings s, string key, CancellationToken ct = default)
    {
        using var client = Client(s);
        await client.DeleteObjectAsync(s.Bucket, key, ct);
    }

    /// <summary>Flux qui libère la réponse et le client S3 à sa fermeture.</summary>
    private sealed class OwnedStream(Stream inner, IDisposable response, IDisposable client) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => inner.ReadAsync(buffer, ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) { inner.Dispose(); response.Dispose(); client.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
