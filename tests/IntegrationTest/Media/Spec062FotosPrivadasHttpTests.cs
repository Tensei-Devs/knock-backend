extern alias MarketApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Modules.Media.Data;
using MarketApi::Knock.Market.Api.Modules.Media.Domain;
using MarketApi::Knock.Market.Api.Modules.Media.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Knock.IntegrationTest.Media;

/// <summary>
/// Spec 062 sobre el canal HTTP: <c>POST /api/v1/imagenes/subidas</c> con <c>proposito = solicitud</c> emite el enlace del flujo privado (CA-01), respeta el tope de 10 pendientes con
/// 429 <c>limit_exceeded</c> (CA-04), y <c>POST /api/v1/imagenes/subidas/{id}/confirmar</c> responde 404 uniforme a una foto ajena (CA-06) y 409 si nada se subió. Host sin contenedores:
/// archivos y blobs son dobles en memoria; la prueba con Azurite y MongoDB corre en el CI.
/// </summary>
public sealed class Spec062FotosPrivadasHttpTests
{
    private const string _ruta = "/api/v1/imagenes/subidas";
    private const string _cuerpo = """{"proposito":"solicitud","tipoContenido":"image/jpeg","tamanoBytes":1000}""";

    private static MarketApiFactory Crear(IMediaBlobStore? blobs = null) => MarketApiFactory.CreateOffline(
        services =>
        {
            services.RemoveAll<IMediaFileStore>();
            services.RemoveAll<IMediaBlobStore>();
            services.AddSingleton<IMediaFileStore>(new InMemoryMediaFileStore());
            services.AddSingleton<IMediaBlobStore>(blobs ?? new InMemoryMediaBlobStore());
            services.AddSingleton<ISubidasImagenStore>(new SubidasImagenMemoriaStore());
            services.AddSingleton(new LimitesImagen(new ContadorVentanaMemoria(TimeProvider.System), new ContadorVentanaMemoria(TimeProvider.System)));
        },
        new Dictionary<string, string?> { ["ConnectionStrings:KnockBlob"] = "UseDevelopmentStorage=true" });

    private static HttpClient Autenticado(MarketApiFactory factory, string sub)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", GatewayTestSupport.IssueToken(sub: sub, key: Convert.FromBase64String(MarketApiFactory.SigningKeyBase64)));
        return client;
    }

    private static async Task<HttpResponseMessage> Pedir(HttpClient client)
    {
        using var contenido = JsonContent.Create(JsonElement.Parse(_cuerpo));
        return await client.PostAsync(new Uri(_ruta, UriKind.Relative), contenido);
    }

    [Fact]
    public async Task PedirEnlace_Solicitud_Emite_201_Con_Id_De_32_Hexadecimales_Y_Sin_Cache_CA_062_01()
    {
        using var factory = Crear();
        using var client = Autenticado(factory, "persona-1");

        using var respuesta = await Pedir(client);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync());
        respuesta.Headers.CacheControl!.NoStore.Should().BeTrue();
        var json = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("id").GetString().Should().MatchRegex("^[0-9a-f]{32}$");
        json.GetProperty("urlSubida").GetString().Should().StartWith("https://");
        json.GetProperty("metodo").GetString().Should().Be("PUT");
        json.GetProperty("encabezados").GetProperty("Content-Type").GetString().Should().Be("image/jpeg");
    }

    [Fact]
    public async Task PedirEnlace_Undecima_Subida_Pendiente_Responde_429_Limit_Exceeded_CA_062_04()
    {
        using var factory = Crear();
        using var client = Autenticado(factory, "persona-2");
        for (var i = 0; i < 10; i++)
        {
            using var ok = await Pedir(client);
            ok.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        using var respuesta = await Pedir(client);

        respuesta.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        respuesta.Headers.RetryAfter.Should().NotBeNull();
        (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("limit_exceeded");
    }

    [Fact]
    public async Task Confirmar_Sin_Haber_Subido_Responde_409_File_Not_Uploaded_Y_Ajena_404_Uniforme_CA_062_06()
    {
        using var factory = Crear();
        using var dueno = Autenticado(factory, "persona-3");
        using var otra = Autenticado(factory, "persona-4");
        using var emitida = await Pedir(dueno);
        var id = (await emitida.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        using var sinSubir = await dueno.PostAsync(new Uri($"{_ruta}/{id}/confirmar", UriKind.Relative), null);
        using var ajena = await otra.PostAsync(new Uri($"{_ruta}/{id}/confirmar", UriKind.Relative), null);

        sinSubir.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await sinSubir.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("file_not_uploaded");
        ajena.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ajena.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("upload_not_found");
    }

    [Fact]
    public async Task PedirEnlace_Con_El_Almacenamiento_Caido_Responde_503_Storage_Unavailable_Y_No_Moderation_CA_062_07()
    {
        using var factory = Crear(new LinkFailingBlobStore());
        using var client = Autenticado(factory, "persona-5");

        using var respuesta = await Pedir(client);

        respuesta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("storage_unavailable");
    }

    /// <summary>Almacén de blobs cuyo enlace de subida falla como falla un Azure caído; el resto delega en el doble en memoria.</summary>
    private sealed class LinkFailingBlobStore : IMediaBlobStore
    {
        private readonly InMemoryMediaBlobStore _inner = new();

        public Task<EnlaceSubida> CreateUploadLinkAsync(string quarantineBlob, string contentType, CancellationToken cancellationToken) =>
            throw new IOException("Blob storage is unavailable.");

        public Task EnsureContainersAsync(CancellationToken cancellationToken) => _inner.EnsureContainersAsync(cancellationToken);

        public Task<UploadedBlob?> GetUploadedAsync(string quarantineBlob, CancellationToken cancellationToken) => _inner.GetUploadedAsync(quarantineBlob, cancellationToken);

        public Task<byte[]?> DownloadQuarantineAsync(string quarantineBlob, UploadedBlob expected, CancellationToken cancellationToken) =>
            _inner.DownloadQuarantineAsync(quarantineBlob, expected, cancellationToken);

        public Task UploadFileAsync(string fileBlob, byte[] content, ImageFormat format, CancellationToken cancellationToken) =>
            _inner.UploadFileAsync(fileBlob, content, format, cancellationToken);

        public Task DeleteQuarantineAsync(string quarantineBlob, CancellationToken cancellationToken) => _inner.DeleteQuarantineAsync(quarantineBlob, cancellationToken);
    }
}
