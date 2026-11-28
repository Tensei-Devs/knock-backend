using System.Web;
using AwesomeAssertions;
using Knock.Contracts.Moderation;
using Knock.Market.Api.Modules.Media;
using Knock.Market.Api.Modules.Media.Data;
using Knock.Market.Api.Modules.Media.Domain;
using Knock.Market.Api.Modules.Media.Options;
using Knock.Market.Api.Modules.Media.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Knock.UnitTest.Media;

/// <summary>Spec 020 (R-04, CA-18), 032 (R-16, CA-20) y 058: <c>IMediaApi</c> verifica el <c>uso</c> de la foto y emite enlaces de lectura de 5 minutos de la versión reducida, con registro de acceso.</summary>
public sealed class Spec020bMediaUsoEnlacesTests : IDisposable
{
    private static readonly DateTimeOffset _ahora = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    private const string _owner = "owner-1";
    private const string _reader = "technician-1";

    private readonly InMemoryMediaFileStore _store = new();
    private readonly ImageProcessor _processor = new(Options.Create(new MediaOptions()));
    private readonly Accesos _accesos = new();
    private readonly AlmacenBlob _almacen;
    private readonly MediaService _service;
    private readonly MediaApi _api;

    public Spec020bMediaUsoEnlacesTests()
    {
        var reloj = new Spec032SasTests.RelojFalso(_ahora);
        _almacen = new AlmacenBlob(new Spec032SasTests.ServicioFalso("stknockprivada", _ahora), new Spec032SasTests.ServicioFalso("stknockpublica", _ahora), reloj);
        _service = new MediaService(_store, new InMemoryMediaBlobStore(), _processor, new AllowAll(), reloj, Options.Create(new MediaOptions()));
        var huella = new HuellaSubHmac(Options.Create(new ImagenesOptions { LlaveBusqueda = Convert.ToBase64String(new byte[32]) }));
        _api = NewApi(_service, new EnlacesPrivados(_almacen, _accesos, huella, reloj));
    }

    public void Dispose()
    {
        _almacen.Dispose();
        _processor.Dispose();
    }

    /// <summary>Fachada pública con enlaces de mentira; para las pruebas que solo usan la verificación de dueño.</summary>
    internal static MediaApi NewApi(MediaService service, IEnlacesPrivados? links = null) =>
        new(service, links ?? new SinEnlaces(), Options.Create(new MediaOptions()));

    [Fact]
    public async Task IsReadyOwnedAndUsedForAsync_OnlyTheRightUseOwnerAndReady_CA_020_18()
    {
        var request = await Ready(_owner, "solicitud");
        var job = await Ready(_owner, "trabajo");
        var profile = await Ready(_owner, "perfil");
        var pending = await Pending(_owner, "solicitud");

        (await _api.IsReadyOwnedAndUsedForAsync(request, _owner, "solicitud", default)).Should().BeTrue();
        (await _api.IsReadyOwnedAndUsedForAsync(job, _owner, "trabajo", default)).Should().BeTrue();
        (await _api.IsReadyOwnedAndUsedForAsync(job, _owner, "solicitud", default)).Should().BeFalse("another use");
        (await _api.IsReadyOwnedAndUsedForAsync(request, _owner, "trabajo", default)).Should().BeFalse("another use");
        (await _api.IsReadyOwnedAndUsedForAsync(profile, _owner, "solicitud", default)).Should().BeFalse("a profile photo is not a request photo");
        (await _api.IsReadyOwnedAndUsedForAsync(request, "someone-else", "solicitud", default)).Should().BeFalse("not the owner");
        (await _api.IsReadyOwnedAndUsedForAsync(pending, _owner, "solicitud", default)).Should().BeFalse("not ready");
        (await _api.IsReadyOwnedAndUsedForAsync(Guid.CreateVersion7(), _owner, "solicitud", default)).Should().BeFalse("does not exist");
    }

    [Fact]
    public async Task CreateReadLinksAsync_SignsTheReducedBlobForFiveMinutesAndLogsTheAccess_CA_032_20()
    {
        var id = await Ready(_owner, "solicitud");
        var stored = (await _store.FindAsync(id, default))!.File;

        var links = await _api.CreateReadLinksAsync([id], "solicitud", _reader, default);

        var link = links.Should().ContainSingle().Subject;
        link.FileId.Should().Be(id);
        link.ExpiresAt.Should().Be(_ahora.AddMinutes(5));
        link.Url.AbsolutePath.Should().EndWith($"/archivos/{stored.ReducedBlob}");
        var query = HttpUtility.ParseQueryString(link.Url.Query);
        query["sp"].Should().Be("r");
        query["sr"].Should().Be("b");
        query["spr"].Should().Be("https");
        query["rscc"].Should().Be("no-store");
        var access = _accesos.Registros.Should().ContainSingle().Subject;
        access.Motivo.Should().Be("foto_solicitud");
        access.SubHmac.Should().HaveLength(64);
        access.SubHmac.Should().NotContain(_reader);
        access.BlobId.Should().NotContain(stored.BlobKey);
    }

    [Fact]
    public async Task CreateReadLinksAsync_LeavesOutFilesThatAreNotReadyOrHaveAnotherUseAndKeepsOrder_CA_020_18()
    {
        var first = await Ready(_owner, "solicitud");
        var other = await Ready(_owner, "perfil");
        var pending = await Pending(_owner, "solicitud");
        var last = await Ready("owner-2", "solicitud");

        var links = await _api.CreateReadLinksAsync([last, other, Guid.CreateVersion7(), pending, first], "solicitud", _reader, default);

        links.Select(l => l.FileId).Should().Equal(last, first);
        _accesos.Registros.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateReadLinksAsync_ForJobPhotosUsesTheJobReason_CA_032_20()
    {
        var id = await Ready(_owner, "trabajo");

        (await _api.CreateReadLinksAsync([id], "trabajo", _reader, default)).Should().ContainSingle();

        _accesos.Registros.Should().ContainSingle().Which.Motivo.Should().Be("foto_trabajo");
    }

    [Fact]
    public async Task CreateReadLinksAsync_RequiresTheRequesterAndAKnownUse_CA_032_20()
    {
        var id = await Ready(_owner, "solicitud");

        await FluentActions.Awaiting(() => _api.CreateReadLinksAsync([id], "solicitud", "", default)).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => _api.CreateReadLinksAsync([id], "perfil", _reader, default)).Should().ThrowAsync<ArgumentOutOfRangeException>();
        (await _api.IsReadyOwnedAndUsedForAsync(id, _owner, "perfil", default)).Should().BeFalse("profile photos are not exposed to other modules");
        _accesos.Registros.Should().BeEmpty();
    }

    [Fact]
    public void FilesContainer_DefaultIsTheOnlyPrivatePhotoContainerThatCanBeLinked_CA_032_20()
    {
        BlobPrivado.ContenedoresPermitidos.Should().Contain(new MediaOptions().FilesContainer);
        BlobPrivado.ContenedoresPermitidos.Should().NotContain(ImagenesOptions.ContenedorCuarentena);
        BlobPrivado.ContenedoresPermitidos.Should().NotContain(ImagenesOptions.ContenedorPublico);
        BlobPrivado.ContenedoresPermitidos.Should().NotContain(new MediaOptions().QuarantineContainer);
    }

    private async Task<Guid> Pending(string owner, string use)
    {
        var ticket = await _service.CreateUploadAsync(owner, use, "image/jpeg", 1000, default);
        return ticket.Value!.Id;
    }

    private async Task<Guid> Ready(string owner, string use)
    {
        var id = await Pending(owner, use);
        var stored = (await _store.FindAsync(id, default))!;
        (await _store.TryReplaceAsync(stored.File.MarkReady(10, 10, _ahora), stored.Version, default)).Should().BeTrue();
        return id;
    }

    private sealed class Accesos : IAccesosPrivadosStore
    {
        public List<AccesoPrivado> Registros { get; } = [];

        public Task InsertAsync(AccesoPrivado acceso, CancellationToken cancellationToken)
        {
            Registros.Add(acceso);
            return Task.CompletedTask;
        }
    }

    private sealed class SinEnlaces : IEnlacesPrivados
    {
        public Task<EnlaceFirmado> CrearLecturaAsync(BlobPrivado blob, MotivoAcceso motivo, string subSolicitante, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class AllowAll : IContentModerator
    {
        public Task<ModerationResult> ReviewTextAsync(string text, CancellationToken cancellationToken) => Task.FromResult(new ModerationResult(ModerationOutcome.Allowed));

        public Task<ModerationResult> ReviewImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) => Task.FromResult(new ModerationResult(ModerationOutcome.Allowed));
    }
}
