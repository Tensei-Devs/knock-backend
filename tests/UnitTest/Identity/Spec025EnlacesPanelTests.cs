using AwesomeAssertions;
using Knock.Identity.Api.Modules.Trust.Crypto;
using Knock.Identity.Api.Modules.Trust.Services;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 025 T-04 y spec 053 VER-04: los enlaces de lectura del panel y el borrado de blobs salen del almacén de fotos privado.</summary>
public sealed class Spec025EnlacesPanelTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);

    private static (FotoVerificacionStore Store, Spec025FotosTests.MemoryBlobs Blobs) Create()
    {
        var blobs = new Spec025FotosTests.MemoryBlobs();
        return (new FotoVerificacionStore(blobs, new CifradoDeVerificacion(new Spec025FotosTests.MemoryWrapper()), new Spec025ServicioTests.FixedClock(_now)), blobs);
    }

    [Fact]
    public async Task Entrega_Un_Enlace_De_Cinco_Minutos_Por_Foto_Sin_Guardarlo()
    {
        var (store, _) = Create();
        var id = ObjectId.GenerateNewId();
        var links = await new TrustVerificationPhotoLinks(store).IssueAsync(id.ToString(), ["ine_frente", "selfie"], TimeSpan.FromMinutes(5), CancellationToken.None);
        links.Select(l => l.Tipo).Should().Equal("ine_frente", "selfie");
        links.Should().OnlyContain(l => l.VenceEn == _now.UtcDateTime.AddMinutes(5) && l.Url.Contains(id.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task El_Purgador_Borra_Los_Tres_Tipos_Y_Es_Idempotente()
    {
        var (store, blobs) = Create();
        var id = ObjectId.GenerateNewId();
        blobs.Final[$"{id}/selfie"] = [1];
        blobs.Quarantine[$"{id}/ine_reverso"] = [1];
        var purger = new TrustVerificationBlobPurger(store);
        await purger.PurgeBlobsAsync(id.ToString(), CancellationToken.None);
        await purger.PurgeBlobsAsync(id.ToString(), CancellationToken.None);
        await purger.PurgeBlobsAsync("no-es-un-id", CancellationToken.None);
        blobs.Final.Should().BeEmpty();
        blobs.Quarantine.Should().BeEmpty();
    }
}
