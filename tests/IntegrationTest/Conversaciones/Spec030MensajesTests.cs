using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace Knock.IntegrationTest.Conversaciones;

/// <summary>El historial de mensajes (spec 030, R-16) y el encabezado de las lecturas (R-20).</summary>
public sealed class Spec030MensajesTests
{
    private static readonly DateTime _origin = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private static string[] Texts(JsonElement page) => [.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("texto").GetString()!)];

    [Fact]
    public async Task Paginacion_De_Mensajes_CA_030_16()
    {
        await using var host = await ConversacionesHost.StartAsync();
        var id = await host.SeedConversationAsync(_origin.AddMinutes(69));
        for (var i = 0; i < 70; i++)
        {
            await host.SeedMessageAsync(id, i % 2 == 0 ? "cliente" : "tecnico", _origin.AddMinutes(i), $"m{i:D2}");
        }

        var expected = Enumerable.Range(0, 70).Reverse().Select(i => $"m{i:D2}").ToArray();
        using var firstResponse = await host.SendAsync(HttpMethod.Get, $"/api/v1/conversaciones/{id}/mensajes", host.Client);
        var first = await ConversacionesHost.ReadAsync(firstResponse);
        using var secondResponse = await host.SendAsync(HttpMethod.Get, $"/api/v1/conversaciones/{id}/mensajes?cursor={Uri.EscapeDataString(first.GetProperty("nextCursor").GetString()!)}", host.Technician);
        var second = await ConversacionesHost.ReadAsync(secondResponse);
        using var thirdResponse = await host.SendAsync(HttpMethod.Get, $"/api/v1/conversaciones/{id}/mensajes?cursor={Uri.EscapeDataString(second.GetProperty("nextCursor").GetString()!)}", host.Client);
        var third = await ConversacionesHost.ReadAsync(thirdResponse);

        Texts(first).Should().Equal(expected.Take(30), "30 por omisión, del más reciente al más antiguo");
        Texts(second).Should().Equal(expected.Skip(30).Take(30));
        Texts(third).Should().Equal(expected.Skip(60));
        third.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
        foreach (var response in new[] { firstResponse, secondResponse, thirdResponse })
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.CacheControl!.Private.Should().BeTrue();
            response.Headers.CacheControl.NoStore.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Limite_De_Cincuenta_Y_Campos_Sin_Autor_CA_030_16()
    {
        await using var host = await ConversacionesHost.StartAsync();
        var id = await host.SeedConversationAsync(_origin.AddMinutes(59));
        for (var i = 0; i < 60; i++)
        {
            await host.SeedMessageAsync(id, "tecnico", _origin.AddMinutes(i), $"m{i:D2}");
        }

        using var response = await host.SendAsync(HttpMethod.Get, $"/api/v1/conversaciones/{id}/mensajes?limit=50", host.Client);
        var page = await ConversacionesHost.ReadAsync(response);

        page.GetProperty("items").GetArrayLength().Should().Be(50);
        page.GetProperty("nextCursor").GetString().Should().NotBeNullOrEmpty();
        var item = page.GetProperty("items")[0];
        item.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("mensajeId", "autorRol", "texto", "contactoOculto", "enviadoEn");
        item.GetProperty("autorRol").GetString().Should().Be("tecnico");
        item.GetProperty("enviadoEn").GetDateTime().Should().Be(_origin.AddMinutes(59));
    }

    [Fact]
    public async Task Las_Lecturas_No_Se_Cachean_CA_030_16()
    {
        await using var host = await ConversacionesHost.StartAsync();
        var id = await host.SeedConversationAsync();

        foreach (var path in new[] { "/api/v1/conversaciones", "/api/v1/conversaciones/no-leidas", $"/api/v1/conversaciones/{id}", $"/api/v1/conversaciones/{id}/mensajes" })
        {
            using var response = await host.SendAsync(HttpMethod.Get, path, host.Client);

            response.StatusCode.Should().Be(HttpStatusCode.OK, path);
            response.Headers.CacheControl!.Private.Should().BeTrue(path);
            response.Headers.CacheControl.NoStore.Should().BeTrue(path);
            response.Headers.ETag.Should().BeNull(path);
        }
    }
}
