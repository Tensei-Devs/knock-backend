extern alias MarketApi;

using AwesomeAssertions;
using Knock.Contracts.Moderation;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Modules.Media.Data;
using MarketApi::Knock.Market.Api.Modules.Media.Domain;
using MarketApi::Knock.Market.Api.Modules.Media.Services;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Knock.IntegrationTest.Media;

/// <summary>
/// Spec 020 (CA-18) y 032 (R-16): <c>IMediaApi</c> contra MongoDB y Azurite reales. Verifica el <c>uso</c> de fotos confirmadas de verdad y que el enlace de lectura apunta a la versión reducida que
/// existe en el contenedor final. Azurite no firma SAS de delegación, así que el emisor de enlaces es un doble que anota el blob pedido (las propiedades de la SAS están en <c>Spec032SasTests</c>).
/// </summary>
public class Spec020bMediaUsoIntegrationTests
{
    private const string _owner = "owner-1";
    private const string _reader = "reader-1";

    private static MongoDB.Driver.MongoClient? _sharedClient;

    [Fact]
    public async Task ConfirmedPhotos_AreCheckedByUseAndLinkedToTheReducedBlob_CA_020_18()
    {
        await using var azurite = await AzuriteFixture.StartAsync();
        using var processor = new ImageProcessor(Options.Create(new MediaOptions { ReducedMaxSide = 512, MaxConcurrentProcessing = 2 }));
        var documentDb = await DocumentDbFixture.SharedAsync();
        _sharedClient ??= new MongoDB.Driver.MongoClient(documentDb.ConnectionString);
        var database = _sharedClient.GetDatabase("s020b_" + Guid.NewGuid().ToString("N"));
        var options = new MediaOptions { ReducedMaxSide = 512, MaxConcurrentProcessing = 2 };
        var store = new MongoMediaFileStore(database, Options.Create(new MarketApi::Knock.Market.Api.Hosting.KnockMongoOptions()));
        var service = new MediaService(store, new AzureMediaBlobStore(new Azure.Storage.Blobs.BlobServiceClient(azurite.ConnectionString), new EnlaceSubidaNoDisponible(), Options.Create(options)), processor, new AllowAll(), TimeProvider.System);
        var links = new RecordingLinks();
        var api = new MediaApi(service, links, Options.Create(options));
        var request = await ConfirmAsync(service, store, azurite, options, "solicitud");
        var job = await ConfirmAsync(service, store, azurite, options, "trabajo");

        (await api.IsReadyOwnedAndUsedForAsync(request, _owner, "solicitud", default)).Should().BeTrue();
        (await api.IsReadyOwnedAndUsedForAsync(request, _owner, "trabajo", default)).Should().BeFalse();
        (await api.IsReadyOwnedAndUsedForAsync(job, _owner, "trabajo", default)).Should().BeTrue();
        (await api.IsReadyOwnedAndUsedForAsync(request, "someone-else", "solicitud", default)).Should().BeFalse();

        var result = await api.CreateReadLinksAsync([job, request], "solicitud", _reader, default);

        result.Select(link => link.FileId).Should().Equal(request);
        links.Requested.Should().ContainSingle().Which.Contenedor.Should().Be(options.FilesContainer);
        var requested = links.Requested[0];
        (await azurite.ListBlobNamesAsync(options.FilesContainer)).Should().Contain(requested.Nombre);
        requested.Nombre.Should().EndWith("/reducida.jpg");
    }

    private static async Task<Guid> ConfirmAsync(MediaService service, MongoMediaFileStore store, AzuriteFixture azurite, MediaOptions options, string use)
    {
        var photo = Jpeg();
        var ticket = await service.CreateUploadAsync(_owner, use, "image/jpeg", photo.Length, default);
        var id = ticket.Value!.Id;
        var key = (await store.FindAsync(id, default))!.File.QuarantineBlob;
        var container = azurite.Container(options.QuarantineContainer);
        await container.CreateIfNotExistsAsync();
        await container.GetBlobClient(key).UploadAsync(BinaryData.FromBytes(photo));
        (await service.ConfirmAsync(_owner, id, default)).Status.Should().Be(MediaOutcomeStatus.Success);
        return id;
    }

    private static byte[] Jpeg()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(800, 400));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.CornflowerBlue);
        using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    private sealed class RecordingLinks : IEnlacesPrivados
    {
        public List<BlobPrivado> Requested { get; } = [];

        public Task<EnlaceFirmado> CrearLecturaAsync(BlobPrivado blob, MotivoAcceso motivo, string subSolicitante, CancellationToken cancellationToken)
        {
            Requested.Add(blob);
            return Task.FromResult(new EnlaceFirmado(new Uri("https://files.test/" + blob.Nombre), DateTimeOffset.UtcNow.AddMinutes(5)));
        }
    }

    private sealed class AllowAll : IContentModerator
    {
        public Task<ModerationResult> ReviewTextAsync(string text, CancellationToken cancellationToken) => Task.FromResult(new ModerationResult(ModerationOutcome.Allowed));

        public Task<ModerationResult> ReviewImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) => Task.FromResult(new ModerationResult(ModerationOutcome.Allowed));
    }
}
