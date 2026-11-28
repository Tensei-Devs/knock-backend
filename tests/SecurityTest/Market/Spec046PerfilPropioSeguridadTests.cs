using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using Xunit;

namespace Knock.SecurityTest.Market;

/// <summary>Spec 046, TEC-15: el perfil propio trae la dirección del taller, así que exige una sesión válida. Mercado real en Production.</summary>
public sealed class Spec046PerfilPropioSeguridadTests
{
    [Fact]
    public async Task Sin_Token_O_Con_Token_Vencido_Responde_401_CA_046_71()
    {
        using var factory = await SecurityMarketFactory.CreateAsync();
        using var client = factory.CreateClient();

        using var anonymous = await Send(client, null);
        using var expired = await Send(client, TokenForge.Expired());

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        expired.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.Content.ReadAsStringAsync()).Should().NotContain("direccionTaller");
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, string? bearer)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/tecnicos/me", UriKind.Relative));
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return await client.SendAsync(request);
    }
}
