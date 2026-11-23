using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using MongoDB.Bson;
using Xunit;

namespace Knock.SecurityTest.Market;

/// <summary>Spec 061 (CA-05): las rutas de moderación de opiniones del panel no responden sin token del panel ni con el token de la app, y nada cambia. Mercado real con la puerta de permisos que falla cerrado.</summary>
public sealed class Spec061OcultarOpinionSeguridadTests
{
    [Theory]
    [InlineData("ocultar")]
    [InlineData("mostrar")]
    public async Task Sin_Token_O_Con_Token_De_La_App_Responde_401_CA_061_05(string action)
    {
        using var factory = await SecurityMarketFactory.CreateAsync();
        using var client = factory.CreateClient();
        var id = ObjectId.GenerateNewId();
        var path = $"/api/v1/admin/opiniones/{id}/{action}";

        using var anonymous = await Send(client, path, null);
        using var appToken = await Send(client, path, TokenForge.Valid());

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        appToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, string path, string? bearer)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative)) { Content = JsonContent.Create(new { motivo = "motivo de prueba valido" }) };
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return await client.SendAsync(request);
    }
}
