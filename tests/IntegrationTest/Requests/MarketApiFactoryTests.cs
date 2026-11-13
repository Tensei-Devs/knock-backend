extern alias MarketApi;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Modules.Requests;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>Spec 020, T-02: el host de pruebas de <c>Knock.Market.Api</c> arranca sobre contenedores, con base propia por host, y registra el módulo Requests.</summary>
public class MarketApiFactoryTests
{
    [Fact]
    public async Task Host_Boots_WithItsOwnDatabase_AndReportsReady_R_17()
    {
        using var first = await MarketApiFactory.CreateAsync();
        using var second = await MarketApiFactory.CreateAsync();
        using var client = first.CreateClient();

        using var ready = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        ready.StatusCode.Should().Be(HttpStatusCode.OK);
        first.DatabaseName.Should().NotBe(second.DatabaseName, "cada host usa una base propia para que las pruebas no se pisen");
        (await first.Services.GetRequiredService<IMongoClient>().ListDatabaseNamesAsync()).Should().NotBeNull();
    }

    [Fact]
    public async Task Host_RegistersTheRequestsModule_AndAnswersUnknownRoutesAsProblemDetails_R_17()
    {
        using var factory = await MarketApiFactory.CreateAsync();
        using var scope = factory.Services.CreateScope();
        using var client = factory.CreateClient();

        var api = scope.ServiceProvider.GetRequiredService<IRequestsApi>();
        using var response = await client.GetAsync(new Uri("/api/v1/esta-ruta-no-existe", UriKind.Relative));

        api.ModuleName.Should().Be("Requests");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetInt32().Should().Be(404);
    }
}
