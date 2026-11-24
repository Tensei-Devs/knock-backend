extern alias messaging;

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using Knock.Contracts.Visits;
using Xunit;
using MarketVisitaCatalog = messaging::Knock.Messaging.Api.Modules.Chat.Data.MarketVisitaCatalog;

namespace Knock.UnitTest.Messaging;

/// <summary>
/// Spec 056, R-11 y CA-12 (T-10): la llamada de Mensajería a la ruta pública <c>GET /api/v1/visitas/{id}</c> de Mercado (spec 026, R-06) reenvía el JWT de quien se conecta, sin credencial de servicio ni otro
/// encabezado de autenticación, y un rechazo de Mercado no se reintenta con otra credencial. El token es sintético y se genera al azar en cada ejecución.
/// </summary>
public sealed class Spec056ReenvioDelJwtTests
{
    [Fact]
    public async Task MarketCall_ForwardsTheUserToken_AndNoServiceCredential_CA_056_12()
    {
        var userToken = "u." + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)) + ".s";
        using var handler = new RecordingHandler(HttpStatusCode.OK, "{\"rol\":\"cliente\",\"estado\":\"en_curso\"}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://market.invalid/") };

        var result = await new MarketVisitaCatalog(http).CheckAsync("visita-1", userToken, CancellationToken.None);

        result.Status.Should().Be(VisitMembershipStatus.Member);
        var request = handler.Requests.Should().ContainSingle().Which;
        request.Method.Should().Be(HttpMethod.Get);
        request.Path.Should().Be("/api/v1/visitas/visita-1", "es la ruta pública de Mercado, no una ruta /interno");
        request.Authorization.Should().BeEquivalentTo(new AuthenticationHeaderValue("Bearer", userToken), "se reenvía el JWT del usuario tal cual");
        request.HeaderNames.Should().NotContain(name => name.Contains("Service", StringComparison.OrdinalIgnoreCase) || name.Contains("Internal", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task MarketRejection_IsNotRetriedWithAnotherCredential_CA_056_12(HttpStatusCode status)
    {
        using var handler = new RecordingHandler(status, string.Empty);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://market.invalid/") };

        var result = await new MarketVisitaCatalog(http).CheckAsync("visita-1", "u." + Convert.ToHexString(RandomNumberGenerator.GetBytes(8)) + ".s", CancellationToken.None);

        result.Status.Should().Be(VisitMembershipStatus.NotMember);
        handler.Requests.Should().ContainSingle("un 401 o 403 de Mercado cierra el paso: no hay segunda llamada con una credencial de servicio");
    }

    private sealed record Seen(HttpMethod Method, string Path, AuthenticationHeaderValue? Authorization, IReadOnlyList<string> HeaderNames);

    private sealed class RecordingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<Seen> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new Seen(request.Method, request.RequestUri!.AbsolutePath, request.Headers.Authorization, [.. request.Headers.Select(header => header.Key)]));
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
