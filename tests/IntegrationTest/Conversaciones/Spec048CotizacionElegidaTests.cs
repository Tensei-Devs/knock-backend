using System.Net;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Conversaciones;

/// <summary>Spec 048, CA-21: el evento <c>CotizacionElegida</c> de Mercado cierra las demás conversaciones de la solicitud sin borrar mensajes y es idempotente.</summary>
public sealed class Spec048CotizacionElegidaTests
{
    private const string _path = "/api/v1/interno/eventos";
    private static readonly Dictionary<string, string?> _settings = new() { ["Security:InternalTokens:MarketKey"] = InternalServiceTokens.KeyBase64 };

    [Fact]
    public async Task ChosenQuote_ClosesTheOtherConversations_KeepsMessages_AndIsIdempotent_CA_048_21()
    {
        await using var host = await ConversacionesHost.StartAsync(_settings);
        var request = ObjectId.GenerateNewId().ToString();
        var chosenTechnician = Guid.NewGuid().ToString("D");
        var otherTechnician = Guid.NewGuid().ToString("D");
        var chosen = await host.SeedConversationAsync(technician: chosenTechnician, request: request, technicianId: ObjectId.GenerateNewId().ToString());
        var other = await host.SeedConversationAsync(technician: otherTechnician, request: request, technicianId: ObjectId.GenerateNewId().ToString());
        var elsewhere = await host.SeedConversationAsync(technician: otherTechnician);
        await host.SeedMessageAsync(other, "cliente", DateTime.UtcNow.AddMinutes(-3), "hola");

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var response = await host.SendServiceAsync(_path, Token(["CotizacionElegida"]), Body(request, chosenTechnician));
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            (await StateOf(host, chosen)).Should().Be(("abierta", BsonNull.Value));
            (await StateOf(host, other)).Should().Be(("cerrada", "other_quote_chosen"));
            (await StateOf(host, elsewhere)).Should().Be(("abierta", BsonNull.Value), "otra solicitud no se toca");
            (await host.Messages.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1, "los mensajes no se borran");
        }
    }

    [Fact]
    public async Task Event_WithoutPermissionOrToken_OrWithUnknownFields_IsRejected_AndChangesNothing()
    {
        await using var host = await ConversacionesHost.StartAsync(_settings);
        var request = ObjectId.GenerateNewId().ToString();
        var other = await host.SeedConversationAsync(request: request);

        using var withoutToken = await host.SendServiceAsync(_path, null, Body(request, Guid.NewGuid().ToString("D")));
        using var wrongPermission = await host.SendServiceAsync(_path, Token(["solicitud_nueva"]), Body(request, Guid.NewGuid().ToString("D")));
        using var extraField = await host.SendServiceAsync(_path, Token(["CotizacionElegida"]), new { eventoId = "e1", tipo = "CotizacionElegida", solicitudId = request, tecnicoUsuarioId = "t", texto = "libre" });
        using var unknownType = await host.SendServiceAsync(_path, Token(["BloqueoCreado"]), new { eventoId = "e1", tipo = "BloqueoCreado", solicitudId = request, tecnicoUsuarioId = "t" });

        withoutToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongPermission.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        extraField.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        unknownType.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await StateOf(host, other)).Should().Be(("abierta", BsonNull.Value));
    }

    private static object Body(string request, string technician) =>
        new { eventoId = "CotizacionElegida:" + request, tipo = "CotizacionElegida", solicitudId = request, tecnicoUsuarioId = technician };

    private static async Task<(string Estado, BsonValue Motivo)> StateOf(ConversacionesHost host, string id)
    {
        var document = await host.Conversations.Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id))).SingleAsync();
        return (document["estado"].AsString, document["motivoCierre"]);
    }

    private static string Token(string[] permissions)
    {
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "knock-market",
            Audience = "knock-notifications",
            Claims = new Dictionary<string, object> { ["permisos"] = permissions, ["jti"] = Guid.NewGuid().ToString("N") },
            IssuedAt = now,
            NotBefore = now.AddSeconds(-1),
            Expires = now.AddMinutes(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(InternalServiceTokens.Key), SecurityAlgorithms.HmacSha256),
        });
    }
}
