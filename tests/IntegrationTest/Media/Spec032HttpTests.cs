extern alias MarketApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Modules.Media.Data;
using MarketApi::Knock.Market.Api.Modules.Media.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Knock.IntegrationTest.Media;

/// <summary>
/// Spec 032, T-06 y T-09 sobre el canal HTTP de los dos endpoints de imágenes: 401, 413, 422, 400 y 404 en ProblemDetails, <c>Cache-Control: private, no-store</c> y sin
/// compresión (R-04, R-06, R-15). Host sin contenedores: almacenamiento, almacén de subidas y límites son dobles; la prueba con Azurite, MongoDB y Redis corre en el CI.
/// </summary>
public sealed class Spec032HttpTests
{
    private const string _ruta = "/api/v1/imagenes/subidas";

    private static MarketApiFactory Crear() => MarketApiFactory.CreateOffline(services =>
    {
        var reloj = TimeProvider.System;
        services.AddSingleton<IAlmacenBlob>(new AlmacenDoble());
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

    private sealed class AlmacenDoble : IAlmacenBlob
    {
        public Task<EnlaceSubida> CrearEnlaceSubidaAsync(string nombreBlob, string tipoContenido, CancellationToken cancellationToken) =>
            Task.FromResult(new EnlaceSubida(
                new Uri($"https://stprueba.blob.core.windows.net/imagenes-cuarentena/{nombreBlob}?sp=cw&sig=firma-de-prueba"),
                DateTimeOffset.UtcNow.AddMinutes(5),
                new Dictionary<string, string> { ["x-ms-blob-type"] = "BlockBlob", ["Content-Type"] = tipoContenido }));

        public Task<PropiedadesBlob?> ObtenerPropiedadesAsync(string nombreBlob, CancellationToken cancellationToken) => Task.FromResult<PropiedadesBlob?>(null);

        public Task<byte[]?> DescargarAsync(string nombreBlob, PropiedadesBlob esperado, int maximoBytes, CancellationToken cancellationToken) => Task.FromResult<byte[]?>(null);

        public Task<bool> PublicarAsync(string ruta, byte[] bytes, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task BorrarCuarentenaAsync(string nombreBlob, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
