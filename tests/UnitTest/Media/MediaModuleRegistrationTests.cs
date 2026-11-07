using AwesomeAssertions;
using Knock.Market.Api.Modules.Media;
using Microsoft.Extensions.DependencyInjection;
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
}
