extern alias IdentityApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using IdentityApi::Knock.Identity.Api.Modules.Safety.Data;
using IdentityApi::Knock.Identity.Api.Modules.Safety.Dtos;
using IdentityApi::Knock.Identity.Api.Modules.Safety.Services;
using Knock.IntegrationTest.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Identity;

/// <summary>Mercado de mentira: responde lo que diga <see cref="Result"/> y cuenta las lecturas.</summary>
internal sealed class FakeMarketReports : IMarketNotFinishedReports
{
    public MarketReportResult Result { get; set; } = MarketReportResult.Failure(MarketReportStatus.Unavailable);

    public int Reads { get; private set; }

    public Task<MarketReportResult> ReadAsync(string visitId, CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult(Result);
    }
}

/// <summary>
/// Spec 053 (BB13): <c>POST /api/v1/interno/reportes-no-terminados</c> en Identidad. El evento <c>ReporteNoTerminadoCreado</c> lleva solo identificadores, el texto se lee en Mercado y el reporte queda
/// <c>abierto</c> en la cola del panel, una sola vez por visita. MongoDB real; solo datos ficticios.
/// </summary>
public sealed class Spec053ReporteNoTerminadoTests
{
    private const string _route = "/api/v1/interno/reportes-no-terminados";
    private const string _permission = "reporte-no-terminado-creado";
    private const string _text = "El trabajo no quedo terminado, ficticio";

    private sealed class Context : IAsyncDisposable
    {
        public FakeMarketReports Market { get; } = new();

        public CapturingLoggerProvider Logs { get; } = new();

        public IdentityApiFactory Factory { get; private set; } = null!;

        public HttpClient Client { get; private set; } = null!;

        public IMongoCollection<BsonDocument> Reports => Factory.Database.GetCollection<BsonDocument>("reportes");

        public async Task InitAsync()
        {
            Factory = await IdentityApiFactory.CreateAsync(
                new Dictionary<string, string?> { ["Security:InternalTokens:MarketKey"] = InternalServiceTokens.KeyBase64 },
                services =>
                {
                    services.RemoveAll<IMarketNotFinishedReports>();
                    services.AddSingleton<IMarketNotFinishedReports>(Market);
                    services.AddLogging(logging => logging.AddProvider(Logs));
                });
            await Factory.Services.GetRequiredService<ReportesStore>().EnsureIndexesAsync(CancellationToken.None);
            Client = Factory.CreateClient();
        }

        public async ValueTask DisposeAsync()
        {
            Client?.Dispose();
            if (Factory is not null)
            {
                await Factory.DisposeAsync();
            }

            Logs.Dispose();
        }
    }

    private static async Task<Context> StartAsync()
    {
        var context = new Context();
        await context.InitAsync();
        return context;
    }

    private static string Token(string permission = _permission, byte[]? key = null, string issuer = "knock-market", string audience = "knock-identity", TimeSpan? lifetime = null) =>
        InternalServiceTokens.Issue("n/a", "n/a", [permission], issuer, audience, key, lifetime);

    private static object Body(string visit, string client, string technician, string? extra = null) =>
        extra is null
            ? new { eventoId = $"ReporteNoTerminadoCreado:{visit}", tipo = "ReporteNoTerminadoCreado", visitaId = visit, clienteId = client, tecnicoUsuarioId = technician }
            : new { eventoId = $"ReporteNoTerminadoCreado:{visit}", tipo = "ReporteNoTerminadoCreado", visitaId = visit, clienteId = client, tecnicoUsuarioId = technician, texto = extra };

    private static async Task<HttpResponseMessage> PostAsync(Context context, object body, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_route, UriKind.Relative)) { Content = JsonContent.Create(body) };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await context.Client.SendAsync(request);
    }

    private static (string Visit, string Client, string Technician) Ids() => (ObjectId.GenerateNewId().ToString(), Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"));

    private static void MarketHas(Context context, (string Visit, string Client, string Technician) ids) =>
        context.Market.Result = new MarketReportResult(MarketReportStatus.Found, new MarketNotFinishedReport(ids.Visit, ids.Client, ids.Technician, _text, DateTime.UtcNow));

    [Fact]
    public async Task Post_WithTheMarketToken_OpensTheReportInTheQueueWithTheMarketText_CA_053_REPORTE_NO_TERMINADO()
    {
        await using var context = await StartAsync();
        var ids = Ids();
        MarketHas(context, ids);

        using var response = await PostAsync(context, Body(ids.Visit, ids.Client, ids.Technician), Token());

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var report = await context.Reports.Find(Builders<BsonDocument>.Filter.Eq("visitaNoTerminadaId", ids.Visit)).SingleAsync();
        report["reportaId"].AsString.Should().Be(ids.Client);
        report["reportadoId"].AsString.Should().Be(ids.Technician);
        report["estado"].AsString.Should().Be("abierto");
        report["motivo"].AsString.Should().Be("mal_servicio");
        report["descripcion"].AsString.Should().Be(_text);
        report["referencia"]["tipo"].AsString.Should().Be("visita");
        report["referencia"]["id"].AsString.Should().Be(ids.Visit);
        report.Contains("gravedad").Should().BeFalse();
    }

    [Fact]
    public async Task Post_TheSameEventTwiceOrAtTheSameTime_OpensASingleReport_CA_053_REPORTE_NO_TERMINADO()
    {
        await using var context = await StartAsync();
        var ids = Ids();
        MarketHas(context, ids);
        var body = Body(ids.Visit, ids.Client, ids.Technician);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => PostAsync(context, body, Token())));
        using var again = await PostAsync(context, body, Token());

        responses.Select(r => r.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.Accepted);
        again.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await context.Reports.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("visitaNoTerminadaId", ids.Visit))).Should().Be(1);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Post_WhenMarketDoesNotAnswer_Answers503AndOpensNothing_ThenTheRetryOpensIt_CA_053_REPORTE_NO_TERMINADO()
    {
        await using var context = await StartAsync();
        var ids = Ids();

        using var down = await PostAsync(context, Body(ids.Visit, ids.Client, ids.Technician), Token());
        MarketHas(context, ids);
        using var retry = await PostAsync(context, Body(ids.Visit, ids.Client, ids.Technician), Token());

        down.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        retry.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await context.Reports.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("visitaNoTerminadaId", ids.Visit))).Should().Be(1);
    }

    [Fact]
    public async Task Post_WhenMarketHasNoReportOrItsPeopleDiffer_OpensNothing_CA_053_REPORTE_NO_TERMINADO()
    {
        await using var context = await StartAsync();
        var ids = Ids();
        context.Market.Result = MarketReportResult.Failure(MarketReportStatus.NotFound);
        using var notFound = await PostAsync(context, Body(ids.Visit, ids.Client, ids.Technician), Token());
        MarketHas(context, (ids.Visit, Guid.NewGuid().ToString("D"), ids.Technician));
        using var differs = await PostAsync(context, Body(ids.Visit, ids.Client, ids.Technician), Token());

        notFound.StatusCode.Should().Be(HttpStatusCode.Accepted);
        differs.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await context.Reports.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(0);
    }

    [Fact]
    public async Task Post_WithoutTokenOrWithTheWrongOneOrPermission_NeverReadsMarketNorOpensAnything_CA_053_REPORTE_NO_TERMINADO()
    {
        await using var context = await StartAsync();
        var ids = Ids();
        MarketHas(context, ids);
        var body = Body(ids.Visit, ids.Client, ids.Technician);

        using var anonymous = await PostAsync(context, body, null);
        using var wrongKey = await PostAsync(context, body, Token(key: new byte[32]));
        using var wrongIssuer = await PostAsync(context, body, Token(issuer: "knock-identity"));
        using var wrongAudience = await PostAsync(context, body, Token(audience: "knock-market"));
        using var tooLong = await PostAsync(context, body, Token(lifetime: TimeSpan.FromMinutes(30)));
        using var otherPermission = await PostAsync(context, body, Token("bloqueos"));

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongKey.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongIssuer.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongAudience.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        tooLong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        otherPermission.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        context.Market.Reads.Should().Be(0);
        (await context.Reports.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(0);
    }

    [Fact]
    public async Task Post_WithoutAConfiguredKey_FailsClosed_CA_053_REPORTE_NO_TERMINADO()
    {
        var market = new FakeMarketReports();
        await using var factory = await IdentityApiFactory.CreateAsync(configureServices: services => services.AddSingleton<IMarketNotFinishedReports>(market));
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_route, UriKind.Relative)) { Content = JsonContent.Create(Body(ObjectId.GenerateNewId().ToString(), Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"))) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token());

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_WithABadBodyOrAnExtraField_Answers422WithoutEchoingTheValues_CA_053_REPORTE_NO_TERMINADO()
    {
        await using var context = await StartAsync();
        var ids = Ids();

        using var badVisit = await PostAsync(context, Body("con espacio", ids.Client, ids.Technician), Token());
        using var extra = await PostAsync(context, Body(ids.Visit, ids.Client, ids.Technician, extra: "texto-que-no-debe-viajar"), Token());

        badVisit.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        extra.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await badVisit.Content.ReadAsStringAsync()).Should().NotContain("con espacio");
        (await extra.Content.ReadAsStringAsync()).Should().NotContain("texto-que-no-debe-viajar");
        context.Market.Reads.Should().Be(0);
    }

    [Fact]
    public async Task Post_NeverWritesTheTextOrThePeopleToTheLogs_CA_053_REPORTE_NO_TERMINADO()
    {
        await using var context = await StartAsync();
        var ids = Ids();
        MarketHas(context, ids);

        using var response = await PostAsync(context, Body(ids.Visit, ids.Client, ids.Technician), Token());

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var logged = context.Logs.All();
        logged.Should().NotContain(_text).And.NotContain(ids.Client).And.NotContain(ids.Technician);
    }
}
