using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using MongoDB.Bson;
using Xunit;

namespace Knock.SecurityTest.Requests;

/// <summary>Spec 063: las lecturas de la cotización final y de los ajustes pendientes exigen sesión y rechazan un <c>id</c> mal formado sin repetirlo. Mercado real en Production.</summary>
public sealed class Spec063LecturaSeguridadTests
{
    private const string _badId = "no-es-un-id-zq9";

    [Theory]
    [InlineData("cotizacion-final")]
    [InlineData("ajustes-pendientes")]
    public async Task Sin_Token_O_Con_Token_Vencido_Responde_401_CA_063_09(string route)
    {
        using var factory = await SecurityMarketFactory.CreateAsync();
        using var client = factory.CreateClient();
        var path = $"/api/v1/visitas/{ObjectId.GenerateNewId()}/{route}";

        using var anonymous = await Send(client, path, null);
        using var expired = await Send(client, path, TokenForge.Expired());

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        expired.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("cotizacion-final")]
    [InlineData("ajustes-pendientes")]
    public async Task Id_Mal_Formado_Responde_422_Sin_Repetir_El_Valor_CA_063_08(string route)
    {
        using var factory = await SecurityMarketFactory.CreateAsync();
        using var client = factory.CreateClient();

        using var response = await Send(client, $"/api/v1/visitas/{_badId}/{route}", TokenForge.Valid());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var raw = await response.Content.ReadAsStringAsync();
        System.Text.Json.JsonElement.Parse(raw).GetProperty("errors").TryGetProperty("id", out _).Should().BeTrue();
        raw.Should().NotContain(_badId, "el valor recibido nunca se repite");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.Contains("ETag").Should().BeFalse();
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, string path, string? bearer)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return await client.SendAsync(request);
    }
}
