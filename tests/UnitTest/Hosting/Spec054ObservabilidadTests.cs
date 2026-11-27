using AwesomeAssertions;
using Knock.Hosting.Procesos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Knock.UnitTest.Hosting;

/// <summary>Spec 054, T-07 (R-13, R-14): métricas, alerta y registros de la bandeja y de las tareas, sin MongoDB y con tiempo simulado.</summary>
public sealed class Spec054ObservabilidadTests
{
    private static readonly DateTime _now = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string[] _tags = ["proceso", "tipo", "id", "intento", "resultado"];

    [Fact]
    public async Task Alerta_Sin_Datos_Personales_CA_054_20()
    {
        var eventId = $"cotizacion_nueva:{Guid.NewGuid():N}:1";
        var taskId = Guid.NewGuid().ToString("N");
        var query = new FakeQuery(
            new AlertaDeProceso(ProcesosMetrics.Outbox, "cotizacion_nueva", eventId, 5, ProcesosMetrics.Fallido),
            new AlertaDeProceso(ProcesosMetrics.Tareas, "EliminarCuenta", taskId, 0, ProcesosMetrics.Atrasado));
        var logger = new ProcesosRegistro<VigilanteDeProcesos>();
        var watcher = new VigilanteDeProcesos(new FixedTime(), logger);
        using var measurements = new ProcesosMedidas();

        await watcher.EvaluateAsync([query], CancellationToken.None);

        query.LateBefore.Should().Be(_now.AddMinutes(-10), "el atraso que alerta es de más de 10 minutos");
        var tags = measurements.Of(ProcesosMetrics.AlertsInstrument, eventId).Should().ContainSingle().Subject;
        tags.Keys.Should().BeEquivalentTo(_tags);
        tags["tipo"].Should().Be("cotizacion_nueva");
        tags["intento"].Should().Be(5);
        tags["resultado"].Should().Be("fallido");
        measurements.Of(ProcesosMetrics.AlertsInstrument, taskId).Should().ContainSingle().Which["resultado"].Should().Be("atrasado");
        logger.Entries.Should().HaveCount(2).And.Contain(x => x.Contains("process_alert", StringComparison.Ordinal) && x.Contains(eventId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task La_Metrica_Sigue_Mientras_Dure_La_Alerta_Y_El_Registro_Sale_Una_Vez_CA_054_20()
    {
        var id = Guid.NewGuid().ToString("N");
        var alert = new AlertaDeProceso(ProcesosMetrics.Tareas, "VencerSolicitud", id, 5, ProcesosMetrics.Fallida);
        var logger = new ProcesosRegistro<VigilanteDeProcesos>();
        var watcher = new VigilanteDeProcesos(new FixedTime(), logger);
        using var measurements = new ProcesosMedidas();

        // Dos servicios que apuntan a la misma base no duplican la alerta.
        await watcher.EvaluateAsync([new FakeQuery(alert), new FakeQuery(alert)], CancellationToken.None);
        await watcher.EvaluateAsync([new FakeQuery(alert)], CancellationToken.None);

        measurements.Of(ProcesosMetrics.AlertsInstrument, id).Should().HaveCount(2);
        logger.Entries.Should().ContainSingle();

        await watcher.EvaluateAsync([new FakeQuery()], CancellationToken.None);
        await watcher.EvaluateAsync([new FakeQuery(alert)], CancellationToken.None);

        logger.Entries.Should().HaveCount(2, "una alerta que se resolvió y vuelve se registra de nuevo");
    }

    [Theory]
    [InlineData(false, true, false, "reserva_perdida")]
    [InlineData(true, true, false, "entregado")]
    [InlineData(true, false, false, "reintento")]
    [InlineData(true, false, true, "fallido")]
    public void Resultado_De_Cada_Intento_CA_054_20(bool owned, bool succeeded, bool lastFailure, string expected) =>
        ProcesosMetrics.Outcome(owned, succeeded, lastFailure, ProcesosMetrics.Entregado, ProcesosMetrics.Fallido).Should().Be(expected);

    [Fact]
    public void La_Alerta_Es_Un_Proceso_De_Fondo_Sin_Comprobacion_De_Salud_CA_054_21()
    {
        var services = new ServiceCollection().AddKnockVigilanciaDeProcesos();

        services.Should().NotContain(x => x.ServiceType == typeof(IConfigureOptions<HealthCheckServiceOptions>));
        services.Should().ContainSingle(x => x.ServiceType == typeof(IHostedService));
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(_now, TimeSpan.Zero);
    }

    private sealed class FakeQuery(params AlertaDeProceso[] alerts) : IConsultaDeAlertas
    {
        public DateTime LateBefore { get; private set; }

        public Task<IReadOnlyList<AlertaDeProceso>> FindAsync(DateTime atrasadasAntesDe, CancellationToken cancellationToken)
        {
            LateBefore = atrasadasAntesDe;
            return Task.FromResult<IReadOnlyList<AlertaDeProceso>>(alerts);
        }
    }
}
