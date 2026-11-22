using AwesomeAssertions;
using Knock.Contracts.Moderation;
using Knock.Market.Api.Modules.Media.Data;
using Knock.Market.Api.Modules.Media.Domain;
using Knock.Market.Api.Modules.Media.Options;
using Knock.Market.Api.Modules.Media.Services;
using SkiaSharp;
using Xunit;

namespace Knock.UnitTest.Media;

/// <summary>Spec 032, T-06: <c>SubidaImagenService</c> con almacenamiento, moderador y reloj de prueba (CA-01, CA-11 a CA-17, CA-21).</summary>
public sealed class Spec032SubidaServiceTests : IDisposable
{
    private const string _sub = "persona-1";
    private const string _base = "https://stknock.blob.core.windows.net/imagenes-publicas";
    private readonly ImageProcessor _puerta = new(Microsoft.Extensions.Options.Options.Create(new MediaOptions { MaxConcurrentProcessing = 2 }));
    private readonly AlmacenFalso _almacen = new();
    private readonly SubidasImagenMemoriaStore _store = new();
    private readonly ModeradorFalso _moderador = new();
    private readonly RelojFalso _reloj = new();

    public void Dispose() => _puerta.Dispose();

    private SubidaImagenService Crear(IAlmacenBlob? almacen = null)
    {
        var limites = new LimitesImagen(new ContadorVentanaMemoria(_reloj), new ContadorVentanaMemoria(_reloj));
        var url = new ImagenPublicaUrl(Microsoft.Extensions.Options.Options.Create(new ImagenesOptions { BaseUrlPublica = _base }));
        return new SubidaImagenService(almacen ?? _almacen, _store, limites, new ProcesadorImagen(_puerta), _moderador, url, _reloj);
    }

    private static byte[] Jpeg()
    {
        using var bitmap = new SKBitmap(200, 100);
        bitmap.Erase(SKColors.Red);
        using var imagen = SKImage.FromBitmap(bitmap);
        return imagen.Encode(SKEncodedImageFormat.Jpeg, 90).ToArray();
    }

    private async Task<(string Id, string Clave)> PedirAsync(SubidaImagenService servicio)
    {
        var respuesta = await servicio.PedirEnlaceAsync(_sub, PropositoImagen.Trabajo, "image/jpeg", 1000, CancellationToken.None);
        respuesta.Resultado.Should().Be(ResultadoSubida.Ok);
        var subida = await _store.FindOwnedAsync(respuesta.Valor!.Id, _sub, CancellationToken.None);
        return (respuesta.Valor.Id, subida!.ClaveBlob);
    }

    [Fact]
    public async Task Emite_Enlace_De_Un_Blob_CA_032_01()
    {
        var respuesta = await Crear().PedirEnlaceAsync(_sub, PropositoImagen.Trabajo, "image/jpeg", 1000, CancellationToken.None);

        respuesta.Resultado.Should().Be(ResultadoSubida.Ok);
        respuesta.Valor!.Metodo.Should().Be("PUT");
        respuesta.Valor.Id.Should().HaveLength(32);
        respuesta.Valor.ConfirmarAntesDe.Should().Be(_reloj.GetUtcNow().AddMinutes(30));
        var subida = await _store.FindOwnedAsync(respuesta.Valor.Id, _sub, CancellationToken.None);
        subida!.ClaveBlob.Should().NotBe(respuesta.Valor.Id).And.HaveLength(32);
        subida.Estado.Should().Be(EstadoSubida.Pendiente);
    }

    [Fact]
    public async Task Confirmacion_Idempotente_CA_032_14()
    {
        var servicio = Crear();
        var (id, clave) = await PedirAsync(servicio);
        _almacen.Subir(clave, Jpeg());

        var primera = await servicio.ConfirmarAsync(_sub, id, CancellationToken.None);
        var segunda = await servicio.ConfirmarAsync(_sub, id, CancellationToken.None);

        primera.Resultado.Should().Be(ResultadoSubida.Ok);
        segunda.Valor!.Url.Should().Be(primera.Valor!.Url);
        _moderador.Llamadas.Should().Be(1, "la segunda confirmación no vuelve a moderar");
        _almacen.Cuarentena.Should().BeEmpty();
    }

    [Fact]
    public async Task Misma_Imagen_Un_Blob_CA_032_17()
    {
        var servicio = Crear();
        var bytes = Jpeg();
        var (id1, clave1) = await PedirAsync(servicio);
        var (id2, clave2) = await PedirAsync(servicio);
        _almacen.Subir(clave1, bytes);
        _almacen.Subir(clave2, bytes);

        var a = await servicio.ConfirmarAsync(_sub, id1, CancellationToken.None);
        var b = await servicio.ConfirmarAsync(_sub, id2, CancellationToken.None);

        b.Valor!.Url.Should().Be(a.Valor!.Url);
        _almacen.Publicos.Should().HaveCount(1);
    }

    [Fact]
    public async Task Subida_Ajena_404_CA_032_15()
    {
        var servicio = Crear();
        var (id, _) = await PedirAsync(servicio);

        (await servicio.ConfirmarAsync("otra-persona", id, CancellationToken.None)).Resultado.Should().Be(ResultadoSubida.UploadNotFound);
        (await servicio.ConfirmarAsync(_sub, new string('a', 32), CancellationToken.None)).Resultado.Should().Be(ResultadoSubida.UploadNotFound);
    }

    [Fact]
    public async Task Sin_Blob_O_Vencida_CA_032_16()
    {
        var servicio = Crear();
        var (id, _) = await PedirAsync(servicio);

        (await servicio.ConfirmarAsync(_sub, id, CancellationToken.None)).Resultado.Should().Be(ResultadoSubida.FileNotUploaded);

        _reloj.Avanzar(TimeSpan.FromMinutes(31));
        (await servicio.ConfirmarAsync(_sub, id, CancellationToken.None)).Resultado.Should().Be(ResultadoSubida.UploadExpired);
    }

    [Fact]
    public async Task Rechaza_Archivo_Grande_CA_032_11()
    {
        var servicio = Crear();
        var (id, clave) = await PedirAsync(servicio);
        _almacen.Subir(clave, new byte[6 * 1024 * 1024]);

        var respuesta = await servicio.ConfirmarAsync(_sub, id, CancellationToken.None);

        respuesta.Resultado.Should().Be(ResultadoSubida.ImageTooLarge);
        _almacen.Descargas.Should().Be(0, "se rechaza por las propiedades, sin descargar");
        _almacen.Cuarentena.Should().BeEmpty();
    }

    [Fact]
    public async Task Imagen_Rechazada_CA_032_12()
    {
        _moderador.Resultado = ModerationOutcome.Rejected;
        var servicio = Crear();
        var (id, clave) = await PedirAsync(servicio);
        _almacen.Subir(clave, Jpeg());

        var respuesta = await servicio.ConfirmarAsync(_sub, id, CancellationToken.None);

        respuesta.Resultado.Should().Be(ResultadoSubida.ImageRejected);
        _almacen.Publicos.Should().BeEmpty();
        (await _store.FindOwnedAsync(id, _sub, CancellationToken.None))!.Estado.Should().Be(EstadoSubida.Rechazada);
        (await servicio.ConfirmarAsync(_sub, id, CancellationToken.None)).Resultado.Should().Be(ResultadoSubida.ImageRejected);
    }

    [Fact]
    public async Task Moderacion_Caida_Falla_Cerrado_CA_032_13()
    {
        _moderador.Resultado = ModerationOutcome.Unavailable;
        var servicio = Crear();
        var (id, clave) = await PedirAsync(servicio);
        _almacen.Subir(clave, Jpeg());

        var fallida = await servicio.ConfirmarAsync(_sub, id, CancellationToken.None);

        fallida.Resultado.Should().Be(ResultadoSubida.ModerationUnavailable);
        fallida.ReintentarEn.Should().NotBeNull();
        _almacen.Publicos.Should().BeEmpty();
        _almacen.Cuarentena.Should().ContainSingle("la subida sigue confirmable");

        _moderador.Resultado = ModerationOutcome.Allowed;
        (await servicio.ConfirmarAsync(_sub, id, CancellationToken.None)).Resultado.Should().Be(ResultadoSubida.Ok);
    }

    [Fact]
    public async Task Formato_Falso_Se_Rechaza_Y_Borra_CA_032_09()
    {
        var servicio = Crear();
        var (id, clave) = await PedirAsync(servicio);
        _almacen.Subir(clave, "<html><script>alert(1)</script></html>"u8.ToArray());

        var respuesta = await servicio.ConfirmarAsync(_sub, id, CancellationToken.None);

        respuesta.Resultado.Should().Be(ResultadoSubida.FormatNotSupported);
        _almacen.Publicos.Should().BeEmpty();
        _almacen.Cuarentena.Should().BeEmpty();
    }

    [Fact]
    public async Task Almacenamiento_Caido_CA_032_21()
    {
        var servicio = Crear(new AlmacenCaido());

        var pedir = await servicio.PedirEnlaceAsync(_sub, PropositoImagen.Perfil, "image/png", 10, CancellationToken.None);

        pedir.Resultado.Should().Be(ResultadoSubida.StorageUnavailable);
        pedir.ReintentarEn.Should().NotBeNull();
    }

    [Fact]
    public async Task Limite_De_Enlaces_Y_Pendientes_CA_032_18()
    {
        var servicio = Crear();
        for (var i = 0; i < 10; i++)
        {
            (await servicio.PedirEnlaceAsync(_sub, PropositoImagen.Trabajo, "image/jpeg", 10, CancellationToken.None)).Resultado.Should().Be(ResultadoSubida.Ok);
        }

        var once = await servicio.PedirEnlaceAsync(_sub, PropositoImagen.Trabajo, "image/jpeg", 10, CancellationToken.None);

        once.Resultado.Should().Be(ResultadoSubida.LimitExceeded, "el tope de 10 pendientes vigentes");
        once.ReintentarEn.Should().NotBeNull();
    }

    private sealed class RelojFalso : TimeProvider
    {
        private DateTimeOffset _actual = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

        public void Avanzar(TimeSpan t) => _actual += t;

        public override DateTimeOffset GetUtcNow() => _actual;
    }

    private sealed class ModeradorFalso : IContentModerator
    {
        public ModerationOutcome Resultado { get; set; } = ModerationOutcome.Allowed;

        public int Llamadas { get; private set; }

        public Task<ModerationResult> ReviewTextAsync(string text, CancellationToken cancellationToken) => Task.FromResult(new ModerationResult(Resultado));

        public Task<ModerationResult> ReviewImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken)
        {
            Llamadas++;
            return Task.FromResult(new ModerationResult(Resultado));
        }
    }

    private sealed class AlmacenCaido : IAlmacenBlob
    {
        public Task<EnlaceSubida> CrearEnlaceSubidaAsync(string nombreBlob, string tipoContenido, CancellationToken cancellationToken) => throw new IOException("Blob storage is unavailable.");

        public Task<PropiedadesBlob?> ObtenerPropiedadesAsync(string nombreBlob, CancellationToken cancellationToken) => throw new IOException("Blob storage is unavailable.");

        public Task<byte[]?> DescargarAsync(string nombreBlob, PropiedadesBlob esperado, int maximoBytes, CancellationToken cancellationToken) => throw new IOException("Blob storage is unavailable.");

        public Task<bool> PublicarAsync(string ruta, byte[] bytes, CancellationToken cancellationToken) => throw new IOException("Blob storage is unavailable.");

        public Task BorrarCuarentenaAsync(string nombreBlob, CancellationToken cancellationToken) => throw new IOException("Blob storage is unavailable.");
    }

    private sealed class AlmacenFalso : IAlmacenBlob
    {
        public Dictionary<string, byte[]> Cuarentena { get; } = [];

        public Dictionary<string, byte[]> Publicos { get; } = [];

        public int Descargas { get; private set; }

        public void Subir(string clave, byte[] bytes) => Cuarentena[clave] = bytes;

        public Task<EnlaceSubida> CrearEnlaceSubidaAsync(string nombreBlob, string tipoContenido, CancellationToken cancellationToken) =>
            Task.FromResult(new EnlaceSubida(
                new Uri($"https://stknock.blob.core.windows.net/imagenes-cuarentena/{nombreBlob}?sp=cw"),
                DateTimeOffset.UnixEpoch,
                new Dictionary<string, string> { ["x-ms-blob-type"] = "BlockBlob", ["Content-Type"] = tipoContenido }));

        public Task<PropiedadesBlob?> ObtenerPropiedadesAsync(string nombreBlob, CancellationToken cancellationToken) =>
            Task.FromResult(Cuarentena.TryGetValue(nombreBlob, out var b) ? new PropiedadesBlob(b.Length, "etag") : null);

        public Task<byte[]?> DescargarAsync(string nombreBlob, PropiedadesBlob esperado, int maximoBytes, CancellationToken cancellationToken)
        {
            Descargas++;
            return Task.FromResult(Cuarentena.TryGetValue(nombreBlob, out var b) ? b : null);
        }

        public Task<bool> PublicarAsync(string ruta, byte[] bytes, CancellationToken cancellationToken) =>
            Task.FromResult(Publicos.TryAdd(ruta, bytes));

        public Task BorrarCuarentenaAsync(string nombreBlob, CancellationToken cancellationToken)
        {
            Cuarentena.Remove(nombreBlob);
            return Task.CompletedTask;
        }
    }
}
