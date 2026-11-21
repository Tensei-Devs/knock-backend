using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Trust.Crypto;
using Knock.Identity.Api.Modules.Trust.Services;
using Knock.Identity.Api.Modules.Trust.Storage;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 025, T-04: almacén de fotos con un almacenamiento y una llave maestra de mentira. Contenido ficticio, sin fotos reales.</summary>
public sealed class Spec025FotosTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);
    private static readonly byte[] _jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly byte[] _png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 9, 9];
    private readonly ObjectId _id = ObjectId.GenerateNewId();

    private static async Task<(FotoVerificacionStore Store, MemoryBlobs Blobs, WrappedVerificationKey Key, CifradoDeVerificacion Crypto)> CreateAsync()
    {
        var blobs = new MemoryBlobs();
        var crypto = new CifradoDeVerificacion(new MemoryWrapper());
        var (key, wrapped) = await crypto.CreateKeyAsync(CancellationToken.None);
        key.Dispose();
        return (new FotoVerificacionStore(blobs, crypto, new FixedClock(_now)), blobs, wrapped, crypto);
    }

    private static async Task<DomainException> FailsAsync(Func<Task> action) => (await action.Should().ThrowAsync<DomainException>()).Which;

    [Fact]
    public async Task Enlaces_De_Subida_De_Cinco_Minutos_Uno_Por_Tipo_CA_025_03()
    {
        var (store, blobs, _, _) = await CreateAsync();
        var links = await store.IssueUploadLinksAsync(_id, ["ine_frente", "selfie"], CancellationToken.None);
        links.Select(l => l.Type).Should().Equal("ine_frente", "selfie");
        links.Should().OnlyContain(l => l.ExpiresAt == _now.UtcDateTime.AddMinutes(5));
        blobs.Links.Should().Equal($"{_id}/ine_frente", $"{_id}/selfie");
        blobs.LinkLifetimes.Should().OnlyContain(t => t == TimeSpan.FromMinutes(5));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Confirma_Cifra_Y_Borra_La_Cuarentena_CA_025_09(bool png)
    {
        var (store, blobs, key, crypto) = await CreateAsync();
        var photo = png ? _png : _jpeg;
        blobs.Quarantine[$"{_id}/selfie"] = photo;

        await store.ConfirmAsync(_id, "selfie", key, CancellationToken.None);

        blobs.Quarantine.Should().BeEmpty();
        var stored = blobs.Final[$"{_id}/selfie"];
        stored.Should().NotEqual(photo);
        stored.AsSpan().IndexOf(photo).Should().Be(-1, "el definitivo nunca guarda el contenido en claro");
        (await store.ReadDecryptedAsync(_id, "selfie", key, CancellationToken.None)).Should().Equal(photo);
        using var dataKey = await crypto.UnwrapKeyAsync(key, CancellationToken.None);
        var copied = () => CifradoDeVerificacion.Decrypt(dataKey, stored, $"{_id}|ine_frente");
        copied.Should().Throw<CryptographicException>("un blob copiado a otro tipo no se descifra");
    }

    [Fact]
    public async Task Pdf_Renombrado_Responde_422_Y_No_Deja_Ningun_Blob_CA_025_10()
    {
        var (store, blobs, key, _) = await CreateAsync();
        blobs.Quarantine[$"{_id}/selfie"] = "%PDF-1.7 ..."u8.ToArray();
        var error = await FailsAsync(() => store.ConfirmAsync(_id, "selfie", key, CancellationToken.None));
        error.Code.Should().Be("content_invalid");
        blobs.Quarantine.Should().BeEmpty();
        blobs.Final.Should().BeEmpty();
    }

    [Fact]
    public async Task Foto_De_Mas_De_8_Mb_Responde_422_CA_025_10()
    {
        var (store, blobs, key, _) = await CreateAsync();
        var big = new byte[FotoVerificacionStore.MaxPhotoBytes + 1];
        _jpeg.CopyTo(big, 0);
        blobs.Quarantine[$"{_id}/selfie"] = big;
        (await FailsAsync(() => store.ConfirmAsync(_id, "selfie", key, CancellationToken.None))).Code.Should().Be("content_invalid");
        blobs.Quarantine.Should().BeEmpty();
        FotoVerificacionStore.IsAcceptedImage(big.AsSpan(0, FotoVerificacionStore.MaxPhotoBytes)).Should().BeTrue();
        FotoVerificacionStore.IsAcceptedImage([]).Should().BeFalse();
    }

    [Fact]
    public async Task Sin_Blob_Responde_409_Y_Doble_Confirmacion_Tambien_CA_025_11()
    {
        var (store, blobs, key, _) = await CreateAsync();
        (await FailsAsync(() => store.ConfirmAsync(_id, "selfie", key, CancellationToken.None))).Code.Should().Be("photo_not_uploaded");

        blobs.Quarantine[$"{_id}/selfie"] = _jpeg;
        await store.ConfirmAsync(_id, "selfie", key, CancellationToken.None);
        blobs.Quarantine[$"{_id}/selfie"] = _jpeg;
        (await FailsAsync(() => store.ConfirmAsync(_id, "selfie", key, CancellationToken.None))).Code.Should().Be("photo_already_confirmed");
    }

    [Fact]
    public async Task Borrado_Es_Idempotente_CA_025_16()
    {
        var (store, blobs, key, _) = await CreateAsync();
        blobs.Quarantine[$"{_id}/selfie"] = _jpeg;
        await store.ConfirmAsync(_id, "selfie", key, CancellationToken.None);
        await store.PurgeAsync(_id, ["ine_frente", "selfie"], CancellationToken.None);
        await store.PurgeAsync(_id, ["ine_frente", "selfie"], CancellationToken.None);
        blobs.Final.Should().BeEmpty();
        (await store.ReadDecryptedAsync(_id, "selfie", key, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Sin_Almacenamiento_Configurado_Falla_Cerrado_Con_503()
    {
        var crypto = new CifradoDeVerificacion(new UnavailableVerificationKeyWrapper());
        var store = new FotoVerificacionStore(new UnavailableVerificationBlobStore(), crypto, new FixedClock(_now));
        (await FailsAsync(() => store.IssueUploadLinksAsync(_id, ["selfie"], CancellationToken.None))).Kind.Should().Be(DomainErrorKind.ServiceUnavailable);
        (await FailsAsync(() => crypto.CreateKeyAsync(CancellationToken.None))).Code.Should().Be("verification_storage_unavailable");
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    internal sealed class MemoryBlobs : IVerificationBlobStore
    {
        public Dictionary<string, byte[]> Quarantine { get; } = [];

        public Dictionary<string, byte[]> Final { get; } = [];

        public List<string> Links { get; } = [];

        public List<TimeSpan> LinkLifetimes { get; } = [];

        public Task<Uri> IssueQuarantineUploadLinkAsync(string blobName, TimeSpan lifetime, CancellationToken cancellationToken)
        {
            Links.Add(blobName);
            LinkLifetimes.Add(lifetime);
            return Task.FromResult(new Uri("https://almacen.invalid/cuarentena/" + blobName));
        }

        public Task<Uri> IssueFinalReadLinkAsync(string blobName, TimeSpan lifetime, CancellationToken cancellationToken) =>
            Task.FromResult(new Uri("https://almacen.invalid/definitivo/" + blobName));

        public Task<byte[]?> ReadQuarantineAsync(string blobName, int maxBytes, CancellationToken cancellationToken) =>
            Task.FromResult(Quarantine.TryGetValue(blobName, out var v) ? v.AsSpan(0, Math.Min(v.Length, maxBytes + 1)).ToArray() : null);

        public Task<byte[]?> ReadFinalAsync(string blobName, CancellationToken cancellationToken) => Task.FromResult(Final.TryGetValue(blobName, out var v) ? v : null);

        public Task<bool> WriteFinalAsync(string blobName, byte[] content, CancellationToken cancellationToken) => Task.FromResult(Final.TryAdd(blobName, content));

        public Task DeleteQuarantineAsync(string blobName, CancellationToken cancellationToken)
        {
            Quarantine.Remove(blobName);
            return Task.CompletedTask;
        }

        public Task DeleteFinalAsync(string blobName, CancellationToken cancellationToken)
        {
            Final.Remove(blobName);
            return Task.CompletedTask;
        }
    }

    internal sealed class MemoryWrapper : IVerificationKeyWrapper
    {
        private readonly byte[] _kek = RandomNumberGenerator.GetBytes(32);

        public Task<WrappedVerificationKey> WrapAsync(byte[] dataKey, CancellationToken cancellationToken)
        {
            var packed = new byte[12 + 16 + dataKey.Length];
            RandomNumberGenerator.Fill(packed.AsSpan(0, 12));
            using var aes = new AesGcm(_kek, 16);
            aes.Encrypt(packed.AsSpan(0, 12), dataKey, packed.AsSpan(28), packed.AsSpan(12, 16));
            return Task.FromResult(new WrappedVerificationKey(Convert.ToBase64String(packed), "kek-1"));
        }

        public Task<byte[]> UnwrapAsync(WrappedVerificationKey wrapped, CancellationToken cancellationToken)
        {
            var packed = Convert.FromBase64String(wrapped.Value);
            var plain = new byte[packed.Length - 28];
            using var aes = new AesGcm(_kek, 16);
            aes.Decrypt(packed.AsSpan(0, 12), packed.AsSpan(28), packed.AsSpan(12, 16), plain);
            return Task.FromResult(plain);
        }
    }
}
