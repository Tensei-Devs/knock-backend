using AwesomeAssertions;
using Knock.Market.Api.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace Knock.UnitTest.Hosting;

/// <summary>Spec 003, CA-05 (R-09): la región de los datos es West US y otra región impide arrancar.</summary>
public sealed class Spec003RegionTests
{
    [Fact]
    public void RepositoryAppSettings_DeclaresWestUs_AndStartupAcceptsIt_CA_05()
    {
        var configuration = new ConfigurationBuilder().AddJsonFile(MongoTestServices.RepositoryAppSettings(), optional: false).Build();
        using var provider = MongoTestServices.Build(
            new Dictionary<string, string?> { ["ConnectionStrings:KnockMongo"] = MongoTestServices.SyntheticConnectionString() },
            withRepositoryAppSettings: true);

        provider.GetRequiredService<IStartupValidator>().Validate();

        configuration[KnockMongoOptions.RegionKey].Should().Be("westus");
        provider.GetRequiredService<IOptions<KnockMongoOptions>>().Value.Region.Should().Be("westus");
    }

    [Theory]
    [InlineData("eastus")]
    [InlineData("WestUS")]
    [InlineData("westus2")]
    [InlineData("")]
    public void OtherRegion_FailsStartupNamingTheKey_CA_05(string region)
    {
        var connectionString = MongoTestServices.SyntheticConnectionString();
        using var provider = MongoTestServices.Build(new Dictionary<string, string?>
        {
            ["ConnectionStrings:KnockMongo"] = connectionString,
            ["Mongo:MaxPoolSize"] = "7",
            [KnockMongoOptions.RegionKey] = region,
        });

        var startup = () => provider.GetRequiredService<IStartupValidator>().Validate();

        var message = startup.Should().Throw<OptionsValidationException>().Which.Message;
        message.Should().Contain(KnockMongoOptions.RegionKey);
        message.Should().NotContain(connectionString).And.NotContain(MongoUrl.Create(connectionString).Password);
    }
}
