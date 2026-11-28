extern alias MarketApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.IntegrationTest.Catalog;
using Knock.IntegrationTest.Requests;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Common.Bandeja;
using MarketApi::Knock.Market.Api.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using Xunit;

namespace Knock.IntegrationTest.Reviews;

/// <summary>
/// Spec 061 (R-03, CA-05, CA-06): Mercado real con la puerta real de permisos (<c>IdentityPanelPermissionGate</c>) y una Identidad simulada. Con el permiso y la sesión que Identidad confirma no hay 403 de
/// puerta; si Identidad dice que no (por ejemplo, sin segundo factor no existe sesión) o no responde, la ruta responde 403 y no cambia nada. Solo datos ficticios; corre en CI.
/// </summary>
public sealed class Spec061PuertaRealHttpTests
{
    private static readonly byte[] _panelKey = RandomNumberGenerator.GetBytes(32);

    private sealed class IdentityStub(Func<HttpResponseMessage> respond) : HttpMessageHandler, IHttpClientFactory
    {
        public int Calls { get; private set; }

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(respond());
        }
    }

    private static Task<Spec020Host> StartAsync(IdentityStub identity) =>
        Spec020Host.StartAsync(
            new Dictionary<string, string?>
            {
                [PanelAuthentication.KeyConfigurationKey] = Convert.ToBase64String(_panelKey),
                [EntregaOpinionModerada.IdentityBaseUrlConfigurationKey] = "http://identity.test",
                [EntregaOpinionModerada.IdentityKeyConfigurationKey] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            },
            services => services.AddSingleton<IPanelPermissionGate>(provider => new IdentityPanelPermissionGate(identity, provider.GetRequiredService<IConfiguration>(), TimeProvider.System)));

    private static async Task<HttpStatusCode> HideAsync(Spec020Host host, string reviewId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/v1/admin/opiniones/{reviewId}/ocultar", UriKind.Relative))
        {
            Content = JsonContent.Create(new { motivo = "Lenguaje ofensivo hacia el cliente" }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PanelTestHost.Token(_panelKey));
        using var response = await host.Client.SendAsync(request);
        return response.StatusCode;
    }

    private static HttpResponseMessage Answer(bool allowed) => new(HttpStatusCode.OK) { Content = JsonContent.Create(new { permitido = allowed }) };

    [Fact]
    public async Task Si_Identidad_Confirma_El_Permiso_La_Puerta_Ya_No_Da_403_CA_061_05()
    {
        using var identity = new IdentityStub(() => Answer(true));
        await using var host = await StartAsync(identity);

        var status = await HideAsync(host, ObjectId.GenerateNewId().ToString());

        status.Should().Be(HttpStatusCode.NotFound, "la puerta dejó pasar y la opinión no existe (review_not_found)");
        identity.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Si_Identidad_Niega_O_No_Responde_Es_403_Y_No_Cambia_Nada_CA_061_05_CA_061_06(bool unreachable)
    {
        using var identity = new IdentityStub(() => unreachable ? throw new HttpRequestException("sin red") : Answer(false));
        await using var host = await StartAsync(identity);

        var status = await HideAsync(host, ObjectId.GenerateNewId().ToString());

        status.Should().Be(HttpStatusCode.Forbidden);
        identity.Calls.Should().Be(1);
        (await host.CountAsync("outbox", MongoDB.Driver.Builders<BsonDocument>.Filter.Eq("tipo", "OpinionModerada"))).Should().Be(0);
    }
}
