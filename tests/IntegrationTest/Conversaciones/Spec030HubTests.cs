using AwesomeAssertions;
using Xunit;

namespace Knock.IntegrationTest.Conversaciones;

/// <summary>
/// Spec 030, T-08 (KNOCK-73): lo que se agrega al hub <c>/hubs/chat</c> de la spec 026: el grupo propio <c>usuario:{sub}</c>, <c>UnirseAConversacion</c> y <c>SalirDeConversacion</c> y los eventos
/// <c>mensajeConversacion</c> y <c>noLeidos</c>. Solo datos ficticios.
/// </summary>
public sealed class Spec030HubTests
{
    private static readonly TimeSpan _silence = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task Ajeno_No_Se_Une_Ni_Recibe_Nada_CA_030_03()
    {
        await using var scenario = await ConversacionesScenario.StartAsync();
        var copy = await scenario.CopyAsync();
        var outsider = await scenario.ConnectAsync(copy, scenario.Outsider);
        var live = ConversacionesScenario.Listen(outsider, "mensajeConversacion");
        var counters = ConversacionesScenario.Listen(outsider, "noLeidos");

        var forged = await ConversacionesScenario.InvokeAsync(outsider, "UnirseAConversacion", scenario.ConversationId);
        var missing = await ConversacionesScenario.InvokeAsync(outsider, "UnirseAConversacion", MongoDB.Bson.ObjectId.GenerateNewId().ToString());
        var malformed = await ConversacionesScenario.InvokeAsync(outsider, "UnirseAConversacion", "no-es-un-id");
        using var sent = await scenario.SendAsync(copy, scenario.Technician, "hola");

        sent.IsSuccessStatusCode.Should().BeTrue();
        foreach (var result in new[] { forged, missing, malformed })
        {
            result.GetProperty("ok").GetBoolean().Should().BeFalse();
            result.GetProperty("codigo").GetString().Should().Be("unauthorized", "no se distingue «no existe» de «es ajena»");
        }

        (await ConversacionesScenario.NothingArrivesAsync(live, _silence)).Should().BeTrue();
        (await ConversacionesScenario.NothingArrivesAsync(counters, _silence)).Should().BeTrue();
    }

    [Fact]
    public async Task Cada_Dispositivo_De_Quien_Recibe_Obtiene_NoLeidos_En_Vivo_Entre_Replicas_CA_030_01()
    {
        await using var scenario = await ConversacionesScenario.StartAsync();
        var replicaA = await scenario.CopyAsync();
        var replicaB = await scenario.CopyAsync();
        var phone = await scenario.ConnectAsync(replicaB, scenario.Client);
        var tablet = await scenario.ConnectAsync(replicaA, scenario.Client);
        var senderDevice = await scenario.ConnectAsync(replicaB, scenario.Technician);
        var phoneCounters = ConversacionesScenario.Listen(phone, "noLeidos");
        var tabletCounters = ConversacionesScenario.Listen(tablet, "noLeidos");
        var senderCounters = ConversacionesScenario.Listen(senderDevice, "noLeidos");

        using var sent = await scenario.SendAsync(replicaA, scenario.Technician, "¿A qué hora le queda bien?");

        sent.IsSuccessStatusCode.Should().BeTrue();
        foreach (var inbox in new[] { phoneCounters, tabletCounters })
        {
            var counter = await ConversacionesScenario.NextAsync(inbox);
            counter.GetProperty("noLeidos").GetInt32().Should().Be(1);
            counter.GetProperty("total").GetInt32().Should().Be(1);
        }

        (await ConversacionesScenario.NothingArrivesAsync(senderCounters, _silence)).Should().BeTrue("quien escribe no recibe el contador de la otra parte");
    }

    [Fact]
    public async Task Unirse_Y_Salir_Controlan_Quien_Recibe_El_Mensaje_CA_030_01()
    {
        await using var scenario = await ConversacionesScenario.StartAsync();
        var copy = await scenario.CopyAsync();
        var client = await scenario.ConnectAsync(copy, scenario.Client);
        var technician = await scenario.ConnectAsync(copy, scenario.Technician);
        var clientLive = ConversacionesScenario.Listen(client, "mensajeConversacion");
        var technicianLive = ConversacionesScenario.Listen(technician, "mensajeConversacion");
        (await ConversacionesScenario.InvokeAsync(client, "UnirseAConversacion", scenario.ConversationId)).GetProperty("ok").GetBoolean().Should().BeTrue();
        (await ConversacionesScenario.InvokeAsync(technician, "UnirseAConversacion", scenario.ConversationId)).GetProperty("ok").GetBoolean().Should().BeTrue();

        using var first = await scenario.SendAsync(copy, scenario.Client, "primero");
        (await ConversacionesScenario.NextAsync(clientLive)).GetProperty("texto").GetString().Should().Be("primero");
        (await ConversacionesScenario.NextAsync(technicianLive)).GetProperty("texto").GetString().Should().Be("primero");

        (await ConversacionesScenario.InvokeAsync(technician, "SalirDeConversacion", scenario.ConversationId)).GetProperty("ok").GetBoolean().Should().BeTrue();
        using var second = await scenario.SendAsync(copy, scenario.Client, "segundo");

        first.IsSuccessStatusCode.Should().BeTrue();
        second.IsSuccessStatusCode.Should().BeTrue();
        (await ConversacionesScenario.NextAsync(clientLive)).GetProperty("texto").GetString().Should().Be("segundo");
        (await ConversacionesScenario.NothingArrivesAsync(technicianLive, _silence)).Should().BeTrue("quien salió ya no recibe la sala");
    }
}
