using System.Net;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using Xunit;

namespace Knock.SecurityTest.Messaging;

/// <summary>
/// Spec 044 (avisos dentro de la app), seguridad negativa sobre HTTP real con el host de Mensajería: 401 sin sesión, 422 sin repetir el valor, 413 por cuerpo excesivo y 429 con
/// <c>Retry-After</c>. Ninguna ruta llega a MongoDB (cadena sintética): todas se rechazan antes. Corre solo en CI (Redis en contenedor). Solo datos ficticios.
/// </summary>
public sealed class Spec044AvisosSeguridadTests
{
    private const string _notices = "/api/v1/avisos";
    private const string _anyNotice = "507f1f77bcf86cd799439011";

    [Theory]
    [InlineData("GET", _notices, "CA_044_02")]
    [InlineData("GET", _notices + "/no-leidos/contador", "CA_044_06")]
    [InlineData("POST", _notices + "/" + _anyNotice + "/leido", "CA_044_08")]
    [InlineData("POST", _notices + "/leidos", "CA_044_13")]
    [InlineData("GET", "/api/v1/me/preferencias-avisos", "CA_044_15")]
    public async Task WithoutAToken_Returns401AndRunsNothing_CA_044_02_06_08_13_15(string method, string path, string criterion)
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        using var withoutToken = await SecureHttp.SendAsync(client, new HttpMethod(method), path);
        using var expired = await SecureHttp.SendAsync(client, new HttpMethod(method), path, bearer: TokenForge.Expired());

        withoutToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized, criterion);
        expired.StatusCode.Should().Be(HttpStatusCode.Unauthorized, criterion);
    }

    [Theory]
    [InlineData("limite=51")]
    [InlineData("limite=0")]
    [InlineData("leido=quizas")]
    [InlineData("comoRol=admin")]
    [InlineData("cursor=llamame-al-7711234567")]
    public async Task AnInvalidQuery_Returns422WithErrorsPerFieldAndNeverEchoesTheValue_CA_044_03(string query)
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        using var response = await SecureHttp.SendAsync(client, HttpMethod.Get, $"{_notices}?{query}", bearer: TokenForge.Valid());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("errors");
        body.Should().NotContain(query.Split('=')[1], "el valor recibido nunca vuelve en el error");
    }

    [Fact]
    public async Task AnInvalidNoticeId_Returns422WithoutRepeatingIt_CA_044_10()
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        using var response = await SecureHttp.SendAsync(client, HttpMethod.Post, $"{_notices}/no-es-un-id/leido", bearer: TokenForge.Valid());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("no-es-un-id");
    }

    [Theory]
    [InlineData(_notices + "/leidos")]
    [InlineData(_notices + "/" + _anyNotice + "/leido")]
    public async Task APostWithAnOversizedBody_Returns413_CA_044_23(string path)
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        using var response = await SecureHttp.PostJsonAsync(client, path, new { relleno = new string('x', 4096) }, TokenForge.Valid());

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("xxxx");
    }

    [Fact]
    public async Task OverThePerPersonLimit_Returns429WithRetryAfter_CA_044_23()
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();
        var token = TokenForge.Valid();
        var other = TokenForge.Valid();
        HttpResponseMessage? limited = null;

        // El límite de lectura es de 120 por minuto por persona; la consulta inválida se rechaza (422) antes de tocar MongoDB y aun así cuenta.
        for (var i = 0; i < 125 && limited is null; i++)
        {
            var response = await SecureHttp.SendAsync(client, HttpMethod.Get, $"{_notices}?limite=999", bearer: token);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                limited = response;
            }
            else
            {
                response.Dispose();
            }
        }

        limited.Should().NotBeNull("la petición 121 de la misma persona debe recibir 429");
        limited.Headers.GetValues("Retry-After").Should().ContainSingle().Which.Should().MatchRegex("^[1-9][0-9]*$");
        limited.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        using var otherPerson = await SecureHttp.SendAsync(client, HttpMethod.Get, $"{_notices}?limite=999", bearer: other);
        otherPerson.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "el límite es por persona");
        limited.Dispose();
    }
}
