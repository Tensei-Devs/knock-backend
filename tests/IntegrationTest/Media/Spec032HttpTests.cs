extern alias MarketApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Knock.Contracts.Moderation;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Modules.Media.Data;
using MarketApi::Knock.Market.Api.Modules.Media.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkiaSharp;
using Xunit;

namespace Knock.IntegrationTest.Media;

/// <summary>
/// Spec 032, T-06 y T-09 sobre el canal HTTP de los dos endpoints de imágenes: 401, 413, 422, 400 y 404 en ProblemDetails, <c>Cache-Control: private, no-store</c> y sin
/// compresión (R-04, R-06, R-15). Host sin contenedores: almacenamiento, almacén de subidas y límites son dobles; la prueba con Azurite, MongoDB y Redis corre en el CI.
/// </summary>
public sealed class Spec032HttpTests
{
    private const string _ruta = "/api/v1/imagenes/subidas";

    private static MarketApiFactory Crear(byte[]? subido = null) => MarketApiFactory.CreateOffline(services =>
    {
        var reloj = TimeProvider.System;
        services.RemoveAll<IContentModerator>();
        services.AddSingleton<IContentModerator>(new ModeradorQuePermite());
        services.AddSingleton<IAlmacenBlob>(new AlmacenDoble(subido));
        services.AddSingleton<ISubidasImagenStore>(new SubidasImagenMemoriaStore());
        services.AddSingleton(new LimitesImagen(new ContadorVentanaMemoria(reloj), new ContadorVentanaMemoria(reloj)));
    });

    private static HttpClient Autenticado(MarketApiFactory factory, string sub)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", GatewayTestSupport.IssueToken(sub: sub, key: Convert.FromBase64String(MarketApiFactory.SigningKeyBase64)));
        return client;
    }

    private static async Task<HttpResponseMessage> Publicar(HttpClient client, string cuerpo)
    {
        using var contenido = new StringContent(cuerpo, Encoding.UTF8, "application/json");
        return await client.PostAsync(new Uri(_ruta, UriKind.Relative), contenido);
    }

    [Fact]
    public async Task Emite_Enlace_Sin_Cache_Ni_Compresion_CA_032_01()
    {
        using var factory = Crear();
        using var client = Autenticado(factory, "persona-1");
        using var contenidoPeticion = new StringContent("""{"proposito":"trabajo","tipoContenido":"image/jpeg","tamanoBytes":1000}""", Encoding.UTF8, "application/json");
        using var peticion = new HttpRequestMessage(HttpMethod.Post, _ruta) { Content = contenidoPeticion };
        peticion.Headers.AcceptEncoding.ParseAdd("gzip, br");

        using var respuesta = await client.SendAsync(peticion);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        respuesta.Headers.CacheControl!.NoStore.Should().BeTrue();
        respuesta.Content.Headers.ContentEncoding.Should().BeEmpty();
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        cuerpo.GetProperty("metodo").GetString().Should().Be("PUT");
        cuerpo.GetProperty("id").GetString().Should().HaveLength(32);
        cuerpo.GetProperty("urlSubida").GetString().Should().StartWith("https://");
    }

    [Fact]
    public async Task La_Confirmacion_Queda_Fuera_De_La_Compresion_CA_010_E03()
    {
        // La emisión responde 201 y la compresión solo toca los 200: la confirmación es la que probaría una exclusión ausente, con Vary: Accept-Encoding aunque pese menos de 1 KB.
        using var factory = Crear(subido: Jpeg());
        using var client = Autenticado(factory, "persona-1");
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, br");
        using var emitida = await Publicar(client, """{"proposito":"trabajo","tipoContenido":"image/jpeg","tamanoBytes":1000}""");
        var id = (await emitida.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        using var confirmada = await client.PostAsync(new Uri($"{_ruta}/{id}/confirmar", UriKind.Relative), null);

        confirmada.StatusCode.Should().Be(HttpStatusCode.OK);
        confirmada.Content.Headers.ContentEncoding.Should().BeEmpty();
        confirmada.Headers.Vary.Should().NotContain("Accept-Encoding");
        emitida.Content.Headers.ContentEncoding.Should().BeEmpty();
    }

    private static byte[] Jpeg()
    {
        using var bitmap = new SKBitmap(200, 100);
        bitmap.Erase(SKColors.Red);
        using var imagen = SKImage.FromBitmap(bitmap);
        using var datos = imagen.Encode(SKEncodedImageFormat.Jpeg, 90);
        return datos.ToArray();
    }

    [Theory]
    [InlineData("""{"proposito":"documento","tipoContenido":"image/jpeg","tamanoBytes":10}""", "proposito")]
    [InlineData("""{"proposito":"trabajo","tipoContenido":"image/svg+xml","tamanoBytes":10}""", "tipoContenido")]
    [InlineData("""{"proposito":"trabajo","tipoContenido":"image/png","tamanoBytes":5242881}""", "tamanoBytes")]
    public async Task Rechaza_Cuerpo_Invalido_CA_032_02(string cuerpo, string campo)
    {
        using var factory = Crear();
        using var client = Autenticado(factory, "persona-1");

        using var respuesta = await Publicar(client, cuerpo);

        respuesta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        problema.GetProperty("errors").TryGetProperty(campo, out _).Should().BeTrue();
        (await respuesta.Content.ReadAsStringAsync()).Should().NotContain("sig=");
    }

    [Fact]
    public async Task Exige_Sesion_Y_Limita_El_Cuerpo_A_1_KB_CA_032_02()
    {
        using var factory = Crear();
        using var anonimo = factory.CreateClient();
        using var sinSesion = await Publicar(anonimo, "{}");
        sinSesion.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var client = Autenticado(factory, "persona-1");
        using var grande = await Publicar(client, """{"proposito":"trabajo","x":"{relleno}"}""".Replace("{relleno}", new string('a', 2048), StringComparison.Ordinal));
        grande.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Subida_Ajena_Inexistente_Y_Mal_Formada_CA_032_15()
    {
        using var factory = Crear();
        using var duena = Autenticado(factory, "persona-1");
        using var otra = Autenticado(factory, "persona-2");
        using var emitida = await Publicar(duena, """{"proposito":"perfil","tipoContenido":"image/png","tamanoBytes":10}""");
        var id = (await emitida.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        using var ajena = await otra.PostAsync(new Uri($"{_ruta}/{id}/confirmar", UriKind.Relative), null);
        using var inexistente = await otra.PostAsync(new Uri($"{_ruta}/{new string('a', 32)}/confirmar", UriKind.Relative), null);
        using var malFormada = await otra.PostAsync(new Uri($"{_ruta}/no-es-un-id/confirmar", UriKind.Relative), null);

        ajena.StatusCode.Should().Be(HttpStatusCode.NotFound);
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ajena.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("upload_not_found");
        (await inexistente.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("upload_not_found");
        malFormada.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ajena.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    private sealed class ModeradorQuePermite : IContentModerator
    {
        public Task<ModerationResult> ReviewTextAsync(string text, CancellationToken cancellationToken) =>
            Task.FromResult(new ModerationResult(ModerationOutcome.Allowed));

        public Task<ModerationResult> ReviewImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) =>
            Task.FromResult(new ModerationResult(ModerationOutcome.Allowed));
    }

    private sealed class AlmacenDoble(byte[]? subido) : IAlmacenBlob
    {
        public Task<EnlaceSubida> CrearEnlaceSubidaAsync(string nombreBlob, string tipoContenido, CancellationToken cancellationToken) =>
            Task.FromResult(new EnlaceSubida(
                new Uri($"https://stprueba.blob.core.windows.net/imagenes-cuarentena/{nombreBlob}?sp=cw&sig=firma-de-prueba"),
                DateTimeOffset.UtcNow.AddMinutes(5),
                new Dictionary<string, string> { ["x-ms-blob-type"] = "BlockBlob", ["Content-Type"] = tipoContenido }));

        public Task<PropiedadesBlob?> ObtenerPropiedadesAsync(string nombreBlob, CancellationToken cancellationToken) =>
            Task.FromResult(subido is null ? null : new PropiedadesBlob(subido.Length, "etag"));

        public Task<byte[]?> DescargarAsync(string nombreBlob, PropiedadesBlob esperado, int maximoBytes, CancellationToken cancellationToken) => Task.FromResult(subido);

        public Task<bool> PublicarAsync(string ruta, byte[] bytes, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task BorrarCuarentenaAsync(string nombreBlob, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
