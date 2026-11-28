extern alias MarketApi;

using System.Collections.Concurrent;
using MarketApi::Knock.Market.Api.Modules.Assistant.Data;
using MarketApi::Knock.Market.Api.Modules.Assistant.Services;

namespace Knock.IntegrationTest.Assistant;

/// <summary>Cliente de IA de pruebas de la spec 052: devuelve valores cerrados fijos, o el respaldo que se indique (<see cref="Reason"/>), y guarda lo que recibió.</summary>
internal sealed class DobleIa : IAiClient
{
    public string? Reason { get; set; }

    public ConcurrentQueue<SolicitudIa> Requests { get; } = new();

    public Task<ResultadoIa<T>> GenerarAsync<T>(SolicitudIa solicitud, CancellationToken cancellationToken)
        where T : class, IAiOutput<T>
    {
        Requests.Enqueue(solicitud);
        object value = solicitud.Funcion switch
        {
            AiFunction.AsistenteKnock => new AsistenteKnockSalida { Categoria = "Plomería", Mensaje = "Cierra la llave de paso.", PideFoto = true, Seguridad = null },
            AiFunction.ResumenChat => new ResumenChatSalida { Sabe = ["Gotea la llave"], PorConfirmar = ["Horario"], QueLlevar = ["Empaque"] },
            _ => new BorradorCotizacionSalida { PrecioSugerido = 350, TiempoEstimado = "1 hora", Dia = "mañana", Nota = "Incluye mano de obra", Explicacion = "Según la conversación" },
        };
        return Task.FromResult(new ResultadoIa<T>((T?)value, AiOrigin.Provider, Reason, [], false, 1));
    }
}

internal sealed class DobleCuentas(bool technician) : IAsistenteCuentas
{
    public Task<bool> IsTechnicianAsync(string sub, CancellationToken cancellationToken) => Task.FromResult(technician);
}

internal sealed class DobleConversaciones : IConversacionesAsistente
{
    public Task<ConversationAccess> GetForTechnicianAsync(string conversationId, string sub, CancellationToken cancellationToken) =>
        Task.FromResult(new ConversationAccess(ConversationAccessStatus.Ok, "Cliente: se rompió el grifo"));
}

internal sealed class DobleSolicitudes : ISolicitudesAsistente
{
    public Task<RequestAccess> GetAccessAsync(string solicitudId, string sub, CancellationToken cancellationToken) => Task.FromResult(new RequestAccess(true, null));

    public Task<IReadOnlyDictionary<string, DateTime>> GetClosedAtAsync(IReadOnlyCollection<string> solicitudIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, DateTime>>(new Dictionary<string, DateTime>());
}

internal sealed class DobleReferencia : IReferenciaMercado
{
    public Task<PriceRange?> GetRangeAsync(string oficio, string tipoTrabajo, CancellationToken cancellationToken) =>
        Task.FromResult<PriceRange?>(new PriceRange(300, 600, 12, "cotizaciones_aceptadas"));
}

/// <summary>Bitácoras en memoria (solo para el canal HTTP sin contenedores; la prueba con MongoDB real vive en <c>Spec052BitacoraMongoTests</c>).</summary>
internal sealed class DobleBitacoras : IBitacoraStore
{
    private readonly ConcurrentDictionary<string, BitacoraRecord> _logs = new();

    public Task<bool> UpsertAsync(BitacoraRecord log, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);
        var key = log.SolicitudId + "|" + log.TecnicoId;
        var created = _logs.TryAdd(key, log);
        _logs[key] = log;
        return Task.FromResult(created);
    }

    public Task<BitacoraRecord?> FindAsync(string solicitudId, string tecnicoId, CancellationToken cancellationToken) =>
        Task.FromResult(_logs.TryGetValue(solicitudId + "|" + tecnicoId, out var log) ? log : null);

    public Task<IReadOnlyList<BitacoraSinFecha>> ListWithoutExpiryAsync(string? afterId, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BitacoraSinFecha>>([]);

    public Task SetExpiryAsync(string solicitudId, DateTime venceEn, CancellationToken cancellationToken) => Task.CompletedTask;
}
