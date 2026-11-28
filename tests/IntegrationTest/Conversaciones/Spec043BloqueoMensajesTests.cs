extern alias MessagingApi;
using System.Net;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using BloqueoEvento = MessagingApi::Knock.Messaging.Api.Modules.Bloqueos.Dtos.EventoDeBloqueo;
using Bloqueos = MessagingApi::Knock.Messaging.Api.Modules.Bloqueos.Services.BloqueosService;

namespace Knock.IntegrationTest.Conversaciones;

/// <summary>
/// Spec 043, CA-26 a CA-28 en Mensajería (KNOCK-95): con un bloqueo en cualquier sentido nadie envía mensajes y la respuesta es la misma que la de una conversación que no existe; al crearse el bloqueo
/// la conversación abierta queda de solo lectura sin borrar mensajes, y al desbloquear se restablece. MongoDB y Redis reales, Mercado de mentira. Solo datos ficticios.
/// </summary>
public sealed class Spec043BloqueoMensajesTests
{
    private static readonly DateTime _at = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Bloqueo_En_Cualquier_Sentido_Corta_El_Envio_Con_404_CA_043_26_CA_043_27(bool clientBlocks)
    {
        await using var scenario = await ConversacionesScenario.StartAsync();
        var copy = await scenario.CopyAsync();
        var blocker = clientBlocks ? scenario.Client : scenario.Technician;
        var blocked = clientBlocks ? scenario.Technician : scenario.Client;
        await scenario.Database.GetCollection<BsonDocument>("bloqueos").InsertOneAsync(
            new BsonDocument { ["_id"] = $"{blocker}|{blocked}", ["bloqueaId"] = blocker, ["bloqueadoId"] = blocked, ["bloqueado"] = true, ["ocurridoEn"] = new BsonDateTime(_at), ["eventoId"] = "e1" });

        using var fromClient = await scenario.SendAsync(copy, scenario.Client, "Hola");
        using var fromTechnician = await scenario.SendAsync(copy, scenario.Technician, "Hola");
        using var unknown = await scenario.SendAsync(copy, scenario.Client, "Hola", conversationId: ObjectId.GenerateNewId().ToString());

        fromClient.StatusCode.Should().Be(HttpStatusCode.NotFound);
        fromTechnician.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ProblemAsync(fromTechnician)).Should().Be(await ProblemAsync(unknown), "la persona bloqueada recibe la misma respuesta que ante una conversación inexistente (CA-27)");
        (await scenario.MessagesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Crear_El_Bloqueo_Cierra_La_Conversacion_Y_Desbloquear_La_Restablece_CA_043_28()
    {
        await using var scenario = await ConversacionesScenario.StartAsync();
        var copy = await scenario.CopyAsync();
        using (var before = await scenario.SendAsync(copy, scenario.Technician, "Antes del bloqueo"))
        {
            before.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var service = copy.Services.GetRequiredService<Bloqueos>();
        await service.ApplyAsync(new BloqueoEvento("e1", "BloqueoCreado", scenario.Client, scenario.Technician, _at), CancellationToken.None);
        await service.ApplyAsync(new BloqueoEvento("e1", "BloqueoCreado", scenario.Client, scenario.Technician, _at), CancellationToken.None);

        var closed = await scenario.ConversationAsync();
        closed["estado"].AsString.Should().Be("cerrada");
        closed["motivoCierre"].AsString.Should().Be("dismissed", "el cierre no delata el bloqueo (R-17)");
        (await scenario.MessagesAsync()).Should().HaveCount(1, "los mensajes no se borran");

        await service.ApplyAsync(new BloqueoEvento("e2", "BloqueoEliminado", scenario.Client, scenario.Technician, _at.AddMinutes(1)), CancellationToken.None);
        using var after = await scenario.SendAsync(copy, scenario.Technician, "Después de desbloquear");
        after.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>Los campos estables del problema (código, título y estado): la ruta de la instancia lleva el identificador y siempre cambia.</summary>
    private static async Task<string> ProblemAsync(HttpResponseMessage response)
    {
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        return $"{(int)response.StatusCode}|{root.GetProperty("code").GetString()}|{root.GetProperty("title").GetString()}";
    }
}
