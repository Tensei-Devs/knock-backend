using AwesomeAssertions;
using Knock.Market.Api.Modules.Media;
using Knock.Market.Api.Modules.Media.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Knock.UnitTest.Media;

public class MediaModuleRegistrationTests
{
    [Fact]
    public void AddMediaModule_RegistersTimeProvider_OI1_01()
    {
        var services = new ServiceCollection();
        services.AddMediaModule();
        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<TimeProvider>().Should().Be(TimeProvider.System);
    }

    [Fact]
    public void ZeroReducedMaxSide_FailsOnStart_OI1_03()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Media:ReducedMaxSide"] = "0" })
            .Build());
        services.AddMediaModule();
        var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredService<IOptions<MediaOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }
}
