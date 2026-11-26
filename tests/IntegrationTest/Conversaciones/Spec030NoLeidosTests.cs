using System.Net;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Conversaciones;

/// <summary>Contadores de no leídos y «marcar como leído» (spec 030, R-18 y R-19).</summary>
public sealed class Spec030NoLeidosTests
{
    private static readonly DateTime _origin = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private static async Task<BsonDocument> StoredAsync(ConversacionesHost host, string id) =>
        await host.Conversations.Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id))).SingleAsync();

    /// <summary>Una conversación con <paramref name="count"/> mensajes del técnico, uno por minuto desde el origen, todos sin leer por el cliente.</summary>
    private static async Task<string> SeedUnreadAsync(ConversacionesHost host, int count)
    {
        var id = await host.SeedConversationAsync(_origin.AddMinutes(count - 1), unreadClient: count);
        for (var i = 0; i < count; i++)
        {
            await host.SeedMessageAsync(id, "tecnico", _origin.AddMinutes(i));
        }

        return id;
    }

    [Fact]
    public async Task Suma_De_Contadores_CA_030_10()
    {
        await using var host = await ConversacionesHost.StartAsync();
        await host.SeedConversationAsync(_origin, unreadClient: 3);
        await host.SeedConversationAsync(_origin.AddHours(1), unreadClient: 2);
        // Como técnico de otra conversación: cuenta el contador de su lado, no el del cliente.
        await host.SeedConversationAsync(_origin.AddHours(2), unreadClient: 40, unreadTechnician: 4, client: Guid.NewGuid().ToString("D"), technician: host.Client);
        // Ajena: no suma.
        await host.SeedConversationAsync(_origin.AddHours(3), unreadClient: 50, client: Guid.NewGuid().ToString("D"), technician: Guid.NewGuid().ToString("D"));

        using var unread = await host.SendAsync(HttpMethod.Get, "/api/v1/conversaciones/no-leidas", host.Client);
        using var list = await host.SendAsync(HttpMethod.Get, "/api/v1/conversaciones", host.Client);

        unread.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ConversacionesHost.ReadAsync(unread)).GetProperty("total").GetInt32().Should().Be(9);
        (await ConversacionesHost.ReadAsync(list)).GetProperty("totalNoLeidos").GetInt32().Should().Be(9);
    }

    [Fact]
    public async Task Sin_Conversaciones_El_Total_Es_Cero_CA_030_10()
    {
        await using var host = await ConversacionesHost.StartAsync();

        using var unread = await host.SendAsync(HttpMethod.Get, "/api/v1/conversaciones/no-leidas", Guid.NewGuid().ToString("D"));

        unread.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ConversacionesHost.ReadAsync(unread)).GetProperty("total").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Marcar_Leido_Idempotente_CA_030_12()
    {
        await using var host = await ConversacionesHost.StartAsync();
        var id = await SeedUnreadAsync(host, 4);
        await host.SeedConversationAsync(_origin, unreadClient: 3);
        var last = _origin.AddMinutes(3);

        using var first = await host.SendAsync(HttpMethod.Post, $"/api/v1/conversaciones/{id}/leido", host.Client, new { hasta = last });
        var afterFirst = await StoredAsync(host, id);
        using var repeated = await host.SendAsync(HttpMethod.Post, $"/api/v1/conversaciones/{id}/leido", host.Client, new { hasta = last });
        using var earlier = await host.SendAsync(HttpMethod.Post, $"/api/v1/conversaciones/{id}/leido", host.Client, new { hasta = _origin });
        var afterAll = await StoredAsync(host, id);
        using var total = await host.SendAsync(HttpMethod.Get, "/api/v1/conversaciones/no-leidas", host.Client);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        repeated.StatusCode.Should().Be(HttpStatusCode.NoContent);
        earlier.StatusCode.Should().Be(HttpStatusCode.NoContent);
        afterFirst["noLeidosCliente"].ToInt32().Should().Be(0);
        afterFirst["leidoHastaCliente"].ToUniversalTime().Should().Be(last);
        afterAll["noLeidosCliente"].ToInt32().Should().Be(0, "una fecha anterior no sube el contador");
        afterAll["leidoHastaCliente"].ToUniversalTime().Should().Be(last, "la marca nunca retrocede");
        (await ConversacionesHost.ReadAsync(total)).GetProperty("total").GetInt32().Should().Be(3, "el total baja en 4 y queda el de la otra conversación");
        host.Live.Events.Should().Equal([(host.Client, id, 0, 3)], "solo el primer marcado cambió algo y avisa a los otros dispositivos");
    }

    [Fact]
    public async Task Marcar_Leido_A_Medias_Recalcula_Con_Los_Mensajes_Posteriores_CA_030_12()
    {
        await using var host = await ConversacionesHost.StartAsync();
        var id = await SeedUnreadAsync(host, 4);
        // Un mensaje propio posterior no cuenta como no leído.
        await host.SeedMessageAsync(id, "cliente", _origin.AddMinutes(10));

        using var response = await host.SendAsync(HttpMethod.Post, $"/api/v1/conversaciones/{id}/leido", host.Client, new { hasta = _origin.AddMinutes(1) });
        var stored = await StoredAsync(host, id);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        stored["noLeidosCliente"].ToInt32().Should().Be(2, "quedan dos mensajes del técnico después de la marca");
        host.Live.Events.Should().ContainSingle().Which.Unread.Should().Be(2);
    }

    [Fact]
    public async Task Hasta_En_El_Futuro_Se_Acota_Al_Ultimo_Mensaje_CA_030_12()
    {
        await using var host = await ConversacionesHost.StartAsync();
        var id = await SeedUnreadAsync(host, 2);

        using var response = await host.SendAsync(HttpMethod.Post, $"/api/v1/conversaciones/{id}/leido", host.Client, new { hasta = DateTime.UtcNow.AddYears(5) });
        var stored = await StoredAsync(host, id);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        stored["leidoHastaCliente"].ToUniversalTime().Should().Be(_origin.AddMinutes(1), "la app no puede marcar leído algo posterior al último mensaje");
        stored["noLeidosCliente"].ToInt32().Should().Be(0);
    }

    [Fact]
    public async Task Cada_Parte_Marca_Su_Propio_Contador_CA_030_12()
    {
        await using var host = await ConversacionesHost.StartAsync();
        var id = await host.SeedConversationAsync(_origin.AddMinutes(1), unreadClient: 1, unreadTechnician: 1);
        await host.SeedMessageAsync(id, "tecnico", _origin);
        await host.SeedMessageAsync(id, "cliente", _origin.AddMinutes(1));

        using var response = await host.SendAsync(HttpMethod.Post, $"/api/v1/conversaciones/{id}/leido", host.Technician, new { hasta = _origin.AddMinutes(1) });
        var stored = await StoredAsync(host, id);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        stored["noLeidosTecnico"].ToInt32().Should().Be(0);
        stored["leidoHastaTecnico"].ToUniversalTime().Should().Be(_origin.AddMinutes(1));
        stored["noLeidosCliente"].ToInt32().Should().Be(1, "el contador del cliente no se toca");
        stored.Contains("leidoHastaCliente").Should().BeFalse();
        host.Live.Events.Should().ContainSingle().Which.Subject.Should().Be(host.Technician);
    }
}
