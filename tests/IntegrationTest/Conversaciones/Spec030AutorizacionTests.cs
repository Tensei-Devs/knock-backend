using System.Net;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Conversaciones;

/// <summary>Quién ve una conversación (spec 030, R-05 y R-16): solo sus dos partes; cualquier otra persona recibe el mismo 404 que una conversación inexistente.</summary>
public sealed class Spec030AutorizacionTests
{
    [Fact]
    public async Task Ajeno_Recibe_404_CA_030_03()
    {
        await using var host = await ConversacionesHost.StartAsync();
        var id = await host.SeedConversationAsync(unreadClient: 2);
        await host.SeedMessageAsync(id, "tecnico", DateTime.UtcNow.AddMinutes(-4), "hola");
        var outsider = Guid.NewGuid().ToString("D");
        var missing = ObjectId.GenerateNewId().ToString();

        using var read = await host.SendAsync(HttpMethod.Get, $"/api/v1/conversaciones/{id}", outsider);
        using var messages = await host.SendAsync(HttpMethod.Get, $"/api/v1/conversaciones/{id}/mensajes", outsider);
        using var markRead = await host.SendAsync(HttpMethod.Post, $"/api/v1/conversaciones/{id}/leido", outsider, new { hasta = DateTime.UtcNow });
        using var nonexistent = await host.SendAsync(HttpMethod.Get, $"/api/v1/conversaciones/{missing}", outsider);

        foreach (var response in new[] { read, messages, markRead, nonexistent })
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            var body = await response.Content.ReadAsStringAsync();
            ProblemBody.Reasons(body).Should().NotContain(id).And.NotContain(outsider);
            (await ConversacionesHost.ReadAsync(response)).GetProperty("code").GetString().Should().Be("not_found");
        }

        var stored = await host.Conversations.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        stored["noLeidosCliente"].ToInt32().Should().Be(2, "marcar leído como ajeno no cambia nada");
        host.Live.Events.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Los_Dos_Participantes_La_Ven_CA_030_03(bool asClient)
    {
        await using var host = await ConversacionesHost.StartAsync();
        var id = await host.SeedConversationAsync();
        await host.SeedMessageAsync(id, "tecnico", DateTime.UtcNow.AddMinutes(-4));
        var sub = asClient ? host.Client : host.Technician;

        using var read = await host.SendAsync(HttpMethod.Get, $"/api/v1/conversaciones/{id}", sub);
        using var messages = await host.SendAsync(HttpMethod.Get, $"/api/v1/conversaciones/{id}/mensajes", sub);

        read.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ConversacionesHost.ReadAsync(read)).GetProperty("rol").GetString().Should().Be(asClient ? "cliente" : "tecnico");
        messages.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
