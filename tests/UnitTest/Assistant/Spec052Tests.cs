using AwesomeAssertions;
using FluentValidation;
using Knock.Market.Api.Modules.Assistant.Data;
using Knock.Market.Api.Modules.Assistant.Dtos;
using Knock.Market.Api.Modules.Assistant.Services;
using Knock.Market.Api.Modules.Assistant.Validators;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Knock.UnitTest.Assistant;

internal sealed class FakeAi : IAiClient
{
    public string? Reason { get; set; }

    public AiOrigin Origin { get; set; } = AiOrigin.Provider;

    public string ProviderMessage { get; set; } = "Parece una fuga pequeña. Llama al 55 5555 1234 si necesitas ayuda.";

    public List<SolicitudIa> Requests { get; } = [];

    public Task<ResultadoIa<T>> GenerarAsync<T>(SolicitudIa solicitud, CancellationToken cancellationToken) where T : class, IAiOutput<T>
    {
        Requests.Add(solicitud);
        object value = solicitud.Funcion switch
        {
            AiFunction.AsistenteKnock => new AsistenteKnockSalida { Categoria = "Plomería", Mensaje = ProviderMessage, PideFoto = true, Seguridad = null },
            AiFunction.ResumenChat => new ResumenChatSalida { Sabe = ["Llave que gotea. Vive en Calle Hidalgo 123"], PorConfirmar = ["Horario"], QueLlevar = ["Empaque"] },
            _ => new BorradorCotizacionSalida { PrecioSugerido = 350, TiempoEstimado = "1 hora", Dia = "mañana", Nota = "Escríbeme al 55 5555 1234", Explicacion = "Por la mano de obra" },
        };
        return Task.FromResult(new ResultadoIa<T>((T?)value, Origin, Reason, [], false, 1));
    }
}

internal sealed class FakeCuentas(bool technician = true) : IAsistenteCuentas
{
    public Task<bool> IsTechnicianAsync(string sub, CancellationToken cancellationToken) => Task.FromResult(technician);
}

internal sealed class FakeConversaciones(ConversationAccessStatus status = ConversationAccessStatus.Ok) : IConversacionesAsistente
{
    public Task<ConversationAccess> GetForTechnicianAsync(string conversationId, string sub, CancellationToken cancellationToken) =>
        Task.FromResult(new ConversationAccess(status, status == ConversationAccessStatus.Ok ? "Cliente: se rompió el grifo" : string.Empty));
}

internal sealed class FakeSolicitudes(bool participates = true, DateTime? closed = null) : ISolicitudesAsistente
{
    public Task<RequestAccess> GetAccessAsync(string solicitudId, string sub, CancellationToken cancellationToken) => Task.FromResult(new RequestAccess(participates, closed));
}

internal sealed class FakeReferencia : IReferenciaMercado
{
    public int Calls { get; private set; }

    public Task<PriceRange?> GetRangeAsync(string oficio, string tipoTrabajo, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult<PriceRange?>(new PriceRange(300, 600, 12, "cotizaciones_aceptadas"));
    }
}

internal sealed class MemoryBitacoras : IBitacoraStore
{
    private readonly Dictionary<(string, string), BitacoraRecord> _items = [];

    public Task<bool> UpsertAsync(BitacoraRecord log, CancellationToken cancellationToken)
    {
        var created = !_items.ContainsKey((log.SolicitudId, log.TecnicoId));
        _items[(log.SolicitudId, log.TecnicoId)] = log;
        return Task.FromResult(created);
    }

    public Task<BitacoraRecord?> FindAsync(string solicitudId, string tecnicoId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryGetValue((solicitudId, tecnicoId), out var log) ? log : null);
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public sealed class Spec052AsistenteTests
{
    private const string _solicitud = "0123456789abcdef01234567";
    private static readonly DateTimeOffset _now = new(2026, 11, 21, 18, 0, 0, TimeSpan.Zero);

    private static AsistenteService Assistant(FakeAi ai, bool technician = true, ConversationAccessStatus status = ConversationAccessStatus.Ok) =>
        new(ai, new FakeCuentas(technician), new FakeConversaciones(status), new FixedClock(_now));

    [Fact]
    public async Task Turno_Responde_Sugerencia_Sin_Datos_Personales_CA_052_01()
    {
        var ai = new FakeAi();
        var outcome = await Assistant(ai).TurnAsync("sub-1", new TurnoBody("Se tapó el lavabo", null, null), default);
        outcome.Status.Should().Be(AssistantStatus.Ok);
        outcome.Value!.GeneradoPorIa.Should().BeTrue();
        outcome.Value.Mensaje.Should().NotContain("5555 1234").And.Contain("[teléfono]");
        ai.Requests.Single().UsuarioId.Should().Be("sub-1");
    }

    [Fact]
    public async Task Olor_A_Gas_Empieza_Con_Instrucciones_De_Seguridad_CA_052_32()
    {
        var outcome = await Assistant(new FakeAi { ProviderMessage = "Revisa la estufa." }).TurnAsync("sub-1", new TurnoBody("Huele a gas en la cocina", null, null), default);
        outcome.Value!.Mensaje.Should().StartWith(AsistenteSeguridad.GasSafety);
        outcome.Value.Seguridad.Should().NotBeNullOrEmpty();
        var normal = await Assistant(new FakeAi { ProviderMessage = "Revisa la llave." }).TurnAsync("sub-1", new TurnoBody("Gotea la llave", null, null), default);
        normal.Value!.Mensaje.Should().NotContain("911");
    }

    [Theory]
    [InlineData("disabled", "AiUnavailable")]
    [InlineData("limit_unavailable", "AiUnavailable")]
    [InlineData("budget_exhausted", "DailyLimit")]
    [InlineData("unavailable", "Ok")]
    [InlineData("invalid_schema", "Ok")]
    public async Task Sin_IA_503_Tope_429_Y_Plantilla_En_Otros_Casos_CA_052_29_CA_052_30(string reason, string expectedName)
    {
        var outcome = await Assistant(new FakeAi { Reason = reason, Origin = AiOrigin.Template }).TurnAsync("sub-1", new TurnoBody("hola", null, null), default);
        var expected = Enum.Parse<AssistantStatus>(expectedName);
        outcome.Status.Should().Be(expected);
        if (expected == AssistantStatus.DailyLimit)
        {
            outcome.RetryAfterSeconds.Should().Be(12 * 3600);
        }
    }

    [Fact]
    public async Task Resumen_Solo_Tecnico_Participante_Y_Sin_Datos_Personales_CA_052_33()
    {
        var ok = await Assistant(new FakeAi()).SummarizeAsync("sub-t", _solicitud, default);
        ok.Status.Should().Be(AssistantStatus.Ok);
        ok.Value!.Sabe.Should().ContainSingle().Which.Should().NotContain("Hidalgo");
        (await Assistant(new FakeAi(), technician: false).SummarizeAsync("sub-c", _solicitud, default)).Status.Should().Be(AssistantStatus.NotTechnician);
        (await Assistant(new FakeAi(), status: ConversationAccessStatus.NotParticipant).SummarizeAsync("sub-t", _solicitud, default)).Status.Should().Be(AssistantStatus.Forbidden);
        (await Assistant(new FakeAi(), status: ConversationAccessStatus.Unavailable).SummarizeAsync("sub-t", _solicitud, default)).Status.Should().Be(AssistantStatus.AiUnavailable);
    }

    [Fact]
    public async Task Borrador_Es_Sugerencia_Y_No_Se_Guarda_Como_Cotizacion_CA_052_35()
    {
        var ai = new FakeAi();
        var outcome = await Assistant(ai).DraftQuoteAsync("sub-t", _solicitud, default);
        outcome.Value!.Nota.Should().NotContain("5555");
        outcome.Value.GeneradoPorIa.Should().BeTrue();
        typeof(AsistenteService).GetConstructors().Single().GetParameters().Select(p => p.ParameterType.Name).Should().NotContain(["IQuoteWriter", "IBitacoraStore"]);
    }

    [Fact]
    public async Task Rango_Precio_Se_Cachea_Una_Hora_Y_Exige_Tecnico_CA_052_17()
    {
        var reference = new FakeReferencia();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new RangoPrecioService(new FakeCuentas(), reference, cache);
        var first = await service.GetAsync("sub-t", "Plomería", "Fuga", default);
        await service.GetAsync("sub-t", "plomería", "fuga", default);
        first.Value!.Should().Be(new RangoPrecioResponse(300, 600, 12, "cotizaciones_aceptadas"));
        reference.Calls.Should().Be(1);
        (await new RangoPrecioService(new FakeCuentas(false), reference, cache).GetAsync("sub-c", "Plomería", "Fuga", default)).Status.Should().Be(AssistantStatus.NotTechnician);
        using var emptyCache = new MemoryCache(new MemoryCacheOptions());
        (await new RangoPrecioService(new FakeCuentas(), new UnavailableReferenciaMercado(), emptyCache).GetAsync("sub-t", "x", "y", default)).Status.Should().Be(AssistantStatus.ReferenceUnavailable);
    }
}

public sealed class Spec052BitacoraTests
{
    private const string _solicitud = "0123456789abcdef01234567";
    private static readonly DateTimeOffset _now = new(2026, 11, 21, 18, 0, 0, TimeSpan.Zero);
    private static readonly BitacoraBody _body = new(["Fuga"], ["Horario"], ["Empaque"]);

    private static BitacoraService Service(MemoryBitacoras store, bool technician = true, bool participates = true, DateTime? closed = null) =>
        new(new FakeCuentas(technician), new FakeSolicitudes(participates, closed), store, new FixedClock(_now));

    [Fact]
    public async Task Guardar_Crea_201_Y_Reemplaza_200_CA_052_34()
    {
        var store = new MemoryBitacoras();
        var service = Service(store);
        (await service.SaveAsync("sub-t", _solicitud, _body, default)).Status.Should().Be(AssistantStatus.Created);
        (await service.SaveAsync("sub-t", _solicitud, _body with { Sabe = ["Otra"] }, default)).Status.Should().Be(AssistantStatus.Ok);
        (await service.GetAsync("sub-t", _solicitud, default)).Value!.Sabe.Should().Equal("Otra");
    }

    [Fact]
    public async Task Otro_Tecnico_No_La_Ve_CA_052_34()
    {
        var store = new MemoryBitacoras();
        await Service(store).SaveAsync("sub-t", _solicitud, _body, default);
        (await Service(store).GetAsync("sub-otro", _solicitud, default)).Status.Should().Be(AssistantStatus.NotFound);
    }

    [Fact]
    public async Task Sin_Participacion_O_Sin_Perfil_403_CA_052_20()
    {
        var store = new MemoryBitacoras();
        (await Service(store, participates: false).SaveAsync("sub-t", _solicitud, _body, default)).Status.Should().Be(AssistantStatus.Forbidden);
        (await Service(store, technician: false).SaveAsync("sub-c", _solicitud, _body, default)).Status.Should().Be(AssistantStatus.NotTechnician);
        (await Service(store, technician: false).GetAsync("sub-c", _solicitud, default)).Status.Should().Be(AssistantStatus.NotTechnician);
    }

    [Fact]
    public async Task Vence_30_Dias_Despues_Del_Cierre_CA_052_34()
    {
        var store = new MemoryBitacoras();
        var closed = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);
        await Service(store, closed: closed).SaveAsync("sub-t", _solicitud, _body, default);
        (await store.FindAsync(_solicitud, "sub-t", default))!.VenceEn.Should().Be(closed.AddDays(30));
        await Service(new MemoryBitacoras()).SaveAsync("sub-t", _solicitud, _body, default);
    }
}

public sealed class Spec052ValidadoresTests
{
    private static bool Valid<T>(IValidator<T> validator, T value) => validator.Validate(value).IsValid;

    [Theory]
    [InlineData("hola", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void Mensaje_De_1_A_1000_CA_052_03_CA_052_32(string? message, bool expected) => Valid(new TurnoBodyValidator(), new TurnoBody(message, null, null)).Should().Be(expected);

    [Fact]
    public void Mensaje_De_1001_Caracteres_Es_Invalido_CA_052_32()
    {
        Valid(new TurnoBodyValidator(), new TurnoBody(new string('a', 1000), null, null)).Should().BeTrue();
        Valid(new TurnoBodyValidator(), new TurnoBody(new string('a', 1001), null, null)).Should().BeFalse();
        Valid(new TurnoBodyValidator(), new TurnoBody("hola", "https://ejemplo.test/x.jpg", null)).Should().BeFalse();
    }

    [Fact]
    public void Conversacion_Debe_Ser_ObjectId_CA_052_08_CA_052_12()
    {
        Valid(new ConversacionBodyValidator(), new ConversacionBody("0123456789abcdef01234567")).Should().BeTrue();
        Valid(new ConversacionBodyValidator(), new ConversacionBody("no")).Should().BeFalse();
        Valid(new ConversacionBodyValidator(), new ConversacionBody(null)).Should().BeFalse();
    }

    [Fact]
    public void Consulta_De_Rango_Exige_Oficio_Y_Tipo_CA_052_16()
    {
        Valid(new RangoPrecioQueryValidator(), new RangoPrecioQuery { Oficio = "plomeria", TipoTrabajo = "fuga de agua" }).Should().BeTrue();
        Valid(new RangoPrecioQueryValidator(), new RangoPrecioQuery { Oficio = "../x", TipoTrabajo = "fuga" }).Should().BeFalse();
        Valid(new RangoPrecioQueryValidator(), new RangoPrecioQuery { Oficio = "plomeria" }).Should().BeFalse();
    }

    [Fact]
    public void Bitacora_Listas_De_10_Por_200_CA_052_21_CA_052_27()
    {
        var validator = new BitacoraBodyValidator();
        Valid(validator, new BitacoraBody([.. Enumerable.Repeat("a", 10)], [], [])).Should().BeTrue();
        Valid(validator, new BitacoraBody([.. Enumerable.Repeat("a", 11)], [], [])).Should().BeFalse();
        Valid(validator, new BitacoraBody([new string('a', 201)], [], [])).Should().BeFalse();
        Valid(validator, new BitacoraBody(null, [], [])).Should().BeFalse();
        Valid(new SolicitudRouteValidator(), new SolicitudRoute { SolicitudId = "xyz" }).Should().BeFalse();
    }

    [Fact]
    public void Los_Cuerpos_No_Aceptan_Identificadores_De_Personas_CA_052_36()
    {
        foreach (var type in new[] { typeof(TurnoBody), typeof(ConversacionBody), typeof(BitacoraBody) })
        {
            type.GetProperties().Select(p => p.Name).Should().NotContain(["UsuarioId", "TecnicoId", "Sub", "PersonaId"]);
        }
    }
}
