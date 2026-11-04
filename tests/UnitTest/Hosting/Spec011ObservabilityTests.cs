extern alias gateway;
extern alias identity;
extern alias messaging;

using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using Xunit;
using GatewayHosting = gateway::Knock.Gateway.Hosting;
using IdentityHosting = identity::Knock.Identity.Api.Hosting;
using MessagingHosting = messaging::Knock.Messaging.Api.Hosting;

namespace Knock.UnitTest.Hosting;

/// <summary>
/// Spec 011 en Identity, Messaging y el Gateway, con el mismo cableado que su <c>Program.cs</c> (<c>AddKnockLogging</c> y
/// <c>AddKnockObservability</c>) y su <c>appsettings.json</c> del repositorio. Lo que se mira es el registro que sale hacia OpenTelemetry
/// (Application Insights), ya pasado por la redacción. El <c>correlationId</c> del contrato no se prueba aquí: llega con
/// <c>Knock.Contracts</c> (PR #3). CA-03 (alerta) se verifica a mano en el PR, con la infraestructura de KNOCK-200.
/// </summary>
public sealed class Spec011ObservabilityTests
{
    private const string _environment = "Testing";

    /// <summary>JWT ficticio armado en tiempo de ejecución (no hay token literal en el código; ver EXC-004 y EXC-005).</summary>
    private static readonly string _fakeJwt = string.Join('.', "eyJ" + "hbGciOiJub25lIn0", "eyJ" + "zdWIiOiJmaWN0aWNpbyJ9", "firma-ficticia");

    private static readonly Action<ILogger, int, Exception?> _orderProcessed =
        LoggerMessage.Define<int>(LogLevel.Warning, new EventId(1, "OrderProcessed"), "Pedido {OrderCount} procesado");

    private static readonly Action<ILogger, string, string, string, string, Exception?> _contactWithPersonalData =
        LoggerMessage.Define<string, string, string, string>(
            LogLevel.Information, new EventId(2, "Contact"), "Contacto {Contacto} con {Documento} en {Ubicacion} y {Credencial}");

    private static readonly Action<ILogger, string, Exception?> _phoneByKey =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(3, "PhoneByKey"), "Alta de {Phone}");

    private static readonly Action<ILogger, Exception?> _frameworkInfo =
        LoggerMessage.Define(LogLevel.Information, new EventId(4, "FrameworkInfo"), "Detalle del framework");

    private static readonly Dictionary<string, Service> _services = new(StringComparer.Ordinal)
    {
        ["identity"] = new(
            "Knock.Identity.Api",
            "knock-identity",
            (services, configuration, environment) => IdentityHosting.ObservabilityExtensions.AddKnockObservability(
                IdentityHosting.LoggingExtensions.AddKnockLogging(services, configuration), configuration, environment),
            IdentityHosting.TelemetryRedactor.Redact,
            IdentityHosting.TelemetryRedactor.RedactAttribute,
            IdentityHosting.TelemetryRedactor.Placeholder),
        ["messaging"] = new(
            "Knock.Messaging.Api",
            "knock-messaging",
            (services, configuration, environment) => MessagingHosting.ObservabilityExtensions.AddKnockObservability(
                MessagingHosting.LoggingExtensions.AddKnockLogging(services, configuration), configuration, environment),
            MessagingHosting.TelemetryRedactor.Redact,
            MessagingHosting.TelemetryRedactor.RedactAttribute,
            MessagingHosting.TelemetryRedactor.Placeholder),
        ["gateway"] = new(
            "Knock.Gateway",
            "knock-gateway",
            (services, configuration, environment) => GatewayHosting.ObservabilityExtensions.AddKnockObservability(
                GatewayHosting.LoggingExtensions.AddKnockLogging(services, configuration), configuration, environment),
            GatewayHosting.TelemetryRedactor.Redact,
            GatewayHosting.TelemetryRedactor.RedactAttribute,
            GatewayHosting.TelemetryRedactor.Placeholder),
    };

    public static TheoryData<string> Services() => [.. _services.Keys];

    /// <summary>Texto con un dato personal o secreto ficticio y el fragmento que no debe sobrevivir a la redacción.</summary>
    public static TheoryData<string, string, string> SensitiveText()
    {
        (string Input, string Secret)[] cases =
        [
            ("celular +52 55 5555 0101 registrado", "5555 0101"),
            ("celular +525555550101", "5555550101"),
            ("query phone=%2B525555550101", "525555550101"),
            ("celular=5555123456", "5555123456"),
            ("curp XEXX010101HNEXXXA4 encontrada", "XEXX010101HNEXXXA4"),
            ("curp xexx010101hnexxxa4 encontrada", "xexx010101hnexxxa4"),
            ("token " + _fakeJwt, "firma-ficticia"),
            ("Authorization: Bearer abc.def-ghi", "abc.def-ghi"),
            ("?token=t-ficticio&code=777", "t-ficticio"),
            ("{\"refreshToken\":\"r-ficticio\"}", "r-ficticio"),
            ("cerca de 19.4326, -99.1332", "19.4326"),
            ("ruta /at/19.4326/-99.1332", "99.1332"),
            ("lat=19.43&lon=-99.13", "19.43"),
        ];
        var data = new TheoryData<string, string, string>();
        foreach (var service in _services.Keys)
        {
            foreach (var (input, secret) in cases)
            {
                data.Add(service, input, secret);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Services))]
    public async Task Log_WrittenThroughServiceWiring_HasTimestampLevelServiceAndStructuredMessage_CA_011_01(string service)
    {
        var records = await CaptureAsync(service, factory => _orderProcessed(factory.CreateLogger<Spec011ObservabilityTests>(), 3, null));

        var record = records.Should().ContainSingle().Subject;
        record.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
        record.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        record.Level.Should().Be(LogLevel.Warning);
        record.Category.Should().Be(typeof(Spec011ObservabilityTests).FullName);
        record.Message.Should().Be("Pedido 3 procesado");
        record.Attributes.Should().Contain(new KeyValuePair<string, object?>("OrderCount", 3));
        record.Attributes.Should().Contain(new KeyValuePair<string, object?>("{OriginalFormat}", "Pedido {OrderCount} procesado"));
        record.Resource.Should().Contain("service.name", _services[service].ServiceName);
        record.Resource.Should().Contain("deployment.environment", _environment);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public async Task Log_FrameworkInformation_IsFilteredByConfiguredLevel_CA_011_01(string service)
    {
        // appsettings.json: Serilog:MinimumLevel:Override:Microsoft.AspNetCore = Warning (evita llenar la cuota con el detalle del framework).
        var records = await CaptureAsync(service, factory =>
        {
            _frameworkInfo(factory.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics"), null);
            _frameworkInfo(factory.CreateLogger("Knock.Prueba"), null);
        });

        records.Select(r => r.Category).Should().Equal("Knock.Prueba");
    }

    [Theory]
    [MemberData(nameof(Services))]
    public async Task Log_WithFakePhoneCurpTokenAndCoordinates_DoesNotExportThem_CA_011_02(string service)
    {
        string[] secrets = ["+52 55 5555 0101", "5555 0101", "XEXX010101HNEXXXA4", "19.4326", "99.1332", _fakeJwt, "firma-ficticia"];

        var records = await CaptureAsync(service, factory =>
        {
            var logger = factory.CreateLogger<Spec011ObservabilityTests>();
            _contactWithPersonalData(logger, "+52 55 5555 0101", "XEXX010101HNEXXXA4", "19.4326, -99.1332", "Bearer " + _fakeJwt, null);
            _phoneByKey(logger, "5555550101", null);
        });

        records.Should().HaveCount(2);
        // Primer registro: datos en texto libre (mensaje y atributos). Segundo: la clave «Phone» se redacta siempre en el atributo.
        // El mensaje renderizado del segundo no se revisa: un celular nacional sin «+» en texto libre es el riesgo aceptado de
        // docs/OBSERVABILIDAD.md (pendiente de decisión: el mensaje lo repite tal cual).
        var exported = records.Take(1).SelectMany(r => r.Attributes.Select(a => Convert.ToString(a.Value, System.Globalization.CultureInfo.InvariantCulture)).Append(r.Message).Append(r.Body));
        foreach (var text in exported)
        {
            foreach (var secret in secrets)
            {
                (text ?? string.Empty).Should().NotContain(secret);
            }
        }

        records[0].Message.Should().Contain(_services[service].Placeholder);
        records[1].Attributes.Should().Contain(new KeyValuePair<string, object?>("Phone", _services[service].Placeholder));
    }

    [Theory]
    [MemberData(nameof(SensitiveText))]
    public void Redact_FakePersonalDataOrToken_IsMasked_CA_011_02(string service, string input, string secret)
    {
        var redacted = _services[service].Redact(input);

        redacted.Should().NotContain(secret);
        redacted.Should().Contain(_services[service].Placeholder);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void Redact_TextWithoutPersonalData_IsUnchanged_CA_011_02(string service)
    {
        string[] inputs = ["HTTP GET /api/v1/oficios responded 200 in 12.3456 ms", "StatusCode=500 errorCode=42", "build 1.20251102.1234"];

        inputs.Select(_services[service].Redact).Should().Equal(inputs);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void RedactAttribute_SensitiveKey_IsAlwaysReplaced_OtherKeysKeepTheirValue_CA_011_02(string service)
    {
        var (redact, placeholder) = (_services[service].RedactAttribute, _services[service].Placeholder);

        redact("lat", 19.4326).Should().Be(placeholder);
        redact("phone", "5555550101").Should().Be(placeholder);
        redact("curp", "XEXX010101HNEXXXA4").Should().Be(placeholder);
        redact("refresh_token", "r-ficticio").Should().Be(placeholder);
        redact("authorization", "Bearer x").Should().Be(placeholder);
        string[] notes = ["+52 55 5555 0101", "ok"];
        redact("notes", notes).Should().BeEquivalentTo(new[] { placeholder, "ok" });
        redact("StatusCode", 500).Should().Be(500);
        redact("RequestPath", "/api/v1/oficios").Should().Be("/api/v1/oficios");
    }

    /// <summary>Arranca un host con el cableado y el appsettings.json del servicio, escribe los logs y devuelve lo que llega al exportador.</summary>
    private static async Task<IReadOnlyList<CapturedLog>> CaptureAsync(string service, Action<ILoggerFactory> write)
    {
        var wiring = _services[service];
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = _environment,
            ApplicationName = wiring.Project,
        });
        builder.Configuration.AddJsonFile(RepositoryAppSettings(wiring.Project), optional: false);
        wiring.Register(builder.Services, builder.Configuration, builder.Environment);
        using var capture = new CapturingLogProcessor();
        // Se registra después de AddKnockObservability: corre detrás de LogRedactionProcessor, como el exportador de Azure Monitor.
        builder.Services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddProcessor(capture));

        using (var host = builder.Build())
        {
            await host.StartAsync();
            write(host.Services.GetRequiredService<ILoggerFactory>());
            await host.StopAsync();
        }

        // Los avisos de arranque y parada del propio host no son parte de la prueba.
        return [.. capture.Records.Where(r => !string.Equals(r.Category, "Microsoft.Hosting.Lifetime", StringComparison.Ordinal))];
    }

    private static string RepositoryAppSettings(string project)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Knock.slnx")))
        {
            dir = dir.Parent;
        }

        return dir is null
            ? throw new InvalidOperationException("No se encontró Knock.slnx subiendo desde el directorio de pruebas.")
            : Path.Combine(dir.FullName, "src", project, "appsettings.json");
    }

    private sealed record Service(
        string Project,
        string ServiceName,
        Action<IServiceCollection, IConfiguration, IHostEnvironment> Register,
        Func<string, string> Redact,
        Func<string, object?, object?> RedactAttribute,
        string Placeholder);

    private sealed record CapturedLog(
        DateTime Timestamp,
        LogLevel Level,
        string? Category,
        string? Message,
        string? Body,
        IReadOnlyList<KeyValuePair<string, object?>> Attributes,
        IReadOnlyDictionary<string, object> Resource);

    /// <summary>Copia cada registro al terminar (el <see cref="LogRecord"/> se recicla) junto con el recurso del proveedor.</summary>
    private sealed class CapturingLogProcessor : BaseProcessor<LogRecord>
    {
        public ConcurrentQueue<CapturedLog> Records { get; } = new();

        public override void OnEnd(LogRecord data)
        {
            var resource = ParentProvider?.GetResource().Attributes.ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal)
                ?? new Dictionary<string, object>(StringComparer.Ordinal);
            Records.Enqueue(new CapturedLog(
                data.Timestamp,
                data.LogLevel,
                data.CategoryName,
                data.FormattedMessage,
                data.Body,
                [.. data.Attributes ?? []],
                resource));
        }
    }
}
