extern alias MarketApi;

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Bandeja = MarketApi::Knock.Market.Api.Common.Bandeja;
using BandejaStore = MarketApi::Knock.Market.Api.Modules.Requests.Data.BandejaStore;

namespace Knock.IntegrationTest.Requests;

/// <summary>
/// Spec 053 (BB15): el despachador de la bandeja de Mercado entrega <c>ReporteNoTerminadoCreado</c> (solo identificadores) a la ruta interna de Identidad con token firmado con la clave de Mercado, reintenta
/// con espera, no repite lo entregado y no registra la carga ni el token. MongoDB real; el destino es un manejador HTTP de mentira; solo datos ficticios.
/// </summary>
public sealed class Spec053DespachadorBandejaTests
{
    private static readonly string _marketKeyBase64 = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    [Fact]
    public async Task Dispatch_DeliversIdsOnlyWithMarketToken_AndMarksDelivered_CA_053_DESPACHADOR()
    {
        await using var scope = await ScopeAsync(HttpStatusCode.Accepted);

        (await scope.Dispatcher.DispatchOneAsync(CancellationToken.None)).Should().BeTrue();

        var message = scope.Handler.Requests.Should().ContainSingle().Subject;
        message.Path.Should().Be("/api/v1/interno/reportes-no-terminados");
        message.Body.Should().Contain(scope.VisitId).And.NotContain("texto");
        var token = await new JsonWebTokenHandler().ValidateTokenAsync(message.Token, new TokenValidationParameters
        {
            ValidIssuer = "knock-market",
            ValidAudience = "knock-identity",
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(_marketKeyBase64)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        });
        token.IsValid.Should().BeTrue();
        token.ClaimsIdentity.FindAll("permisos").Select(x => x.Value).Should().Contain("reporte-no-terminado-creado");
        (await scope.StateAsync()).Should().Be("entregado");
        (await scope.Dispatcher.DispatchOneAsync(CancellationToken.None)).Should().BeFalse();
        scope.Handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task Dispatch_OnServerError_RetriesWithDelayThenFails_CA_053_DESPACHADOR()
    {
        await using var scope = await ScopeAsync(HttpStatusCode.ServiceUnavailable);

        foreach (var delay in new[] { 5, 30, 120, 600 })
        {
            (await scope.Dispatcher.DispatchOneAsync(CancellationToken.None)).Should().BeTrue();
            (await scope.StateAsync()).Should().Be("pendiente");
            (await scope.Dispatcher.DispatchOneAsync(CancellationToken.None)).Should().BeFalse();
            scope.Clock.Advance(TimeSpan.FromSeconds(delay));
        }

        (await scope.Dispatcher.DispatchOneAsync(CancellationToken.None)).Should().BeTrue();
        (await scope.StateAsync()).Should().Be("fallido");
        scope.Handler.Requests.Should().HaveCount(5);
    }

    [Fact]
    public async Task Dispatch_OnUnauthorized_FailsAtOnce_AndLogsNoPayloadOrToken_CA_053_DESPACHADOR()
    {
        await using var scope = await ScopeAsync(HttpStatusCode.Unauthorized);

        (await scope.Dispatcher.DispatchOneAsync(CancellationToken.None)).Should().BeTrue();

        (await scope.StateAsync()).Should().Be("fallido");
        scope.Handler.Requests.Should().HaveCount(1);
        var logged = scope.Logs.All();
        logged.Should().NotContain(scope.Handler.Requests[0].Token).And.NotContain(scope.ClientId).And.NotContain(scope.TechnicianId);
    }

    [Fact]
    public async Task Dispatch_WithoutKeyOrAddress_LeavesTheMessagePending_CA_053_DESPACHADOR()
    {
        await using var scope = await ScopeAsync(HttpStatusCode.Accepted, withKey: false);

        (await scope.Dispatcher.DispatchOneAsync(CancellationToken.None)).Should().BeTrue();

        scope.Handler.Requests.Should().BeEmpty();
        (await scope.StateAsync()).Should().Be("pendiente");
    }

    [Fact]
    public async Task Dispatch_IgnoresOtherTypes_CA_053_DESPACHADOR()
    {
        await using var scope = await ScopeAsync(HttpStatusCode.Accepted, type: "VisitaCerrada");

        (await scope.Dispatcher.DispatchOneAsync(CancellationToken.None)).Should().BeFalse();

        scope.Handler.Requests.Should().BeEmpty();
        (await scope.StateAsync()).Should().Be("pendiente");
    }

    private static async Task<DispatchScope> ScopeAsync(HttpStatusCode answer, bool withKey = true, string type = "ReporteNoTerminadoCreado")
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        var scope = new DispatchScope(fixture.ConnectionString, answer, withKey, type);
        await scope.SeedAsync();
        return scope;
    }

    private sealed class DispatchScope : IAsyncDisposable, IDisposable
    {
        private readonly MongoClient _client;
        private readonly string _name = "s053_disp_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        private readonly IMongoDatabase _database;
        private readonly CapturingLoggerProvider _logs = new();
        private readonly CaptureHandler _handler;
        private readonly string _type;
        private readonly string _id;

        public DispatchScope(string connection, HttpStatusCode answer, bool withKey, string type)
        {
            _client = new MongoClient(connection);
            _database = _client.GetDatabase(_name);
            _handler = new CaptureHandler(answer);
            _type = type;
            VisitId = ObjectId.GenerateNewId().ToString();
            _id = $"{type}:{VisitId}";
            var settings = new Dictionary<string, string?> { ["Services:Identity:BaseUrl"] = withKey ? "http://identity.test" : null, ["Security:InternalTokens:MarketKey"] = withKey ? _marketKeyBase64 : null };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            var delivery = new Bandeja.EntregaReporteNoTerminado(new HandlerFactory(_handler), configuration, Options.Create(new Bandeja.BandejaOptions()), Clock);
            Dispatcher = new Bandeja.DespachadorDeBandeja(new BandejaStore(_database), [delivery], Clock, new TypedLogger<Bandeja.DespachadorDeBandeja>(_logs.CreateLogger("test")));
        }

        public Bandeja.DespachadorDeBandeja Dispatcher { get; }

        public CaptureHandler Handler => _handler;

        public TestClock Clock { get; } = new();

        public CapturingLoggerProvider Logs => _logs;

        public string VisitId { get; }

        public string ClientId { get; } = Guid.NewGuid().ToString("D");

        public string TechnicianId { get; } = Guid.NewGuid().ToString("D");

        public Task SeedAsync() => _database.GetCollection<BsonDocument>("outbox").InsertOneAsync(new BsonDocument
        {
            ["_id"] = _id,
            ["tipo"] = _type,
            ["agregadoId"] = VisitId,
            ["datos"] = new BsonDocument { ["visitaId"] = VisitId, ["clienteId"] = ClientId, ["tecnicoUsuarioId"] = TechnicianId },
            ["estado"] = "pendiente",
            ["intentos"] = 0,
            ["creadoEn"] = Clock.GetUtcNow().UtcDateTime,
        });

        public async Task<string> StateAsync() => (await _database.GetCollection<BsonDocument>("outbox").Find(Builders<BsonDocument>.Filter.Eq("_id", _id)).SingleAsync())["estado"].AsString;

        public async ValueTask DisposeAsync()
        {
            await _client.DropDatabaseAsync(_name);
            Dispose();
        }

        public void Dispose()
        {
            _client.Dispose();
            _handler.Dispose();
            _logs.Dispose();
        }
    }

    internal sealed record Sent(string Path, string Body, string Token);

    internal sealed class CaptureHandler(HttpStatusCode answer) : HttpMessageHandler
    {
        public List<Sent> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new Sent(request.RequestUri!.AbsolutePath, await request.Content!.ReadAsStringAsync(cancellationToken), request.Headers.Authorization!.Parameter!));
            return new HttpResponseMessage(answer);
        }
    }

    private sealed class HandlerFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class TypedLogger<T>(Microsoft.Extensions.Logging.ILogger inner) : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => inner.IsEnabled(logLevel);

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            inner.Log(logLevel, eventId, state, exception, formatter);
    }

    internal sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan value) => _now += value;
    }
}
