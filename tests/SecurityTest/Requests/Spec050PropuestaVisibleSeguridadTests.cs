using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using MongoDB.Bson;
using Xunit;

namespace Knock.SecurityTest.Requests;

/// <summary>Spec 050, R-35: la propuesta de reprogramación sale en la vista de la visita, que exige una sesión válida. Mercado real en Production.</summary>
public sealed class Spec050PropuestaVisibleSeguridadTests
{
    [Fact]
    public async Task La_Vista_Sin_Token_O_Con_Token_Vencido_Responde_401_CA_050_59()
    {
        using var factory = await SecurityMarketFactory.CreateAsync();
        using var client = factory.CreateClient();
        var path = new Uri($"/api/v1/visitas/{ObjectId.GenerateNewId()}", UriKind.Relative);

        using var anonymous = await client.GetAsync(path);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenForge.Expired());
        using var expired = await client.SendAsync(request);

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        expired.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.Content.ReadAsStringAsync()).Should().NotContain("propuestaReprogramacion");
    }
}
