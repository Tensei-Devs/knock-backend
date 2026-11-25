using System.Net;
using AwesomeAssertions;
using Knock.Market.Api.Common.Bandeja;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Catalog;

/// <summary>Spec 045 (R-20): entrega de <c>CambioDeOficio</c> de la bandeja de salida a la auditoría del panel de Identidad. Solo datos ficticios.</summary>
public sealed class Spec045CambioDeOficioEntregaTests
{
    private static readonly string _key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    private static MensajeReservado Message(bool complete = true)
    {
        var data = new BsonDocument { { "oficioClave", "gas" }, { "accion", "creado" } };
        if (complete)
        {
            data.Add("porId", "panel-de-prueba");
        }

        return new MensajeReservado("CambioDeOficio:abc", "CambioDeOficio", data, "lease", 0);
    }

    private static EntregaCambioDeOficio Delivery(Capture handler, bool configured = true)
    {
        var settings = configured
            ? new Dictionary<string, string?> { ["Services:Identity:BaseUrl"] = "http://identity.test", ["Security:InternalTokens:MarketKey"] = _key }
            : new Dictionary<string, string?>();
        return new EntregaCambioDeOficio(new Factory(handler), new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), Options.Create(new BandejaOptions()), TimeProvider.System);
    }

    [Fact]
    public async Task Entrega_A_Identidad_Solo_Con_Identificadores_Y_Permiso_Propio_CA_045_35()
    {
        using var handler = new Capture(HttpStatusCode.Accepted);

        var result = await Delivery(handler).DeliverAsync(Message(), CancellationToken.None);

        result.Should().Be(ResultadoDeEntrega.Aceptado);
        var sent = handler.Sent.Should().ContainSingle().Subject;
        sent.Path.Should().Be("/api/v1/interno/cambios-de-oficio");
        BsonDocument.Parse(sent.Body).Names.Should().BeEquivalentTo("eventoId", "tipo", "oficioClave", "accion", "porId");
        var token = new JsonWebTokenHandler().ReadJsonWebToken(sent.Token);
        token.Audiences.Should().ContainSingle().Which.Should().Be("knock-identity");
        token.Issuer.Should().Be("knock-market");
        token.Claims.Where(c => c.Type == "permisos").Select(c => c.Value).Should().BeEquivalentTo("cambio-de-oficio");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, true)]
    [InlineData(HttpStatusCode.UnprocessableEntity, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    [InlineData(HttpStatusCode.TooManyRequests, false)]
    public async Task Respuestas_Se_Traducen_A_Rechazo_O_Reintento_CA_045_35(HttpStatusCode answer, bool rejected)
    {
        using var handler = new Capture(answer);

        (await Delivery(handler).DeliverAsync(Message(), CancellationToken.None)).Should().Be(rejected ? ResultadoDeEntrega.Rechazado : ResultadoDeEntrega.Reintentable);
    }

    [Fact]
    public async Task Sin_Direccion_O_Clave_El_Mensaje_Queda_Para_Reintento_CA_045_35()
    {
        using var handler = new Capture(HttpStatusCode.Accepted);

        (await Delivery(handler, configured: false).DeliverAsync(Message(), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Reintentable);
        handler.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Mensaje_Incompleto_Se_Rechaza_Sin_Enviar_Nada_CA_045_35()
    {
        using var handler = new Capture(HttpStatusCode.Accepted);

        (await Delivery(handler).DeliverAsync(Message(complete: false), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Rechazado);
        handler.Sent.Should().BeEmpty();
    }

    private sealed record Sent(string Path, string Body, string Token);

    private sealed class Capture(HttpStatusCode answer) : HttpMessageHandler
    {
        public List<Sent> Sent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sent.Add(new Sent(request.RequestUri!.AbsolutePath, await request.Content!.ReadAsStringAsync(cancellationToken), request.Headers.Authorization!.Parameter!));
            return new HttpResponseMessage(answer);
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
