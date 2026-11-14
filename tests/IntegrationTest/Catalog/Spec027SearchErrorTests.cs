extern alias MarketApi;

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using AwesomeAssertions;
using Knock.Contracts.Correlation;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Hosting;
using MarketApi::Knock.Market.Api.Modules.Catalog.Controllers;
using MarketApi::Knock.Market.Api.Modules.Catalog.Data;
using MarketApi::Knock.Market.Api.Modules.Catalog.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Catalog;

public sealed class Spec027SearchErrorTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task Cursor_Invalido_ProblemDetails_Sin_Datos_Internos_CA_027_11(string environment)
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        using var client = new MongoClient(fixture.ConnectionString);
        var name = "search_errors_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        var database = client.GetDatabase(name);
        try
        {
            var id = ObjectId.GenerateNewId();
            await database.GetCollection<BsonDocument>("colonias").InsertOneAsync(new BsonDocument { ["_id"] = id, ["activo"] = true });
            await database.GetCollection<BsonDocument>("catalogo_meta").InsertOneAsync(new BsonDocument { ["_id"] = "sinonimos", ["version"] = 1 });
            await using var app = await StartAsync(database, id.ToString(), "cursor-malformado", environment);
            using var response = await app.GetTestClient().GetAsync(new Uri("/probe", UriKind.Relative));
            await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_cursor");
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task Mongo_Caido_ProblemDetails_Sin_Datos_Internos_CA_027_17(string environment)
    {
        var settings = new MongoClientSettings { Server = new MongoServerAddress("127.0.0.1", 1), ServerSelectionTimeout = TimeSpan.FromMilliseconds(500) };
        using var client = new MongoClient(settings);
        await using var app = await StartAsync(client.GetDatabase("search_offline"), ObjectId.GenerateNewId().ToString(), null, environment);
        using var response = await app.GetTestClient().GetAsync(new Uri("/probe", UriKind.Relative));
        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, "search_unavailable");
    }

    private static async Task<WebApplication> StartAsync(IMongoDatabase database, string colonia, string? cursor, string environment)
    {
        var options = Options.Create(new KnockMongoOptions());
        var cache = new DisabledCatalogCache();
        var source = new BusquedaSource(database, options, new CatalogStore(database, options, cache), new BusquedaStore(database, options));
        var service = new BusquedaService(source, cache, new FiltroDatosPersonalesNoDisponible(), TimeProvider.System, RandomNumberGenerator.GetBytes(32));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Services.AddKnockProblemDetails();
        var app = builder.Build();
        app.UseKnockCorrelationId();
        app.MapGet("/probe", async (HttpContext context) =>
        {
            var result = await service.SearchAsync(new SearchInput("boiler", colonia, null, false, 20, cursor), context.RequestAborted);
            await SearchProblems.WriteAsync(context, result.Error!);
        });
        await app.StartAsync();
        return app;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("code").GetString().Should().Be(code);
        json.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        json.RootElement.GetProperty("correlationId").GetString().Should().Be(response.Headers.GetValues(Knock.Contracts.Correlation.CorrelationId.HeaderName).Single());
        body.Should().NotContain("SearchRank").And.NotContain("distance").And.NotContain("cursor-malformado").And.NotContain("127.0.0.1").And.NotContain("stackTrace").And.NotContain("detail");
    }
}
