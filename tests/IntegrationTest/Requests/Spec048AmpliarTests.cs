using System.Net;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

public sealed class Spec048AmpliarTests
{
    private static string Path(ObjectId id) => $"/api/v1/solicitudes/{id}/ampliar";

    [Fact]
    public async Task Owner_ExtendsOpenRequest_SchedulesNextWaveNow_CA_048_01_CA_048_17()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await SeedRequestAsync(host, host.ClientSub, "enviada", 1);
        var before = DateTime.UtcNow.AddSeconds(-1);
        using var response = await host.SendAsync(HttpMethod.Post, Path(id), host.ClientSub);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Spec020Host.ReadAsync(response)).GetProperty("estado").GetString().Should().Be("ampliada");
        var task = await Tasks(host).Find(new BsonDocument("tipo", "RepartirOleada")).SingleAsync();
        task["claveNegocio"].AsString.Should().Be($"{id}:2");
        task["programadaPara"].ToUniversalTime().Should().BeOnOrBefore(DateTime.UtcNow).And.BeOnOrAfter(before);
    }

    [Fact]
    public async Task SecondCallForSameWave_IsConflict_CA_048_06()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await SeedRequestAsync(host, host.ClientSub, "cotizando", 1);
        (await StatusAsync(host, id, host.ClientSub)).Should().Be(HttpStatusCode.OK);
        using var again = await host.SendAsync(HttpMethod.Post, Path(id), host.ClientSub);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Spec020Host.ReadAsync(again)).GetProperty("code").GetString().Should().Be("request_not_extendable");
        (await Tasks(host).CountDocumentsAsync(new BsonDocument("tipo", "RepartirOleada"))).Should().Be(1);
    }

    [Theory]
    [InlineData("cancelada", 1)]
    [InlineData("vencida", 1)]
    [InlineData("cotizacion_elegida", 1)]
    [InlineData("enviada", 3)]
    [InlineData("enviada", 0)]
    public async Task NotOpenOrWavesExhausted_IsConflict_CA_048_17(string state, int wave)
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await SeedRequestAsync(host, host.ClientSub, state, wave);
        (await StatusAsync(host, id, host.ClientSub)).Should().Be(HttpStatusCode.Conflict);
        (await Tasks(host).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(0);
    }

    [Fact]
    public async Task ExpiredRequest_IsConflict_CA_048_06()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await SeedRequestAsync(host, host.ClientSub, "enviada", 1, DateTime.UtcNow.AddMinutes(-1));
        (await StatusAsync(host, id, host.ClientSub)).Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task OtherPersonOrUnknownRequest_IsNotFound_AndChangesNothing_CA_048_03_CA_048_05()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await SeedRequestAsync(host, host.ClientSub, "enviada", 1);
        (await StatusAsync(host, id, Guid.NewGuid().ToString("D"))).Should().Be(HttpStatusCode.NotFound);
        (await StatusAsync(host, ObjectId.GenerateNewId(), host.ClientSub)).Should().Be(HttpStatusCode.NotFound);
        (await Tasks(host).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(0);
    }

    [Fact]
    public async Task MalformedId_Returns422_WithoutEchoingIt_CA_048_04()
    {
        await using var host = await Spec020Host.StartAsync();
        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/solicitudes/identificador-invalido-zzz/ampliar", host.ClientSub);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var raw = await response.Content.ReadAsStringAsync();
        System.Text.Json.JsonElement.Parse(raw).GetProperty("errors").GetProperty("id").GetArrayLength().Should().BeGreaterThan(0);
        raw.Should().NotContain("zzz");
        (await Tasks(host).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(0);
    }

    [Fact]
    public async Task ErrorResponses_CarryNoPersonalData_CA_048_23()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await SeedRequestAsync(host, host.ClientSub, "cancelada", 1);
        using var conflict = await host.SendAsync(HttpMethod.Post, Path(id), host.ClientSub);
        using var notFound = await host.SendAsync(HttpMethod.Post, Path(ObjectId.GenerateNewId()), host.ClientSub);
        foreach (var response in new[] { conflict, notFound })
        {
            (await response.Content.ReadAsStringAsync()).Should().NotContain(host.ClientSub).And.NotContain(id.ToString());
        }
    }

    [Fact]
    public async Task NoSeparateFilesRoute_ExistsForARequest_CA_048_18()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await SeedRequestAsync(host, host.ClientSub, "enviada", 1);
        using var response = await host.SendAsync(HttpMethod.Get, $"/api/v1/solicitudes/{id}/archivos", host.ClientSub);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OwnerComesFromToken_NotFromBody_CA_048_22()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await SeedRequestAsync(host, host.ClientSub, "enviada", 1);
        var other = Guid.NewGuid().ToString("D");
        using var response = await host.SendAsync(HttpMethod.Post, Path(id), other, new { clienteId = host.ClientSub });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MissingToken_IsUnauthorized_CA_048_02()
    {
        await using var host = await Spec020Host.StartAsync();
        using var response = await host.SendAsync(HttpMethod.Post, Path(ObjectId.GenerateNewId()));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConcurrentCalls_ScheduleTheWaveOnce_CA_048_25()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await SeedRequestAsync(host, host.ClientSub, "enviada", 1);
        var results = await Task.WhenAll(StatusAsync(host, id, host.ClientSub), StatusAsync(host, id, host.ClientSub));
        results.Count(x => x == HttpStatusCode.OK).Should().Be(1);
        results.Count(x => x == HttpStatusCode.Conflict).Should().Be(1);
        (await Tasks(host).CountDocumentsAsync(new BsonDocument("tipo", "RepartirOleada"))).Should().Be(1);
    }

    [Fact]
    public async Task OversizedBody_IsRejected_CA_048_24()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await SeedRequestAsync(host, host.ClientSub, "enviada", 1);
        using var response = await host.SendAsync(HttpMethod.Post, Path(id), host.ClientSub, rawJson: "{\"x\":\"" + new string('a', 4000) + "\"}");
        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task EleventhCallInAnHour_IsTooManyRequests_CA_048_24()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await SeedRequestAsync(host, host.ClientSub, "enviada", 1);
        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
        {
            last?.Dispose();
            last = await host.SendAsync(HttpMethod.Post, Path(id), host.ClientSub);
        }

        using (last)
        {
            last!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            last.Headers.RetryAfter.Should().NotBeNull();
        }
    }

    private static async Task<HttpStatusCode> StatusAsync(Spec020Host host, ObjectId id, string sub)
    {
        using var response = await host.SendAsync(HttpMethod.Post, Path(id), sub);
        return response.StatusCode;
    }

    private static async Task<ObjectId> SeedRequestAsync(Spec020Host host, string clientId, string state, int wave, DateTime? expiresAt = null)
    {
        var id = ObjectId.GenerateNewId();
        await host.Database.GetCollection<BsonDocument>("solicitudes").InsertOneAsync(new BsonDocument
        {
            ["_id"] = id,
            ["clienteId"] = clientId,
            ["oficioClave"] = Spec020Host.Plumbing,
            ["estado"] = state,
            ["oleadaActual"] = wave,
            ["venceEn"] = expiresAt ?? DateTime.UtcNow.AddHours(24),
            ["creadoEn"] = DateTime.UtcNow,
        });
        return id;
    }

    private static IMongoCollection<BsonDocument> Tasks(Spec020Host host) => host.Database.GetCollection<BsonDocument>("tareas_programadas");
}
