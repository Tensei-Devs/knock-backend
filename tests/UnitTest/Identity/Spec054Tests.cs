using AwesomeAssertions;
using Knock.Hosting.Procesos;
using Knock.Identity.Api.Hosting.Procesos;
using Knock.Identity.Api.Hosting.Procesos.Outbox;
using Knock.Identity.Api.Hosting.Procesos.Reintentos;
using Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;
using Knock.UnitTest.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 054 (T-02 a T-05): catálogo de plazos, reintentos, bandeja de salida y ejecutor de Identidad con dobles en memoria y tiempo simulado (sin MongoDB).</summary>
public sealed class Spec054Tests
{
    private static readonly DateTime _now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly int[] _attempts = [1, 2, 3, 4];

    [Theory]
    [InlineData("RepartirOleada", 15 * 60)]
    [InlineData("RevisarOleada", 15 * 60)]
    [InlineData("VencerSolicitud", 48 * 3600)]
    [InlineData("AvisarAlVerificador", 24 * 3600)]
    [InlineData("BorrarFotosDeVerificacion", 7 * 86400)]
    [InlineData("EliminarCuenta", 7 * 86400)]
    [InlineData("PrepararExportacion", 0)]
    [InlineData("VencerRecuperacion", 7 * 86400)]
    [InlineData("VencerCotizacionFinal", 48 * 3600)]
    [InlineData("VencerAjuste", 24 * 3600)]
    [InlineData("VencerPropuestaDeReprogramacion", 24 * 3600)]
    [InlineData("CerrarPeriodoDeOpinion", 30 * 86400)]
    [InlineData("CerrarReporteDeNoTerminado", 7 * 86400)]
    [InlineData("VencerSuspension", 7 * 86400)]
    public void Plazo_De_Cada_Tipo_CA_054_13(string tipo, int seconds)
    {
        CatalogoDeTareas.ProgramadaPara(tipo, _now).Should().Be(_now.AddSeconds(seconds));
        CatalogoDeTareas.Tipos.Should().HaveCount(14);
    }

    [Fact]
    public void Propuesta_De_Reprogramacion_Vence_Lo_Primero_Entre_24h_Y_Dos_Horas_Antes_De_La_Cita_CA_054_13()
    {
        CatalogoDeTareas.ProgramadaPara("VencerPropuestaDeReprogramacion", _now, _now.AddHours(10)).Should().Be(_now.AddHours(8));
        CatalogoDeTareas.ProgramadaPara("VencerPropuestaDeReprogramacion", _now, _now.AddDays(5)).Should().Be(_now.AddHours(24));
        CatalogoDeTareas.ProgramadaPara("VencerPropuestaDeReprogramacion", _now, _now.AddHours(1)).Should().Be(_now);
    }

    [Fact]
    public void Tipo_Fuera_Del_Catalogo_No_Se_Programa_CA_054_12()
    {
        var act = () => CatalogoDeTareas.ProgramadaPara("Inventada", _now);

        act.Should().Throw<ArgumentException>().WithMessage("unknown_task_type*");
    }

    [Fact]
    public void La_Limpieza_Conserva_14_Dias_Lo_Hecho_Y_90_Lo_Fallido_Y_Lo_Reciente_No_Vence_CA_054_18()
    {
        // El borrado lo hace el índice TTL de `limpiarEn` (`Spec054OutboxScriptTests`, CA_054_09); aquí se fija la fecha que cada cierre le pone.
        PoliticaDeReintentos.LimpiarEn(_now, fallido: false).Should().Be(_now.AddDays(PoliticaDeReintentos.RetencionTerminadoDias));
        PoliticaDeReintentos.LimpiarEn(_now, fallido: true).Should().Be(_now.AddDays(90));
        PoliticaDeReintentos.LimpiarEn(_now, fallido: false).Should().BeAfter(_now.AddDays(13), "un documento de menos de 14 días se conserva");
    }

    [Fact]
    public void Esperas_De_Reintento_Y_Limpieza_CA_054_06()
    {
        _attempts.Select(x => PoliticaDeReintentos.Espera(x)!.Value.TotalSeconds).Should().Equal(5, 30, 120, 600);
        PoliticaDeReintentos.Espera(5).Should().BeNull();
        PoliticaDeReintentos.LimpiarEn(_now, fallido: false).Should().Be(_now.AddDays(14));
        PoliticaDeReintentos.LimpiarEn(_now, fallido: true).Should().Be(_now.AddDays(90));
    }

    [Fact]
    public void Id_Determinista_CA_054_03()
    {
        OutboxReglas.BuildId("cotizacion_nueva", "abc", 2).Should().Be("cotizacion_nueva:abc:2");
        OutboxReglas.BuildId("cotizacion_nueva", "abc", 2).Should().Be(OutboxReglas.BuildId("cotizacion_nueva", "abc", 2));
    }

    [Fact]
    public void Carga_Solo_Con_Identificadores_CA_054_04()
    {
        var ok = new EventoDeOutbox("a:b:1", "cotizacion_nueva", "messaging", new BsonDocument { ["destinatarioId"] = "u1", ["recurso"] = ObjectId.GenerateNewId() });
        var phone = ok with { Carga = new BsonDocument { ["telefono"] = "5512345678" } };
        var nested = ok with { Carga = new BsonDocument { ["datos"] = new BsonDocument { ["x"] = "1" } } };
        var unknownType = ok with { Tipo = "otro" };
        var unknownTarget = ok with { Destino = "otro" };

        OutboxReglas.Validate(ok);
        new[] { phone, nested }.Should().AllSatisfy(x => ((Action)(() => OutboxReglas.Validate(x))).Should().Throw<ArgumentException>().WithMessage("payload_not_allowed*"));
        ((Action)(() => OutboxReglas.Validate(unknownType))).Should().Throw<ArgumentException>().WithMessage("unknown_event_type*");
        ((Action)(() => OutboxReglas.Validate(unknownTarget))).Should().Throw<ArgumentException>().WithMessage("unknown_destination*");
    }

    [Fact]
    public async Task Entrega_Y_Marca_Entregado_CA_054_05()
    {
        var store = new FakeOutbox(Event());
        var processor = Processor(store, ResultadoDeEntrega.Aceptado);

        (await processor.ProcessOneAsync(CancellationToken.None)).Should().BeTrue();

        store.Single.Estado.Should().Be("entregado");
        (await processor.ProcessOneAsync(CancellationToken.None)).Should().BeFalse("ya no queda nada pendiente");
    }

    [Fact]
    public async Task Reintentos_Y_Fallo_Al_Quinto_CA_054_06()
    {
        var store = new FakeOutbox(Event());
        var processor = Processor(store, ResultadoDeEntrega.Reintentable);
        var waits = new List<double>();

        for (var i = 0; i < 5; i++)
        {
            store.Single.ProgramadaPara = _now;
            await processor.ProcessOneAsync(CancellationToken.None);
            waits.Add((store.Single.ProgramadaPara - _now).TotalSeconds);
        }

        waits.Take(4).Should().Equal(5, 30, 120, 600);
        store.Single.Estado.Should().Be("fallido");
        store.Single.Intentos.Should().Be(5);
    }

    [Fact]
    public async Task Sin_Reintento_En_401_403_CA_054_07()
    {
        var store = new FakeOutbox(Event());

        await Processor(store, ResultadoDeEntrega.Rechazado).ProcessOneAsync(CancellationToken.None);

        store.Single.Estado.Should().Be("fallido");
        store.Single.Intentos.Should().Be(1);
    }

    [Fact]
    public async Task Excepcion_De_La_Entrega_Se_Trata_Como_Reintentable_CA_054_06()
    {
        var store = new FakeOutbox(Event());
        var processor = new ProcesadorDeOutbox(store, new ThrowingDelivery(), new FixedTime(_now), NullLogger<ProcesadorDeOutbox>.Instance);

        await processor.ProcessOneAsync(CancellationToken.None);

        store.Single.Estado.Should().Be("pendiente");
        store.Single.Intentos.Should().Be(1);
    }

    [Fact]
    public async Task Ejecuta_Una_Vez_Y_Queda_Hecha_CA_054_10()
    {
        var store = new FakeTasks(new FakeTask("VencerSolicitud", _now.AddMinutes(-1)));
        var handler = new CountingHandler("VencerSolicitud");
        var executor = Executor(store, handler);

        (await executor.ExecuteOneAsync(CancellationToken.None)).Should().BeTrue();
        (await executor.ExecuteOneAsync(CancellationToken.None)).Should().BeFalse();

        handler.Calls.Should().Be(1);
        store.Single.Estado.Should().Be("hecha");
    }

    [Fact]
    public async Task Tarea_Aun_No_Vencida_No_Se_Ejecuta_CA_054_19()
    {
        var store = new FakeTasks(new FakeTask("CerrarPeriodoDeOpinion", _now.AddDays(30)));
        var handler = new CountingHandler("CerrarPeriodoDeOpinion");

        (await Executor(store, handler).ExecuteOneAsync(CancellationToken.None)).Should().BeFalse();
        (await Executor(store, handler, _now.AddDays(30)).ExecuteOneAsync(CancellationToken.None)).Should().BeTrue();

        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Manejador_Que_Falla_Reintenta_Y_Queda_Fallida_CA_054_11()
    {
        var store = new FakeTasks(new FakeTask("VencerSolicitud", _now));
        var executor = Executor(store, new CountingHandler("VencerSolicitud", fail: true));
        var waits = new List<double>();

        for (var i = 0; i < 5; i++)
        {
            store.Single.Programada = _now;
            await executor.ExecuteOneAsync(CancellationToken.None);
            waits.Add((store.Single.Programada - _now).TotalSeconds);
        }

        waits.Take(4).Should().Equal(5, 30, 120, 600);
        store.Single.Estado.Should().Be("fallida");
    }

    [Fact]
    public async Task Tipo_Desconocido_No_Se_Ejecuta_Y_Queda_Fallida_CA_054_12()
    {
        var store = new FakeTasks(new FakeTask("Inventada", _now));
        var handler = new CountingHandler("VencerSolicitud");

        await Executor(store, handler).ExecuteOneAsync(CancellationToken.None);

        handler.Calls.Should().Be(0);
        store.Single.Estado.Should().Be("fallida");
    }

    [Fact]
    public async Task Tipo_Del_Catalogo_Sin_Manejador_Queda_Pendiente_CA_054_12()
    {
        var store = new FakeTasks(new FakeTask("EliminarCuenta", _now));

        (await Executor(store).ExecuteOneAsync(CancellationToken.None)).Should().BeFalse();

        store.Single.Estado.Should().Be("pendiente");
    }

    [Fact]
    public async Task Entrega_Y_Tarea_Dejan_Metrica_Y_Registro_CA_054_20()
    {
        var eventId = $"cotizacion_nueva:{Guid.NewGuid():N}:1";
        var task = new FakeTask("VencerSolicitud", _now);
        var unknown = new FakeTask("Inventada", _now);
        var logger = new ProcesosRegistro<ProcesadorDeOutbox>();
        var processor = new ProcesadorDeOutbox(new FakeOutbox(Event() with { Id = eventId }), new FixedDelivery(ResultadoDeEntrega.Aceptado), new FixedTime(_now), logger);
        using var measurements = new ProcesosMedidas();

        await processor.ProcessOneAsync(CancellationToken.None);
        await Executor(new FakeTasks(task), new CountingHandler("VencerSolicitud", fail: true)).ExecuteOneAsync(CancellationToken.None);
        await Executor(new FakeTasks(unknown)).ExecuteOneAsync(CancellationToken.None);

        var delivered = measurements.Of(ProcesosMetrics.AttemptsInstrument, eventId).Should().ContainSingle().Subject;
        delivered["resultado"].Should().Be("entregado");
        delivered["intento"].Should().Be(1);
        measurements.Of(ProcesosMetrics.AttemptsInstrument, task.Row.Id).Should().ContainSingle().Which["resultado"].Should().Be("reintento");
        measurements.Of(ProcesosMetrics.AttemptsInstrument, unknown.Row.Id).Should().ContainSingle().Which["resultado"].Should().Be("fallida");
        logger.Entries.Should().ContainSingle().Which.Should().Contain(eventId).And.Contain("entregado");
    }

    [Fact]
    public void Salud_No_Depende_De_La_Cola_CA_054_21()
    {
        var services = new ServiceCollection().AddProcesosDeFondo();

        services.Should().NotContain(x => x.ServiceType == typeof(IConfigureOptions<HealthCheckServiceOptions>));
        services.Should().Contain(x => x.ServiceType == typeof(IConsultaDeAlertas));
    }

    private static EventoDeOutbox Event() => new("cotizacion_nueva:abc:1", "cotizacion_nueva", "messaging", new BsonDocument { ["destinatarioId"] = "u1" });

    private static ProcesadorDeOutbox Processor(FakeOutbox store, ResultadoDeEntrega result) =>
        new(store, new FixedDelivery(result), new FixedTime(_now), NullLogger<ProcesadorDeOutbox>.Instance);

    private static EjecutorDeTareasProgramadas Executor(FakeTasks store, IManejadorDeTarea? handler = null, DateTime? now = null) =>
        new(store, handler is null ? [] : [handler], new FixedTime(now ?? _now), NullLogger<EjecutorDeTareasProgramadas>.Instance);

    private sealed class FixedTime(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }

    private sealed class FixedDelivery(ResultadoDeEntrega result) : IEntregaInterna
    {
        public Task<ResultadoDeEntrega> DeliverAsync(OutboxReservado evento, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class ThrowingDelivery : IEntregaInterna
    {
        public Task<ResultadoDeEntrega> DeliverAsync(OutboxReservado evento, CancellationToken cancellationToken) => throw new HttpRequestException("down");
    }

    private sealed class CountingHandler(string tipo, bool fail = false) : IManejadorDeTarea
    {
        public int Calls { get; private set; }

        public string Tipo => tipo;

        public Task HandleAsync(TareaReservada task, CancellationToken cancellationToken)
        {
            Calls++;
            return fail ? throw new InvalidOperationException("boom") : Task.CompletedTask;
        }
    }

    private sealed class Row(string id, string tipo, DateTime programada)
    {
        public string Id { get; } = id;
        public string Tipo { get; } = tipo;
        public string Estado { get; set; } = "pendiente";
        public DateTime ProgramadaPara { get; set; } = programada;
        public int Intentos { get; set; }
        public string? Reserva { get; set; }
    }

    private sealed class FakeOutbox : IOutboxStore
    {
        private readonly Row _row;
        private readonly EventoDeOutbox _event;

        public FakeOutbox(EventoDeOutbox evento)
        {
            _event = evento;
            _row = new Row(evento.Id, evento.Tipo, _now);
        }

        public Row Single => _row;

        public Task<bool> AddAsync(IClientSessionHandle session, EventoDeOutbox evento, DateTime now, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<OutboxReservado?> ReserveAsync(DateTime now, CancellationToken cancellationToken)
        {
            if (_row.Estado != "pendiente" || _row.ProgramadaPara > now)
            {
                return Task.FromResult<OutboxReservado?>(null);
            }

            _row.Estado = "en_proceso";
            _row.Reserva = "r";
            return Task.FromResult<OutboxReservado?>(new OutboxReservado(_row.Id, _row.Tipo, _event.Destino, _event.Carga, "r", _row.Intentos));
        }

        public Task<bool> MarkDeliveredAsync(OutboxReservado evento, DateTime now, CancellationToken cancellationToken)
        {
            _row.Estado = "entregado";
            return Task.FromResult(true);
        }

        public Task<bool> FailAsync(OutboxReservado evento, bool permanent, DateTime now, CancellationToken cancellationToken)
        {
            _row.Intentos = evento.Intentos + 1;
            var delay = permanent ? null : PoliticaDeReintentos.Espera(_row.Intentos);
            _row.Estado = delay is null ? "fallido" : "pendiente";
            _row.ProgramadaPara = delay is { } wait ? now + wait : _row.ProgramadaPara;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeTask(string tipo, DateTime programada)
    {
        public Row Row { get; } = new(ObjectId.GenerateNewId().ToString(), tipo, programada);
    }

    private sealed class FakeTasks : ITareasProgramadasStore
    {
        private readonly Row _row;

        public FakeTasks(FakeTask task) => _row = task.Row;

        public TaskView Single => new(_row);

        public Task<bool> ScheduleAsync(IClientSessionHandle? session, string type, string businessKey, DateTime due, DateTime now, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<TareaReservada?> ReserveAsync(DateTime now, IReadOnlyCollection<string> excludedTypes, CancellationToken cancellationToken)
        {
            if (_row.Estado != "pendiente" || _row.ProgramadaPara > now || excludedTypes.Contains(_row.Tipo))
            {
                return Task.FromResult<TareaReservada?>(null);
            }

            _row.Estado = "en_proceso";
            return Task.FromResult<TareaReservada?>(new TareaReservada(ObjectId.Parse(_row.Id), _row.Tipo, "k", "r", _row.Intentos));
        }

        public Task<bool> CompleteAsync(TareaReservada task, DateTime now, CancellationToken cancellationToken)
        {
            _row.Estado = "hecha";
            return Task.FromResult(true);
        }

        public Task<bool> FailAsync(TareaReservada task, DateTime now, CancellationToken cancellationToken)
        {
            _row.Intentos = task.Intentos + 1;
            var delay = PoliticaDeReintentos.Espera(_row.Intentos);
            _row.Estado = delay is null ? "fallida" : "pendiente";
            _row.ProgramadaPara = delay is { } wait ? now + wait : _row.ProgramadaPara;
            return Task.FromResult(true);
        }

        public Task<bool> AbortAsync(TareaReservada task, DateTime now, CancellationToken cancellationToken)
        {
            _row.Estado = "fallida";
            return Task.FromResult(true);
        }

        public Task<bool> ConfirmLeaseAsync(IClientSessionHandle session, TareaReservada task, DateTime now, CancellationToken cancellationToken) => Task.FromResult(true);

        internal sealed class TaskView(Row row)
        {
            public string Estado => row.Estado;

            public DateTime Programada
            {
                get => row.ProgramadaPara;
                set => row.ProgramadaPara = value;
            }
        }
    }
}
