using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.IntegrationTest.Support;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest.Scripts;

/// <summary>Spec 031, R-17 y §6: <c>uso_moderacion</c> con TTL de 365 días en las dos bases, de forma idempotente. Corre solo en el CI (necesita contenedores).</summary>
public sealed class Spec031ModerationScriptTests
{
    [Fact]
    public async Task Script_Crea_Uso_Moderacion_En_Dos_Bases_Idempotente_CA_031_16()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        var suffix = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        var market = "s031m_" + suffix;
        var messaging = "s031g_" + suffix;
        using var client = new MongoClient(fixture.ConnectionString);
        try
        {
            await using var tools = await MongoToolsContainer.StartAsync(fixture, new Dictionary<string, string> { ["KNOCK_DB_MARKET"] = market, ["KNOCK_DB_MESSAGING"] = messaging });
            string[] first = [];
            for (var run = 0; run < 2; run++)
            {
                var result = await tools.RunAsync("mongosh", "--nodb", "--quiet", "--file", $"{MongoToolsContainer.DbDirectory}/031-moderacion-uso-moderacion.js");
                result.ExitCode.Should().Be(0, result.Output);
                var marketIndexes = (await MongoIndexes.DescribeAsync(client.GetDatabase(market), "uso_moderacion")).ToArray();
                (await MongoIndexes.DescribeAsync(client.GetDatabase(messaging), "uso_moderacion")).Should().Equal(marketIndexes);
                marketIndexes.Should().Contain("creadoEn_1_ttl creadoEn:1 - ttl:31536000");
                marketIndexes.Should().Contain("contexto_1_decision_1_creadoEn_-1 contexto:1,decision:1,creadoEn:-1 - ttl:-");
                if (run == 0)
                {
                    first = marketIndexes;
                }
                else
                {
                    marketIndexes.Should().Equal(first);
                }
            }
        }
        finally
        {
            await client.DropDatabaseAsync(market);
            await client.DropDatabaseAsync(messaging);
        }
    }
}
