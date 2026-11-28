using System.Net;
using Knock.IntegrationTest.Support;
using Xunit;

namespace Knock.IntegrationTest.Contract;

/// <summary>Spec 034 (R-02, CA-04): con MongoDB inaccesible la vida sigue en 200 (no reinicia el contenedor) y la disponibilidad sale de rotación con 503.</summary>
public sealed class Spec034SaludTests
{
    [Fact]
    public async Task Vida_No_Depende_De_Mongo_CA_034_04()
    {
        await using var market = MarketApiFactory.CreateOffline(_ => { });
        using var client = market.CreateClient();

        using var live = await client.GetAsync(new Uri("/health", UriKind.Relative));
        using var ready = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
    }
}
