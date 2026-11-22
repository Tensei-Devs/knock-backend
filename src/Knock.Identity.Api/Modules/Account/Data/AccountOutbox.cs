using Knock.Identity.Api.Hosting.Procesos.Outbox;
using Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;
using Knock.Identity.Api.Modules.Account.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Identity.Api.Modules.Account.Data;

/// <summary>
/// Implementación real de <see cref="IAccountOutbox"/> sobre la bandeja de salida y el ejecutor de tareas de la spec 054. Cada escritura es idempotente: el <c>_id</c> del
/// evento y la pareja <c>tipo + claveNegocio</c> de la tarea son deterministas, así que un reintento no duplica nada.
/// </summary>
internal sealed class AccountOutbox(IOutboxStore outbox, ITareasProgramadasStore tasks, IMongoClient client, TimeProvider clock) : IAccountOutbox
{
    public const string DeletionTask = "EliminarCuenta";
    public const string ExportTask = "PrepararExportacion";
    public const string RecoveryExpiryTask = "VencerRecuperacion";
    private const string _sessionRevoked = "sesion_revocada";
    private const string _accountDeleted = "CuentaEliminada";

    public async Task EnqueueSessionRevokedAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        await AddEventAsync(_sessionRevoked, "messaging", "sid", sessionId, cancellationToken);
    }

    public async Task EnqueueAccountDeletedAsync(string accountSub, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSub);
        await AddEventAsync(_accountDeleted, "market", "sub", accountSub, cancellationToken);
    }

    public async Task EnqueueExportAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var now = clock.GetUtcNow().UtcDateTime;
        // Una exportación en curso por persona (R-22) lo impide la marca de la cuenta; la clave lleva el instante para que una exportación posterior no choque con la ya hecha.
        await tasks.ScheduleAsync(null, ExportTask, $"{userId}:{new DateTimeOffset(now, TimeSpan.Zero).ToUnixTimeSeconds()}", CatalogoDeTareas.ProgramadaPara(ExportTask, now), now, cancellationToken);
    }

    public async Task EnqueueRecoveryExpiryAsync(string userId, DateTime createdAt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var now = clock.GetUtcNow().UtcDateTime;
        await tasks.ScheduleAsync(null, RecoveryExpiryTask, RecoveryExpiryKey(userId, createdAt), CatalogoDeTareas.ProgramadaPara(RecoveryExpiryTask, createdAt), now, cancellationToken);
    }

    /// <summary>Clave de negocio de la tarea: <c>{usuarioId}:{creado en segundos Unix}</c>. Solo identificadores; un caso nuevo tras vencer el anterior tiene otra clave.</summary>
    public static string RecoveryExpiryKey(string userId, DateTime createdAt) => $"{userId}:{new DateTimeOffset(createdAt, TimeSpan.Zero).ToUnixTimeSeconds()}";

    public async Task EnqueueDeletionAsync(string accountSub, DateTime deleteAt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSub);
        var now = clock.GetUtcNow().UtcDateTime;
        await tasks.ScheduleAsync(null, DeletionTask, DeletionKey(accountSub, deleteAt), deleteAt, now, cancellationToken);
    }

    /// <summary>Clave de negocio de la tarea: <c>{sub}:{eliminarEn en segundos Unix}</c>. Solo identificadores; permite distinguir una programación nueva tras cancelar.</summary>
    public static string DeletionKey(string sub, DateTime deleteAt) => $"{sub}:{new DateTimeOffset(deleteAt, TimeSpan.Zero).ToUnixTimeSeconds()}";

    /// <summary>Escribe un evento cuyo <c>_id</c> es <c>{tipo}:{id}:1</c> y cuya carga es solo ese identificador.</summary>
    private async Task AddEventAsync(string type, string destination, string field, string id, CancellationToken cancellationToken)
    {
        var evento = new EventoDeOutbox(OutboxReglas.BuildId(type, id, 1), type, destination, new BsonDocument(field, id));
        using var session = await client.StartSessionAsync(cancellationToken: cancellationToken);
        await outbox.AddAsync(session, evento, clock.GetUtcNow().UtcDateTime, cancellationToken);
    }
}
