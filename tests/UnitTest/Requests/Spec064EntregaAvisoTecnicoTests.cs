using System.Net;
using AwesomeAssertions;
using Knock.Market.Api.Common.Bandeja;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Requests;

/// <summary>Spec 064 (R-07, R-08, CA-08): entrega de <c>TecnicoNoSePresento</c> a Mensajería con solo identificadores. Solo datos ficticios.</summary>
public sealed class Spec064EntregaAvisoTecnicoTests
{
    private static readonly string _key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    [Fact]
    public async Task Delivers_To_Messaging_With_The_Notice_Permission_And_Only_Identifiers_CA_064_08()
    {
        using var handler = new Capture(HttpStatusCode.Accepted);
        var message = Message();

        (await Delivery(handler).DeliverAsync(message, CancellationToken.None)).Should().Be(ResultadoDeEntrega.Aceptado);

        var sent = handler.Sent.Should().ContainSingle().Subject;
        sent.Path.Should().Be("/api/v1/interno/avisos");
        var claims = new JsonWebTokenHandler().ReadJsonWebToken(sent.Token).Claims.Select(c => (c.Type, c.Value)).ToList();
        claims.Should().Contain(("aud", "knock-notifications")).And.Contain(("permisos", "tecnico_no_se_presento")).And.Contain(("iss", "knock-market"));
        sent.Body.Should().Contain("\"tipo\":\"tecnico_no_se_presento\"").And.Contain("\"rol\":\"tecnico\"").And.Contain("\"destinatarioId\":\"tech-user\"").And.Contain($"\"eventoId\":\"{message.Id}\"");
        sent.Body.Should().NotContain("clienteId").And.NotContain("solicitudId").And.NotContain("llegaste");
    }

    [Fact]
    public async Task Without_Address_Or_Key_Stays_For_Retry_And_A_Rejection_Is_Final_CA_064_08()
    {
        using var ok = new Capture(HttpStatusCode.Accepted);
        using var unavailable = new Capture(HttpStatusCode.ServiceUnavailable);
        using var forbidden = new Capture(HttpStatusCode.Forbidden);

        (await Delivery(ok, withMessaging: false).DeliverAsync(Message(), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Reintentable);
        (await Delivery(unavailable).DeliverAsync(Message(), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Reintentable);
        (await Delivery(forbidden).DeliverAsync(Message(), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Rechazado);
        (await Delivery(ok).DeliverAsync(new MensajeReservado("TecnicoNoSePresento:x", "TecnicoNoSePresento", new BsonDocument(), "lease", 0), CancellationToken.None)).Should().Be(ResultadoDeEntrega.Rechazado);

        ok.Sent.Should().BeEmpty();
    }

    private static MensajeReservado Message() =>
        new($"TecnicoNoSePresento:{ObjectId.GenerateNewId()}", "TecnicoNoSePresento", new BsonDocument
        {
            { "visitaId", ObjectId.GenerateNewId().ToString() },
            { "solicitudId", ObjectId.GenerateNewId().ToString() },
            { "clienteId", "client-user" },
            { "tecnicoUsuarioId", "tech-user" },
        }, "lease", 0);

    private static EntregaTecnicoNoSePresento Delivery(Capture handler, bool withMessaging = true)
    {
        var settings = new Dictionary<string, string?>();
        if (withMessaging)
        {
            settings["Services:Messaging:BaseUrl"] = "http://messaging.test";
            settings["Services:Messaging:InternalKey"] = _key;
        }

        return new EntregaTecnicoNoSePresento(new Factory(handler), new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), Options.Create(new BandejaOptions()), TimeProvider.System);
    }

    private sealed record Sent(string Path, string Body, string Token);

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class Capture(HttpStatusCode answer) : HttpMessageHandler
    {
        public List<Sent> Sent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sent.Add(new Sent(request.RequestUri!.AbsolutePath, await request.Content!.ReadAsStringAsync(cancellationToken), request.Headers.Authorization!.Parameter!));
            return new HttpResponseMessage(answer);
        }
    }
}
