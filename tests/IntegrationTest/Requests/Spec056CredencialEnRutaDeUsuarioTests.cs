using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>
/// Spec 056, T-10 (R-11, CA-04 y CA-12): la ruta pública <c>GET /api/v1/visitas/{id}</c>, a la que Mensajería reenvía el JWT del usuario, rechaza una credencial de servicio aunque sea
/// válida en una ruta interna. MongoDB real; solo datos ficticios.
/// </summary>
public sealed class Spec056CredencialEnRutaDeUsuarioTests
{
    [Fact]
    public async Task UserRoute_RejectsAValidServiceCredential_AndAcceptsTheUserToken_CA_056_04_CA_056_12()
    {
        await using var host = await Spec020Host.StartAsync(new Dictionary<string, string?> { ["Security:InternalTokens:IdentityKey"] = InternalServiceTokens.KeyBase64 });
        var visit = await host.ConfirmedVisitAsync();
        var element = ObjectId.GenerateNewId().ToString();
        var userClaims = new Dictionary<string, string> { ["sub"] = host.ClientSub, ["sid"] = Guid.NewGuid().ToString("N") };
        var credential = InternalServiceTokens.Issue(host.ClientSub, element, extra: userClaims);

        using var internalRoute = await SendAsync(host, $"/api/v1/interno/historial?usuarioId={host.ClientSub}&elementoId={element}", credential);
        using var userRoute = await SendAsync(host, $"/api/v1/visitas/{visit.VisitId}", credential);
        using var withUserToken = await host.VisitAsync(visit.VisitId);

        internalRoute.StatusCode.Should().Be(HttpStatusCode.OK, "la credencial es válida: el rechazo de abajo no se debe a que esté mal formada");
        userRoute.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        withUserToken.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<HttpResponseMessage> SendAsync(Spec020Host host, string path, string bearer)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return await host.Client.SendAsync(request);
    }
}
