using AwesomeAssertions;
using Knock.Hosting.Procesos;
using Knock.Market.Api.Common.Bandeja;
using Knock.Market.Api.Common.TareasProgramadas;
using Knock.UnitTest.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.UnitTest.Requests;

/// <summary>Spec 054, R-13: la bandeja y el ejecutor de Mercado dejan métrica y registro de cada intento con tipo, identificador, intento y resultado.</summary>
public sealed class Spec054MercadoMetricasTests
{
    private const string _eventType = "VisitaCerrada";
    private const string _taskType = "VencerSolicitud";
    private const string _personalText = "texto-que-no-debe-salir";

    [Theory]
    [InlineData(true, false, 0, "entregado")]
    [InlineData(false, false, 0, "reintento")]
    [InlineData(false, false, 4, "fallido")]
    [InlineData(false, true, 0, "fallido")]
    public async Task Cada_Entrega_Deja_Metrica_Y_Registro_CA_054_20(bool accepted, bool rejected, int previousAttempts, string expected)
    {
        var personal = Guid.NewGuid().ToString("D");
        var id = $"{_eventType}:{personal}:1";
        var published = ProcesosMetrics.Identificador(id);
        var result = (accepted, rejected) switch
        {
            (true, _) => ResultadoDeEntrega.Aceptado,
            (_, true) => ResultadoDeEntrega.Rechazado,
            _ => ResultadoDeEntrega.Reintentable,
        };
        var message = new MensajeReservado(id, _eventType, new BsonDocument("nota", _personalText), "r", previousAttempts);
        var logger = new ProcesosRegistro<DespachadorDeBandeja>();
        var dispatcher = new DespachadorDeBandeja(new FakeBandeja(message), [new FakeDelivery(result)], TimeProvider.System, logger);
        using var measurements = new ProcesosMedidas();

        await dispatcher.DispatchOneAsync(CancellationToken.None);

        var tags = measurements.Of(ProcesosMetrics.AttemptsInstrument, published).Should().ContainSingle().Subject;
        tags["proceso"].Should().Be("outbox");
        tags["tipo"].Should().Be(_eventType);
        tags["intento"].Should().Be(previousAttempts + 1);
        tags["resultado"].Should().Be(expected);
        tags.Values.Should().NotContain(_personalText);
        logger.Entries.Should().Contain(x => x.Contains(published, StringComparison.Ordinal) && x.Contains(expected, StringComparison.Ordinal))
            .And.NotContain(x => x.Contains(_personalText, StringComparison.Ordinal) || x.Contains(personal, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, 0, "hecha")]
    [InlineData(true, 0, "reintento")]
    [InlineData(true, 4, "fallida")]
    public async Task Cada_Tarea_Deja_Metrica_Y_Registro_CA_054_20(bool fails, int previousAttempts, string expected)
    {
        var task = new TareaReservada(ObjectId.GenerateNewId(), _taskType, _personalText, "r", previousAttempts);
        var id = task.Id.ToString();
        var logger = new ProcesosRegistro<EjecutorDeTareasProgramadas>();
        var executor = new EjecutorDeTareasProgramadas(new FakeTasks(task), [new FakeHandler(fails)], TimeProvider.System, logger);
        using var measurements = new ProcesosMedidas();

        await executor.ExecuteOneAsync(CancellationToken.None);

        var tags = measurements.Of(ProcesosMetrics.AttemptsInstrument, id).Should().ContainSingle().Subject;
        tags["proceso"].Should().Be("tareas_programadas");
        tags["intento"].Should().Be(previousAttempts + 1);
        tags["resultado"].Should().Be(expected);
        tags.Values.Should().NotContain(_personalText, "la clave de negocio no sale en la telemetría");
        logger.Entries.Should().Contain(x => x.Contains(id, StringComparison.Ordinal) && x.Contains(expected, StringComparison.Ordinal))
            .And.NotContain(x => x.Contains(_personalText, StringComparison.Ordinal));
    }

    private sealed class FakeBandeja(MensajeReservado message) : IBandejaStore
    {
        public Task<MensajeReservado?> ReserveAsync(DateTime now, IReadOnlyCollection<string> types, CancellationToken cancellationToken) => Task.FromResult<MensajeReservado?>(message);

        public Task<bool> MarkDeliveredAsync(MensajeReservado mensaje, DateTime now, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> FailAsync(MensajeReservado mensaje, bool permanent, DateTime now, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class FakeDelivery(ResultadoDeEntrega result) : IEntregaDeBandeja
    {
        public string Tipo => _eventType;

        public Task<ResultadoDeEntrega> DeliverAsync(MensajeReservado mensaje, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class FakeTasks(TareaReservada reserved) : ITareasProgramadasStore
    {
        public Task<TareaReservada?> ReserveAsync(DateTime now, CancellationToken cancellationToken, IReadOnlyCollection<string>? types = null) => Task.FromResult<TareaReservada?>(reserved);

        public Task<bool> CompleteAsync(TareaReservada task, DateTime now, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> FailAsync(TareaReservada task, DateTime now, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> ConfirmLeaseAsync(IClientSessionHandle session, TareaReservada task, DateTime now, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class FakeHandler(bool fails) : IManejadorDeTarea
    {
        public string Tipo => _taskType;

        public Task HandleAsync(TareaReservada task, CancellationToken cancellationToken) =>
            fails ? throw new InvalidOperationException("boom") : Task.CompletedTask;
    }
}
