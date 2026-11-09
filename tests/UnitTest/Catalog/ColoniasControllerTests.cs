using AwesomeAssertions;
using Knock.Market.Api.Modules.Catalog.Controllers.V1;
using Knock.Market.Api.Modules.Catalog.Domain;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Knock.Market.Api.Modules.Catalog.Services;
using Xunit;

namespace Knock.UnitTest.Catalog;

/// <summary>TT-API-07: el controller convierte el punto en colonia y no devuelve nada más (ni las coordenadas ni campos internos).</summary>
public class ColoniasControllerTests
{
    private sealed class FakeLocator(Colonia? answer) : INeighborhoodLocator
    {
        public List<(double Latitude, double Longitude)> Calls { get; } = [];

        public Task<Colonia?> FindAtAsync(double latitude, double longitude, CancellationToken cancellationToken)
        {
            Calls.Add((latitude, longitude));
            return Task.FromResult(answer);
        }
    }

    [Fact]
    public async Task PointInsideACoverage_ReturnsOnlyTheFourColoniaFields_SEC_TT_API_07_11()
    {
        var locator = new FakeLocator(new Colonia("665f1b2c3d4e5f6071829304", "Centro", "86000", "Centro"));
        var controller = new ColoniasController(locator);

        var result = await controller.GetAtPoint(new NeighborhoodAtPointQuery { Lat = "20.0512347", Lon = "-99.3456781" }, CancellationToken.None);

        var response = result.Value!;
        response.EnCobertura.Should().BeTrue();
        response.Colonia.Should().Be(new NeighborhoodAtPointColoniaResponse("665f1b2c3d4e5f6071829304", "Centro", "86000", "Centro"));
        locator.Calls.Should().ContainSingle().Which.Should().Be((20.0512347, -99.3456781));
    }

    [Fact]
    public async Task PointOutsideTheCoverage_IsA200WithANullColoniaNotA404_SEC_TT_API_07_12()
    {
        var controller = new ColoniasController(new FakeLocator(null));

        var result = await controller.GetAtPoint(new NeighborhoodAtPointQuery { Lat = "0", Lon = "0" }, CancellationToken.None);

        result.Value.Should().Be(new NeighborhoodAtPointResponse(false, null));
    }
}
