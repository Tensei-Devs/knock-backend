using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Conversaciones;

/// <summary>La lista de conversaciones (spec 030, R-17): de la más reciente a la más antigua, con cursor y solo las de la persona.</summary>
public sealed class Spec030ListaTests
{
    private static readonly DateTime _origin = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private static string[] Ids(JsonElement page) => [.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("conversacionId").GetString()!)];

    [Fact]
    public async Task Orden_Y_Paginacion_CA_030_11()
    {
        await using var host = await ConversacionesHost.StartAsync();
        var oldest = await host.SeedConversationAsync(_origin.AddHours(1));
        var newest = await host.SeedConversationAsync(_origin.AddHours(3));
        var middle = await host.SeedConversationAsync(_origin.AddHours(2));
        await host.SeedConversationAsync(_origin.AddHours(9), client: Guid.NewGuid().ToString("D"), technician: Guid.NewGuid().ToString("D"));

        using var response = await host.SendAsync(HttpMethod.Get, "/api/v1/conversaciones", host.Client);
        var page = await ConversacionesHost.ReadAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Ids(page).Should().Equal(newest, middle, oldest);
        page.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Veinticinco_Conversaciones_Salen_En_Dos_Paginas_Sin_Repetir_CA_030_11()
    {
        await using var host = await ConversacionesHost.StartAsync();
        var expected = new List<string>();
        for (var i = 0; i < 25; i++)
        {
            expected.Add(await host.SeedConversationAsync(_origin.AddMinutes(i)));
        }

        expected.Reverse();

        using var firstResponse = await host.SendAsync(HttpMethod.Get, "/api/v1/conversaciones", host.Client);
        var first = await ConversacionesHost.ReadAsync(firstResponse);
        var cursor = first.GetProperty("nextCursor").GetString();
        using var secondResponse = await host.SendAsync(HttpMethod.Get, $"/api/v1/conversaciones?cursor={Uri.EscapeDataString(cursor!)}", host.Client);
        var second = await ConversacionesHost.ReadAsync(secondResponse);

        Ids(first).Should().HaveCount(20).And.Equal(expected.Take(20));
        cursor.Should().NotBeNullOrEmpty();
        Ids(second).Should().Equal(expected.Skip(20));
        second.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Mismo_Momento_Se_Desempata_Por_Identificador_Sin_Repetir_CA_030_11()
    {
        await using var host = await ConversacionesHost.StartAsync();
        var ids = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add(await host.SeedConversationAsync(_origin));
        }

        var expected = ids.OrderByDescending(id => id, StringComparer.Ordinal).ToArray();

        using var firstResponse = await host.SendAsync(HttpMethod.Get, "/api/v1/conversaciones?limit=2", host.Client);
        var first = await ConversacionesHost.ReadAsync(firstResponse);
        using var secondResponse = await host.SendAsync(HttpMethod.Get, $"/api/v1/conversaciones?limit=2&cursor={Uri.EscapeDataString(first.GetProperty("nextCursor").GetString()!)}", host.Client);
        var second = await ConversacionesHost.ReadAsync(secondResponse);
        using var thirdResponse = await host.SendAsync(HttpMethod.Get, $"/api/v1/conversaciones?limit=2&cursor={Uri.EscapeDataString(second.GetProperty("nextCursor").GetString()!)}", host.Client);
        var third = await ConversacionesHost.ReadAsync(thirdResponse);

        Ids(first).Concat(Ids(second)).Concat(Ids(third)).Should().Equal(expected);
        third.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Como_Cliente_Y_Como_Tecnico_Se_Mezclan_Por_Fecha_CA_030_11()
    {
        await using var host = await ConversacionesHost.StartAsync();
        var asClientOld = await host.SeedConversationAsync(_origin.AddHours(1));
        var asTechnicianNew = await host.SeedConversationAsync(_origin.AddHours(5), client: Guid.NewGuid().ToString("D"), technician: host.Client);
        var asClientMid = await host.SeedConversationAsync(_origin.AddHours(3));

        using var response = await host.SendAsync(HttpMethod.Get, "/api/v1/conversaciones?limit=2", host.Client);
        var page = await ConversacionesHost.ReadAsync(response);

        Ids(page).Should().Equal(asTechnicianNew, asClientMid);
        page.GetProperty("items")[0].GetProperty("rol").GetString().Should().Be("tecnico");
        page.GetProperty("items")[1].GetProperty("rol").GetString().Should().Be("cliente");
        page.GetProperty("nextCursor").GetString().Should().NotBeNullOrEmpty();
        asClientOld.Should().NotBeNull();
    }

    [Fact]
    public async Task Lista_Vacia_CA_030_13()
    {
        await using var host = await ConversacionesHost.StartAsync();

        using var response = await host.SendAsync(HttpMethod.Get, "/api/v1/conversaciones", host.Client);
        var page = await ConversacionesHost.ReadAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        page.GetProperty("items").GetArrayLength().Should().Be(0);
        page.GetProperty("totalNoLeidos").GetInt32().Should().Be(0);
        page.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Cada_Elemento_Trae_El_Ultimo_Mensaje_O_Nulo_CA_030_11()
    {
        await using var host = await ConversacionesHost.StartAsync();
        var withoutMessages = await host.SeedConversationAsync(_origin);
        var withMessage = await host.SeedConversationAsync(_origin.AddHours(1));
        await host.Conversations.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(withMessage)),
            Builders<BsonDocument>.Update.Set(
                "ultimoMensaje",
                new BsonDocument { { "texto", "Voy en camino" }, { "enviadoEn", _origin.AddHours(1) }, { "autorRol", "tecnico" } }));

        using var response = await host.SendAsync(HttpMethod.Get, "/api/v1/conversaciones", host.Client);
        var items = (await ConversacionesHost.ReadAsync(response)).GetProperty("items");

        items[0].GetProperty("conversacionId").GetString().Should().Be(withMessage);
        items[0].GetProperty("ultimoMensaje").GetProperty("texto").GetString().Should().Be("Voy en camino");
        items[0].GetProperty("ultimoMensaje").GetProperty("autorRol").GetString().Should().Be("tecnico");
        items[0].GetProperty("ultimoMensaje").GetProperty("enviadoEn").GetDateTime().Should().Be(_origin.AddHours(1));
        items[1].GetProperty("conversacionId").GetString().Should().Be(withoutMessages);
        items[1].GetProperty("ultimoMensaje").ValueKind.Should().Be(JsonValueKind.Null);
        items[1].GetProperty("servicio").GetString().Should().Be(ConversacionesHost.ServiceName);
    }
}
