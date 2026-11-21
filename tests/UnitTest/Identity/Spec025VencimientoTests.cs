using AwesomeAssertions;
using Knock.Identity.Api.Modules.Trust.Data;
using Knock.Identity.Api.Modules.Trust.Services;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 025, T-06: vencimiento a los 7 días con un almacén y un almacenamiento de mentira.</summary>
public sealed class Spec025VencimientoTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 28, 18, 0, 0, TimeSpan.Zero);
    private static readonly byte[] _jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1];

    private static (VerificationExpiryService Service, FakeExpiryStore Store, Spec025FotosTests.MemoryBlobs Blobs) Create(bool lease = true, params ExpiredVerification[] expired)
    {
        var store = new FakeExpiryStore(lease, expired);
        var blobs = new Spec025FotosTests.MemoryBlobs();
        var clock = new Spec025ServicioTests.FixedClock(_now);
        var photos = new FotoVerificacionStore(blobs, new Knock.Identity.Api.Modules.Trust.Crypto.CifradoDeVerificacion(new Spec025FotosTests.MemoryWrapper()), clock);
        return (new VerificationExpiryService(store, photos, clock, NullLogger<VerificationExpiryService>.Instance), store, blobs);
    }

    [Fact]
    public async Task Borra_Blobs_Llave_Y_Deja_Fotos_Borradas_A_Los_Siete_Dias_CA_025_16()
    {
        var id = ObjectId.GenerateNewId();
        var (service, store, blobs) = Create(true, new ExpiredVerification(id, ["ine_frente", "selfie"]));
        blobs.Final[$"{id}/selfie"] = _jpeg;
        blobs.Quarantine[$"{id}/ine_frente"] = _jpeg;

        (await service.RunOnceAsync(TimeSpan.FromMinutes(55), CancellationToken.None)).Should().Be(1);

        blobs.Final.Should().BeEmpty();
        blobs.Quarantine.Should().BeEmpty();
        store.Marked.Should().Equal(id);
    }

    [Fact]
    public async Task Repetirlo_No_Falla_Ni_Cuenta_Dos_Veces_CA_025_16()
    {
        var id = ObjectId.GenerateNewId();
        var (service, store, _) = Create(true, new ExpiredVerification(id, ["selfie"]));
        await service.RunOnceAsync(TimeSpan.FromMinutes(55), CancellationToken.None);
        store.AlreadyMarked = true;
        (await service.RunOnceAsync(TimeSpan.FromMinutes(55), CancellationToken.None)).Should().Be(0);
    }

    [Fact]
    public async Task Sin_Arrendamiento_Otra_Replica_Corre_La_Tarea_CA_025_16()
    {
        var (service, store, _) = Create(false, new ExpiredVerification(ObjectId.GenerateNewId(), ["selfie"]));
        (await service.RunOnceAsync(TimeSpan.FromMinutes(55), CancellationToken.None)).Should().Be(0);
        store.Marked.Should().BeEmpty();
    }

    [Fact]
    public async Task Si_Falla_El_Borrado_No_Destruye_La_Llave_Ni_Cambia_El_Estado()
    {
        var id = ObjectId.GenerateNewId();
        var store = new FakeExpiryStore(true, [new ExpiredVerification(id, ["selfie"])]);
        var clock = new Spec025ServicioTests.FixedClock(_now);
        var crypto = new Knock.Identity.Api.Modules.Trust.Crypto.CifradoDeVerificacion(new Spec025FotosTests.MemoryWrapper());
        var photos = new FotoVerificacionStore(new Knock.Identity.Api.Modules.Trust.Storage.UnavailableVerificationBlobStore(), crypto, clock);
        var service = new VerificationExpiryService(store, photos, clock, NullLogger<VerificationExpiryService>.Instance);

        (await service.RunOnceAsync(TimeSpan.FromMinutes(55), CancellationToken.None)).Should().Be(0);
        store.Marked.Should().BeEmpty();
    }

    private sealed class FakeExpiryStore(bool lease, IReadOnlyList<ExpiredVerification> expired) : IVerificationExpiryStore
    {
        public List<ObjectId> Marked { get; } = [];

        public bool AlreadyMarked { get; set; }

        public Task<bool> TryAcquireLeaseAsync(string taskName, DateTime now, DateTime leaseUntil, CancellationToken cancellationToken) => Task.FromResult(lease);

        public Task<IReadOnlyList<ExpiredVerification>> FindExpiredAsync(DateTime now, int take, CancellationToken cancellationToken) => Task.FromResult(expired);

        public Task<bool> MarkPhotosDeletedAsync(ObjectId id, DateTime now, CancellationToken cancellationToken)
        {
            if (AlreadyMarked)
            {
                return Task.FromResult(false);
            }

            Marked.Add(id);
            return Task.FromResult(true);
        }
    }
}
