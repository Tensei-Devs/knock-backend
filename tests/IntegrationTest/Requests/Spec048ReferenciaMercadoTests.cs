using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

public sealed class Spec048ReferenciaMercadoTests
{
    private const string _path = "/api/v1/cotizaciones/referencia-mercado";
    private const string _plumbing = "?oficio=plo";

    [Fact]
    public async Task TenAcceptedQuotes_ReturnMarketRange_WithoutIndividualPrices_CA_048_07_CA_048_19()
    {
        await using var host = await StartTechnicianAsync();
        await SeedAcceptedAsync(host, Spec020Host.Plumbing, "fijo", [100, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 1100, 1200]);
        using var response = await host.SendAsync(HttpMethod.Get, _path + _plumbing + "&tipoTrabajo=fijo", host.ClientSub);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await Spec020Host.ReadAsync(response);
        body.GetProperty("origen").GetString().Should().Be("mercado");
        body.GetProperty("p25").GetInt32().Should().Be(375);
        body.GetProperty("mediana").GetInt32().Should().Be(650);
        body.GetProperty("p75").GetInt32().Should().Be(925);
        body.EnumerateObject().Select(x => x.Name).Should().BeEquivalentTo("origen", "p25", "mediana", "p75");
    }

    [Fact]
    public async Task RangeQuotes_UseTheMidpoint_CA_048_19()
    {
        await using var host = await StartTechnicianAsync();
        await SeedAcceptedAsync(host, Spec020Host.Plumbing, "rango", Enumerable.Repeat(200, 10).ToArray(), isRange: true);
        using var response = await host.SendAsync(HttpMethod.Get, _path + _plumbing, host.ClientSub);
        var body = await Spec020Host.ReadAsync(response);
        body.GetProperty("mediana").GetInt32().Should().Be(300);
    }

    [Fact]
    public async Task FewerThanTenOfTheType_FallBackToTheTradeRange_CA_048_19()
    {
        await using var host = await StartTechnicianAsync();
        await SeedAcceptedAsync(host, Spec020Host.Plumbing, "fijo", [100, 200, 300, 400, 500, 600]);
        await SeedAcceptedAsync(host, Spec020Host.Plumbing, "visita", [150, 150, 150, 150, 150]);
        using var response = await host.SendAsync(HttpMethod.Get, _path + _plumbing + "&tipoTrabajo=fijo", host.ClientSub);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Spec020Host.ReadAsync(response)).GetProperty("origen").GetString().Should().Be("categoria");
    }

    [Fact]
    public async Task FewerThanTenInTheTrade_HaveNoData_AndIgnoreOldOrOtherTradeQuotes_CA_048_19()
    {
        await using var host = await StartTechnicianAsync();
        await SeedAcceptedAsync(host, Spec020Host.Plumbing, "fijo", [100, 200, 300, 400, 500, 600, 700, 800, 900]);
        await SeedAcceptedAsync(host, Spec020Host.Workshop, "fijo", Enumerable.Repeat(100, 10).ToArray());
        await SeedAcceptedAsync(host, Spec020Host.Plumbing, "fijo", [100, 100], DateTime.UtcNow.AddDays(-91));
        using var response = await host.SendAsync(HttpMethod.Get, _path + _plumbing, host.ClientSub);
        var body = await Spec020Host.ReadAsync(response);
        body.GetProperty("origen").GetString().Should().Be("sin_datos");
        body.GetProperty("p25").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task RepeatedQuery_IsServedFromCache_CA_048_11()
    {
        await using var host = await StartTechnicianAsync();
        await SeedAcceptedAsync(host, Spec020Host.Plumbing, "fijo", Enumerable.Repeat(100, 10).ToArray());
        using var first = await host.SendAsync(HttpMethod.Get, _path + _plumbing, host.ClientSub);
        (await Spec020Host.ReadAsync(first)).GetProperty("origen").GetString().Should().Be("mercado");
        await host.Database.GetCollection<BsonDocument>("cotizaciones").DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        using var second = await host.SendAsync(HttpMethod.Get, _path + _plumbing, host.ClientSub);
        (await Spec020Host.ReadAsync(second)).GetProperty("origen").GetString().Should().Be("mercado");
    }

    [Fact]
    public async Task NonTechnician_IsForbidden_CA_048_09()
    {
        await using var host = await Spec020Host.StartAsync();
        using var response = await host.SendAsync(HttpMethod.Get, _path + _plumbing, host.ClientSub);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MissingToken_IsUnauthorized_CA_048_08()
    {
        await using var host = await Spec020Host.StartAsync();
        using var response = await host.SendAsync(HttpMethod.Get, _path + _plumbing);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("", "oficio")]
    [InlineData("?oficio=oficio-que-no-existe-zzz", "oficio")]
    [InlineData("?oficio=plo&tipoTrabajo=tipo-invalido-zzz", "tipoTrabajo")]
    public async Task InvalidQuery_Returns422_WithoutEchoingTheValue_CA_048_10(string query, string field)
    {
        await using var host = await StartTechnicianAsync();
        using var response = await host.SendAsync(HttpMethod.Get, _path + query, host.ClientSub);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var raw = await response.Content.ReadAsStringAsync();
        var body = JsonElement.Parse(raw);
        body.GetProperty("errors").EnumerateObject().Select(x => x.Name.ToUpperInvariant()).Should().Contain(field.ToUpperInvariant());
        raw.Should().NotContain("zzz");
    }

    [Fact]
    public async Task ThirtyFirstCallInAMinute_IsTooManyRequests_CA_048_24()
    {
        await using var host = await StartTechnicianAsync();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 31; i++)
        {
            last?.Dispose();
            last = await host.SendAsync(HttpMethod.Get, _path + _plumbing, host.ClientSub);
        }

        using (last)
        {
            last!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            last.Headers.RetryAfter.Should().NotBeNull();
        }
    }

    private static async Task<Spec020Host> StartTechnicianAsync()
    {
        var host = await Spec020Host.StartAsync();
        await host.SeedTechnicianAsync(host.ClientSub, "activo", false, [Spec020Host.Plumbing], host.ColoniaId);
        return host;
    }

    private static async Task SeedAcceptedAsync(Spec020Host host, string trade, string type, int[] prices, DateTime? acceptedAt = null, bool isRange = false)
    {
        var requests = prices.Select(_ => new BsonDocument { ["_id"] = ObjectId.GenerateNewId(), ["oficioClave"] = trade, ["clienteId"] = Guid.NewGuid().ToString("D") }).ToList();
        await host.Database.GetCollection<BsonDocument>("solicitudes").InsertManyAsync(requests);
        var quotes = prices.Select((price, i) =>
        {
            var quote = new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                ["solicitudId"] = requests[i]["_id"],
                ["tecnicoId"] = ObjectId.GenerateNewId(),
                ["estado"] = "aceptada",
                ["tipoPrecio"] = type,
                ["aceptadaEn"] = acceptedAt ?? DateTime.UtcNow.AddDays(-5),
                ["creadoEn"] = DateTime.UtcNow,
            };
            if (isRange)
            {
                quote["precioMin"] = price;
                quote["precioMax"] = price + 200;
            }
            else
            {
                quote["precio"] = price;
            }

            return quote;
        });
        await host.Database.GetCollection<BsonDocument>("cotizaciones").InsertManyAsync(quotes);
    }
}
