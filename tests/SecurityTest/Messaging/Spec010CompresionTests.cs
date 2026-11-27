using System.Net;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using Xunit;

namespace Knock.SecurityTest.Messaging;

/// <summary>Spec 010, R-09 y enmienda 2026-11-27: Mensajería comprime el JSON de más de 1 KB, salvo lo que cuelga de <c>/hubs</c> y de <c>/api/v1/interno</c>. Corre solo en CI (Redis en contenedor).</summary>
public sealed class Spec010CompresionTests
{
    private const string _large = "/sonda/protegida/grande";

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? acceptEncoding)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Authorization = new("Bearer", TokenForge.Valid());
        if (acceptEncoding is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Encoding", acceptEncoding);
        }

        return await client.SendAsync(request);
    }

    [Theory]
    [InlineData("gzip, br", "br")]
    [InlineData("gzip", "gzip")]
    public async Task LargeJson_IsCompressedWithVary_CA_010_E01(string accepted, string expected)
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        using var response = await SendAsync(client, HttpMethod.Get, _large, accepted);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentEncoding.Should().ContainSingle().Which.Should().Be(expected);
        response.Headers.Vary.Should().Contain("Accept-Encoding");
    }

    [Fact]
    public async Task LargeJsonWithoutAcceptEncoding_IsNotCompressed_CA_010_E01()
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        using var response = await SendAsync(client, HttpMethod.Get, _large, acceptEncoding: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentEncoding.Should().BeEmpty();
        response.Headers.Vary.Should().Contain("Accept-Encoding");
    }

    [Theory]
    [InlineData("/api/v1/interno/sonda-grande")]
    [InlineData("/hubs/sonda-grande")]
    public async Task InternalRoutesAndHubs_AreNeverCompressed_CA_010_E02(string path)
    {
        // La puerta del chat responde 503 bajo /hubs/chat con el chat apagado; esta sonda cuelga de /hubs pero no de /hubs/chat.
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        using var response = await SendAsync(client, HttpMethod.Get, path, "gzip, br");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentEncoding.Should().BeEmpty();
        response.Headers.Vary.Should().NotContain("Accept-Encoding", "la ruta queda fuera del middleware: no se retiene en el búfer");
    }

    [Fact]
    public async Task TheChatNegotiation_IsNeverCompressed_CA_010_E02()
    {
        await using var host = await SecurityMessagingFactory.CreateAsync(new Dictionary<string, string?> { ["Chat:Habilitado"] = "true" });
        using var client = host.CreateClient();

        using var response = await SendAsync(client, HttpMethod.Post, "/hubs/chat/negotiate?negotiateVersion=1", "gzip, br");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentEncoding.Should().BeEmpty();
        response.Headers.Vary.Should().NotContain("Accept-Encoding");
    }
}
