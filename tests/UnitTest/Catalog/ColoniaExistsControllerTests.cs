using AwesomeAssertions;
using Knock.Market.Api.Modules.Catalog.Controllers.V1;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Knock.Market.Api.Modules.Catalog.Services;
using Xunit;

namespace Knock.UnitTest.Catalog;

/// <summary>Spec 017, R-06: el controller de «¿existe esta colonia?» responde solo el booleano del nombre que recibe.</summary>
public class ColoniaExistsControllerTests
{
    private sealed class FakeNames(bool answer) : INeighborhoodNames
    {
        public List<string> Calls { get; } = [];

        public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken)
        {
            Calls.Add(name);
            return Task.FromResult(answer);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Exists_AnswersOnlyTheBooleanForTheName_CA_017_07(bool known)
    {
        var names = new FakeNames(known);
        var controller = new ColoniaExistsController(names);

        var result = await controller.Exists(new NeighborhoodExistsQuery { Nombre = "Tultengo" }, CancellationToken.None);

        result.Value.Should().Be(new NeighborhoodExistsResponse(known));
        names.Calls.Should().ContainSingle().Which.Should().Be("Tultengo");
    }
}
