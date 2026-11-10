using System.Collections.Concurrent;
using System.Net;
using AwesomeAssertions;
using Knock.Contracts.Correlation;
using Knock.IntegrationTest.Gateway.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using Xunit;

namespace Knock.IntegrationTest.Gateway;

/// <summary>
/// Spec 011 CA-01: una petición que cruza el gateway y un servicio conserva el mismo
/// <c>X-Correlation-Id</c> en el encabezado de respuesta y en los registros de ambos lados.
/// El servicio de prueba usa el mismo <c>CorrelationIdMiddleware</c> de <c>Knock.Contracts</c>
/// que usan los servicios reales (Gateway, Identity, Messaging), por eso su registro captura
/// la misma propiedad descrita por <see cref="CorrelationId.LogProperty"/>.
/// (Spec 011 §5 describe el formato como <c>correlationId</c>; el código emite
/// <c>CorrelationId</c> por decisión documentada en <c>Memoria.md</c>; aquí se verifica el valor,
/// no el nombre.)
/// </summary>
public sealed class CorrelationAcrossServicesTests : IAsyncLifetime, IDisposable
{
    private static readonly Action<ILogger, Exception?> _served =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, "ServicioDePrueba"), "Servicio de prueba atendió la petición");

    private readonly CapturingLogProcessor _gatewayLogs = new();
    private readonly LogCaptureProvider _serviceLogs = new();
    private WebApplication? _service;
    private GatewayTestHost? _gateway;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(_serviceLogs);
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        var app = builder.Build();
        app.UseKnockCorrelationId();
        app.Map(
            "/{**ruta}",
            (HttpContext context, ILogger<CorrelationAcrossServicesTests> logger) =>
            {
                _served(logger, null);
                return Results.Ok(new { servicio = "mercado", correlationId = CorrelationId.Current(context) });
            });
        await app.StartAsync();
        _service = app;
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First() + "/";

        _gateway = GatewayTestHost.Start(
            new Dictionary<string, string> { ["identidad"] = address, ["mercado"] = address, ["mensajeria"] = address },
            services => services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddProcessor(_gatewayLogs)));
        _client = _gateway.Client;
    }

    public async Task DisposeAsync()
    {
        if (_gateway is not null)
        {
            await _gateway.DisposeAsync();
        }

        if (_service is not null)
        {
            await _service.StopAsync();
            await _service.DisposeAsync();
        }
    }

    public void Dispose()
    {
        _gateway?.Dispose();
        _gatewayLogs.Dispose();
        _serviceLogs.Dispose();
    }

    [Fact]
    public async Task Request_WithoutHeader_GeneratesAndKeepsTheSameIdOnBothSides_CA_011_01()
    {
        var response = await _client!.GetAsync(new Uri("/api/v1/oficios", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var fromResponse = response.Headers.GetValues(CorrelationId.HeaderName).Should().ContainSingle().Subject;
        CorrelationId.IsValid(fromResponse).Should().BeTrue();
        (await response.Content.ReadAsStringAsync()).Should().Contain(fromResponse);

        _gatewayLogs.CorrelationIds().Should().NotBeEmpty().And.OnlyContain(id => id == fromResponse);
        _serviceLogs.CorrelationIds().Should().ContainSingle().Which.Should().Be(fromResponse);
    }

    [Fact]
    public async Task Request_WithAValidHeader_KeepsTheSameIdOnBothSides_CA_011_01()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/oficios", UriKind.Relative));
        request.Headers.Add(CorrelationId.HeaderName, "cliente-abc-123");

        var response = await _client!.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues(CorrelationId.HeaderName).Should().ContainSingle().Which.Should().Be("cliente-abc-123");
        (await response.Content.ReadAsStringAsync()).Should().Contain("cliente-abc-123");

        _gatewayLogs.CorrelationIds().Should().NotBeEmpty().And.OnlyContain(id => id == "cliente-abc-123");
        _serviceLogs.CorrelationIds().Should().ContainSingle().Which.Should().Be("cliente-abc-123");
    }
}

/// <summary>Copia los registros de OpenTelemetry del gateway (los que reenvía Serilog vía <c>OpenTelemetryLogSink</c>) y extrae el identificador de correlación.</summary>
internal sealed class CapturingLogProcessor : BaseProcessor<LogRecord>
{
    private readonly ConcurrentQueue<string?> _correlationIds = new();

    public IReadOnlyList<string?> CorrelationIds() => [.. _correlationIds];

    public override void OnEnd(LogRecord data)
    {
        if (data.Attributes is not null)
        {
            foreach (var attribute in data.Attributes)
            {
                if (string.Equals(attribute.Key, CorrelationId.LogProperty, StringComparison.Ordinal))
                {
                    _correlationIds.Enqueue(attribute.Value as string);
                }
            }
        }
    }
}

/// <summary>Registra cada scope con el identificador (incluido el del middleware de correlación) del servicio de prueba.</summary>
internal sealed class LogCaptureProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string?> _correlationIds = new();

    public void Dispose()
    {
    }

    public ILogger CreateLogger(string categoryName) => new CaptureLogger(this);

    public IReadOnlyList<string?> CorrelationIds() => [.. _correlationIds];

    private void RecordScope(object? state)
    {
        if (state is IEnumerable<KeyValuePair<string, object>> pairs)
        {
            foreach (var pair in pairs)
            {
                if (string.Equals(pair.Key, CorrelationId.LogProperty, StringComparison.Ordinal))
                {
                    _correlationIds.Enqueue(pair.Value as string);
                }
            }
        }
    }

    private sealed class CaptureLogger(LogCaptureProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            provider.RecordScope(state);
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            provider.RecordScope(state);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
