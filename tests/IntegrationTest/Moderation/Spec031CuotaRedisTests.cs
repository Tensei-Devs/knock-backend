extern alias MarketApi;

using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Modules.Moderation.Data;
using StackExchange.Redis;
using Xunit;

namespace Knock.IntegrationTest.Moderation;

/// <summary>Spec 031, CA-08 contra Redis real: el script Lua cuenta de forma atómica, así que con una sola llamada disponible solo una de dos moderaciones simultáneas llama a Azure.</summary>
public sealed class Spec031CuotaRedisTests
{
    [Fact]
    public async Task Conteo_Atomico_Con_Redis_Real_CA_031_08()
    {
        var fixture = await RedisTestFixture.SharedAsync();
        await using var connection = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        var store = new ConsumoModeracionStore(connection);
        var service = "it" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        var day = new DateOnly(2026, 11, 21);

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(i => store.ReserveAsync(service, "persona-" + i, day, monthlyLimit: 1, personLimit: 10, CancellationToken.None)));

        results.Count(r => r.Result == QuotaResult.Reserved).Should().Be(1, "solo queda una llamada en el tope mensual");
        results.Count(r => r.Result == QuotaResult.MonthlyExhausted).Should().Be(1);
    }

    [Fact]
    public async Task Tope_Por_Persona_Es_Atomico_Con_Redis_Real_CA_031_08()
    {
        var fixture = await RedisTestFixture.SharedAsync();
        await using var connection = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        var store = new ConsumoModeracionStore(connection);
        var service = "it" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        var person = "persona-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        var day = new DateOnly(2026, 11, 21);

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => store.ReserveAsync(service, person, day, monthlyLimit: 1000, personLimit: 5, CancellationToken.None)));

        results.Count(r => r.Result == QuotaResult.Reserved).Should().Be(5);
        results.Count(r => r.Result == QuotaResult.PersonExhausted).Should().Be(15);
    }
}
