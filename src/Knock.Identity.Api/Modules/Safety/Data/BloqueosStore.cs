using System.Globalization;
using Knock.Contracts.Safety;
using Knock.Hosting.Concurrency;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Hosting.Procesos.Outbox;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Identity.Api.Modules.Safety.Data;

/// <summary>Un bloqueo guardado: quién bloqueó a quién (identificadores públicos) y cuándo; <see cref="RecordId"/> sirve de cursor.</summary>
internal sealed record StoredBlock(ObjectId RecordId, string BlockedId, DateTime CreatedAtUtc);

/// <summary>
/// Colección <c>bloqueos</c> (spec 043, §6). Es el único acceso a MongoDB del módulo. Las dos personas se guardan con su identificador público (el <c>sub</c>), porque el <c>_id</c> interno
/// de <c>usuarios</c> no sale de la base. Toda lectura y escritura lleva el <c>sub</c> de quien llama (R-24). El índice único (bloqueaId, bloqueadoId) impide bloquear dos veces.
/// </summary>
internal sealed class BloqueosStore(
    [FromKeyedServices(DataExtensions.DatabaseKey)] IMongoDatabase database,
    IMongoClient client,
    IOutboxStore outbox,
    IOptions<KnockMongoOptions> mongoOptions)
{
    public const string BlocksCollection = "bloqueos";
    private const string _blockerField = "bloqueaId";
    private const string _blockedField = "bloqueadoId";
    private const string _createdField = "creadoEn";
    private const string _idField = "_id";
    private static readonly string[] _destinations = ["market", "messaging"];

    private readonly FindOptions _findOptions = new() { MaxTime = mongoOptions.Value.QueryMaxTime };

    private IMongoCollection<BsonDocument> Blocks => database.GetCollection<BsonDocument>(BlocksCollection);

    /// <summary>Índices de la spec 043 (§6). Idempotente; lo usan las pruebas y el script de <c>db/</c> hace lo mismo.</summary>
    public Task EnsureIndexesAsync(CancellationToken cancellationToken) =>
        Blocks.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending(_blockerField).Ascending(_blockedField),
                    new CreateIndexOptions { Unique = true, Name = "bloqueaId_1_bloqueadoId_1" }),
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending(_blockedField), new CreateIndexOptions { Name = "bloqueadoId_1" }),
            ],
            cancellationToken);

    /// <summary>
    /// Guarda el bloqueo y deja <c>BloqueoCreado</c> en la bandeja de salida para Mercado y Mensajería, en la misma transacción (spec 043, R-23; spec 054, R-02): o quedan los dos o ninguno.
    /// Falso si ya existía. Dentro de la instancia, los bloqueos del mismo par se turnan (<see cref="KeyedGate"/>) y quien entra después relee: varios <c>insert</c> a la vez sobre el índice único se bloqueaban entre sí
    /// en documentdb-local (código 24) y agotaban los reintentos como 500. Entre instancias decide el índice único, que ante un conflicto de escritura relee el bloqueo y reintenta.
    /// </summary>
    public Task<bool> AddAsync(string blockerId, string blockedId, DateTime nowUtc, CancellationToken cancellationToken) =>
        KeyedGate.RunAsync(blockerId + "|" + blockedId, () => AddSerializedAsync(blockerId, blockedId, nowUtc, cancellationToken), cancellationToken);

    private async Task<bool> AddSerializedAsync(string blockerId, string blockedId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var pair = filters.Eq(_blockerField, blockerId) & filters.Eq(_blockedField, blockedId);
        async Task<bool> ExistsAsync() =>
            await Blocks.Find(pair, _findOptions).Project(Builders<BsonDocument>.Projection.Include(_idField)).FirstOrDefaultAsync(cancellationToken) is not null;

        return !await ExistsAsync()
            && await MongoWriteErrors.InsertIfAbsentAsync(
                async () =>
                {
                    var recordId = ObjectId.GenerateNewId();
                    using var session = await client.StartSessionAsync(cancellationToken: cancellationToken);
                    await session.WithTransactionAsync(
                        async (transaction, token) =>
                        {
                            await Blocks.InsertOneAsync(
                                transaction,
                                new BsonDocument
                                {
                                    [_idField] = recordId,
                                    [_blockerField] = blockerId,
                                    [_blockedField] = blockedId,
                                    [_createdField] = new BsonDateTime(TruncateToMilliseconds(nowUtc)),
                                },
                                cancellationToken: token);
                            await AddEventsAsync(transaction, BlockCreated.EventType, recordId, blockerId, blockedId, nowUtc, token);
                            return true;
                        },
                        cancellationToken: cancellationToken);
                },
                ExistsAsync,
                delay => Task.Delay(delay, cancellationToken));
    }

    /// <summary>Quita el bloqueo de quien llama y deja <c>BloqueoEliminado</c> en la bandeja en la misma transacción; falso si no había.</summary>
    public async Task<bool> RemoveAsync(string blockerId, string blockedId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        using var session = await client.StartSessionAsync(cancellationToken: cancellationToken);
        return await session.WithTransactionAsync(
            async (transaction, token) =>
            {
                var removed = await Blocks.FindOneAndDeleteAsync(
                    transaction,
                    filters.Eq(_blockerField, blockerId) & filters.Eq(_blockedField, blockedId),
                    new FindOneAndDeleteOptions<BsonDocument> { Projection = Builders<BsonDocument>.Projection.Include(_idField) },
                    token);
                if (removed is null)
                {
                    return false;
                }

                await AddEventsAsync(transaction, BlockRemoved.EventType, removed[_idField].AsObjectId, blockerId, blockedId, nowUtc, token);
                return true;
            },
            cancellationToken: cancellationToken);
    }

    private static DateTime TruncateToMilliseconds(DateTime value) => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMillisecond));

    /// <summary>Un evento por servicio destino (Mercado y Mensajería, R-23). El <c>_id</c> sale del bloqueo, así que repetir la operación no duplica; la carga solo lleva identificadores y la fecha.</summary>
    private async Task AddEventsAsync(IClientSessionHandle transaction, string type, ObjectId recordId, string blockerId, string blockedId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        foreach (var destination in _destinations)
        {
            var eventId = OutboxReglas.BuildId(type, $"{recordId}:{destination}", 1);
            var payload = new BsonDocument
            {
                ["eventoId"] = eventId,
                ["bloqueaId"] = blockerId,
                ["bloqueadoId"] = blockedId,
                ["ocurridoEn"] = nowUtc.ToString("O", CultureInfo.InvariantCulture),
            };
            await outbox.AddAsync(transaction, new EventoDeOutbox(eventId, type, destination, payload), nowUtc, cancellationToken);
        }
    }

    /// <summary>
    /// Los bloqueos de quien llama, del más reciente al más antiguo (por <c>_id</c>, que crece con el tiempo). Pide uno de más para saber si hay otra página; el cursor es el
    /// <c>_id</c> del último elemento devuelto.
    /// </summary>
    public async Task<IReadOnlyList<StoredBlock>> ListAsync(string blockerId, ObjectId? after, int take, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var filter = filters.Eq(_blockerField, blockerId);
        if (after is { } cursor)
        {
            filter &= filters.Lt(_idField, cursor);
        }

        var documents = await Blocks.Find(filter, _findOptions).Sort(Builders<BsonDocument>.Sort.Descending(_idField)).Limit(take).ToListAsync(cancellationToken);
        return [.. documents.Select(d => new StoredBlock(d[_idField].AsObjectId, d[_blockedField].AsString, d[_createdField].ToUniversalTime()))];
    }
}
