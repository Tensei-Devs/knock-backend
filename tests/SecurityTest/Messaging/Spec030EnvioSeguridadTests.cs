using System.Net;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using Xunit;

namespace Knock.SecurityTest.Messaging;

/// <summary>
/// Spec 030, T-06 (KNOCK-73): seguridad negativa de <c>POST /api/v1/conversaciones/{id}/mensajes</c> que no necesita base de datos: sin sesión, con sesión inválida y con cuerpos mal formados
/// o demasiado grandes la petición se corta antes de tocar MongoDB, Redis o Mercado. Solo datos ficticios.
/// </summary>
public sealed class Spec030EnvioSeguridadTests
{
    private const string _path = "/api/v1/conversaciones/507f1f77bcf86cd799439011/mensajes";

    [Fact]
    public async Task WithoutAToken_Returns401_AndNothingElse_CA_030_20()
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        using var response = await SecureHttp.PostJsonAsync(client, _path, new { texto = "hola" });

        var raw = await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
        raw.Should().Contain("unauthorized");
    }

    [Fact]
    public async Task WithAnExpiredOrForgedToken_Returns401_CA_030_20()
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        using var expired = await SecureHttp.PostJsonAsync(client, _path, new { texto = "hola" }, TokenForge.Expired());
        using var forged = await SecureHttp.PostJsonAsync(client, _path, new { texto = "hola" }, TokenForge.SignatureStripped());

        await SecureHttp.AssertProblemAsync(expired, HttpStatusCode.Unauthorized);
        await SecureHttp.AssertProblemAsync(forged, HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("{ no es json")]
    [InlineData("")]
    [InlineData("[1,2,3]")]
    public async Task AnUnreadableBody_Returns400_AndNeverEchoesIt_CA_030_02(string body)
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        using var response = await SecureHttp.SendRawJsonAsync(client, HttpMethod.Post, _path, body, TokenForge.Valid());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var raw = await response.Content.ReadAsStringAsync();
        if (body.Length > 0)
        {
            Knock.IntegrationTest.Support.ProblemBody.Reasons(raw).Should().NotContain(body);
        }
    }

    [Fact]
    public async Task ABodyOver4KB_IsRejectedBeforeItIsRead_CA_030_02()
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        using var response = await SecureHttp.PostJsonAsync(client, _path, new { texto = new string('a', 5_000) }, TokenForge.Valid());

        response.StatusCode.Should().BeOneOf(HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.UnprocessableEntity, HttpStatusCode.BadRequest);
        response.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task AnInvalidText_Returns422WithoutTouchingTheStore_CA_030_02()
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();
        const string secret = "llámame al 7711234567 \u0007";

        using var response = await SecureHttp.PostJsonAsync(client, _path, new { texto = secret }, TokenForge.Valid());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "la validación responde antes de abrir MongoDB (que aquí ni existe)");
        Knock.IntegrationTest.Support.ProblemBody.Reasons(await response.Content.ReadAsStringAsync()).Should().NotContain("7711234567");
    }
}
