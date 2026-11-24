using Knock.Hosting.Data;
using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Conversaciones.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Messaging.Api.Modules.Conversaciones.Data;

/// <summary>Un mensaje guardado. <c>autorId</c> (el <c>sub</c>) existe en la colección pero nunca se lee fuera de ella (R-16).</summary>
internal sealed record StoredMessage(ObjectId Id, string MensajeId, string AutorRol, string Texto, bool ContactoOculto, DateTime CreadoEn);

/// <summary>Un mensaje por guardar (spec 030, R-14). <c>Texto</c> ya viene ocultado y moderado; <c>AutorId</c> es el <c>sub</c> y nunca sale de la base.</summary>
internal sealed record NuevoMensaje(
    string ConversacionId,
    string MensajeId,
    string AutorRol,
    string AutorId,
    string Texto,
    bool ContactoOculto,
    DateTime CreadoEnUtc);

/// <summary>Contadores de la otra parte tras guardar: los del evento <c>noLeidos</c> (R-14). <c>DestinatarioUsuarioId</c> es su <c>sub</c>.</summary>
internal sealed record MensajeGuardado(string DestinatarioUsuarioId, int NoLeidos, int TotalNoLeidos);

/// <summary>Acceso a MongoDB para enviar un mensaje (spec 030, R-09 y R-14): leer los participantes, guardar el permiso vigente y la transacción de guardado.</summary>
internal interface IMensajesStore
{
    /// <summary>La conversación con ese identificador, o <see langword="null"/> si no existe o el identificador no es un <c>ObjectId</c>.</summary>
    Task<ConversacionParaEnvio?> ObtenerAsync(string conversacionId, CancellationToken cancellationToken);

    /// <summary>Actualiza <c>estado</c>, <c>motivoCierre</c>, <c>elegido</c> y <c>visitaId</c> con el permiso recién consultado (R-09).</summary>
    Task GuardarPermisoAsync(string conversacionId, PermisoVigente permiso, DateTime ahoraUtc, CancellationToken cancellationToken);

    /// <summary>Inserta el mensaje, actualiza la conversación y escribe el buzón en una sola transacción (R-14).</summary>
    Task<MensajeGuardado> GuardarAsync(NuevoMensaje mensaje, CancellationToken cancellationToken);
}

/// <summary>
/// Colección <c>mensajes</c> (spec 030, §6). Es el único acceso a MongoDB del módulo para los mensajes. Lectura del historial por cursor y conteo de no leídos; y la escritura transaccional del envío (T-06): mensaje, conversación y buzón en una transacción.
/// </summary>
internal sealed class MensajesStore(
    [FromKeyedServices(DataExtensions.DatabaseKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> mongoOptions) : IMensajesStore
{
    public const string MessagesCollection = "mensajes";
    public const string ConversacionesCollection = "conversaciones";
    public const string OutboxCollection = "outbox";
    public const string MensajeNuevoType = "MensajeNuevo";
    public const string ConversationIndexName = "conversacionId_1_creadoEn_-1__id_-1";
    public const string RetentionIndexName = "creadoEn_1";

    /// <summary>Retención de 180 días desde la creación (R-21), en segundos: la misma cifra que <c>db/030-conversaciones.js</c>.</summary>
    public const int RetentionSeconds = 15_552_000;

    private const string _idField = "_id";
    private const string _conversationField = "conversacionId";
    private const string _createdField = "creadoEn";
    private const string _authorRoleField = "autorRol";

    private readonly FindOptions _findOptions = new() { MaxTime = mongoOptions.Value.QueryMaxTime };

    private IMongoCollection<BsonDocument> Messages => database.GetCollection<BsonDocument>(MessagesCollection);

    /// <summary>Índices de la spec 030 (§6), con los nombres de <c>db/030-conversaciones.js</c>. Idempotente; lo usan las pruebas, y el script de <c>db/</c> hace lo mismo.</summary>
    public Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var keys = Builders<BsonDocument>.IndexKeys;
        return Messages.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<BsonDocument>(keys.Ascending(_conversationField).Descending(_createdField).Descending(_idField), new CreateIndexOptions { Name = ConversationIndexName }),
                new CreateIndexModel<BsonDocument>(keys.Ascending(_createdField), new CreateIndexOptions { Name = RetentionIndexName, ExpireAfter = TimeSpan.FromSeconds(RetentionSeconds) }),
            ],
            cancellationToken);
    }

    /// <summary>Los mensajes de la conversación del más reciente al más antiguo, empezando después del cursor. Solo trae lo que sale en la respuesta (sin <c>autorId</c>).</summary>
    public async Task<IReadOnlyList<StoredMessage>> ListAsync(ObjectId conversationId, PageCursor? after, int take, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var filter = filters.Eq(_conversationField, conversationId);
        if (after is { } cursor)
        {
            filter &= filters.Lt(_createdField, cursor.Date) | (filters.Eq(_createdField, cursor.Date) & filters.Lt(_idField, cursor.Id));
        }

        var documents = await Messages.Find(filter, _findOptions)
            .Project(Builders<BsonDocument>.Projection.Exclude("autorId"))
            .Sort(Builders<BsonDocument>.Sort.Descending(_createdField).Descending(_idField))
            .Limit(take)
            .ToListAsync(cancellationToken);
        return [.. documents.Select(d => new StoredMessage(
            d[_idField].AsObjectId,
            d["mensajeId"].AsString,
            d[_authorRoleField].AsString,
            d["texto"].AsString,
            d["contactoOculto"].AsBoolean,
            d[_createdField].ToUniversalTime()))];
    }

    /// <summary>Cuántos mensajes de la otra parte (no escritos por <paramref name="role"/>) son posteriores a <paramref name="readUntil"/> (R-19).</summary>
    public async Task<int> CountUnreadAsync(ObjectId conversationId, string role, DateTime? readUntil, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var filter = filters.Eq(_conversationField, conversationId) & filters.Ne(_authorRoleField, role);
        if (readUntil is { } mark)
        {
            filter &= filters.Gt(_createdField, mark);
        }

        return checked((int)await Messages.CountDocumentsAsync(filter, new CountOptions { MaxTime = mongoOptions.Value.QueryMaxTime }, cancellationToken));
    }

    private IMongoCollection<BsonDocument> Conversaciones => database.GetCollection<BsonDocument>(ConversacionesCollection);

    public async Task<ConversacionParaEnvio?> ObtenerAsync(string conversacionId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(conversacionId, out var id))
        {
            return null;
        }

        var document = await Conversaciones.Find(Builders<BsonDocument>.Filter.Eq("_id", id), _findOptions)
            .FirstOrDefaultAsync(cancellationToken);
        return document is null
            ? null
            : new ConversacionParaEnvio(
                conversacionId,
                document["solicitudId"].AsString,
                document["tecnicoId"].AsString,
                document["clienteId"].AsString,
                document["tecnicoUsuarioId"].AsString);
    }

    public async Task GuardarPermisoAsync(string conversacionId, PermisoVigente permiso, DateTime ahoraUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(permiso);
        var update = Builders<BsonDocument>.Update
            .Set("estado", permiso.Abierta ? "abierta" : "cerrada")
            .Set("motivoCierre", OrNull(permiso.MotivoCierre))
            .Set("elegido", permiso.Elegido)
            .Set("visitaId", OrNull(permiso.VisitaId))
            .Set("actualizadoEn", ahoraUtc);
        await Conversaciones.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(conversacionId)), update, cancellationToken: cancellationToken);
    }

    public async Task<MensajeGuardado> GuardarAsync(NuevoMensaje mensaje, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        var conversationId = ObjectId.Parse(mensaje.ConversacionId);
        var fromClient = string.Equals(mensaje.AutorRol, ReglasDeMensaje.RolCliente, StringComparison.Ordinal);
        var unreadField = fromClient ? "noLeidosTecnico" : "noLeidosCliente";
        var recipientField = fromClient ? "tecnicoUsuarioId" : "clienteId";

        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        return await session.WithTransactionAsync(async (transaction, token) =>
        {
            await Messages.InsertOneAsync(
                transaction,
                new BsonDocument
                {
                    { "mensajeId", mensaje.MensajeId },
                    { "conversacionId", conversationId },
                    { "autorRol", mensaje.AutorRol },
                    { "autorId", mensaje.AutorId },
                    { "texto", mensaje.Texto },
                    { "contactoOculto", mensaje.ContactoOculto },
                    { "creadoEn", mensaje.CreadoEnUtc },
                },
                cancellationToken: token);

            var update = Builders<BsonDocument>.Update
                .Set("ultimoMensaje", new BsonDocument { { "texto", ReglasDeMensaje.Resumir(mensaje.Texto) }, { "enviadoEn", mensaje.CreadoEnUtc }, { "autorRol", mensaje.AutorRol } })
                .Set("ultimoMensajeEn", mensaje.CreadoEnUtc)
                .Set("actualizadoEn", mensaje.CreadoEnUtc)
                .Inc(unreadField, 1);
            var conversation = await Conversaciones.FindOneAndUpdateAsync(
                transaction,
                Builders<BsonDocument>.Filter.Eq("_id", conversationId),
                update,
                new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
                token);
            var recipient = conversation[recipientField].AsString;

            await database.GetCollection<BsonDocument>(OutboxCollection).InsertOneAsync(
                transaction,
                new BsonDocument
                {
                    { "_id", $"{MensajeNuevoType}:{mensaje.MensajeId}" },
                    { "tipo", MensajeNuevoType },
                    { "agregadoId", mensaje.ConversacionId },
                    { "datos", new BsonDocument { { "conversacionId", mensaje.ConversacionId }, { "destinatarioUsuarioId", recipient } } },
                    { "estado", "pendiente" },
                    { "intentos", 0 },
                    { "creadoEn", mensaje.CreadoEnUtc },
                },
                cancellationToken: token);

            return new MensajeGuardado(recipient, conversation[unreadField].ToInt32(), await TotalAsync(transaction, recipient, token));
        }, cancellationToken: cancellationToken);
    }

    private static BsonValue OrNull(string? value) => value is null ? BsonNull.Value : new BsonString(value);

    /// <summary>La suma de los contadores guardados de la persona en todas sus conversaciones (como cliente y como técnico), sin contar mensajes.</summary>
    private async Task<int> TotalAsync(IClientSessionHandle session, string sub, CancellationToken cancellationToken)
    {
        var asClient = await SumAsync(session, "clienteId", "noLeidosCliente", sub, cancellationToken);
        var asTechnician = await SumAsync(session, "tecnicoUsuarioId", "noLeidosTecnico", sub, cancellationToken);
        return asClient + asTechnician;
    }

    private async Task<int> SumAsync(IClientSessionHandle session, string participantField, string unreadField, string sub, CancellationToken cancellationToken)
    {
        var rows = await Conversaciones.Aggregate(session)
            .Match(Builders<BsonDocument>.Filter.Eq(participantField, sub))
            .Group(new BsonDocument { { "_id", BsonNull.Value }, { "total", new BsonDocument("$sum", "$" + unreadField) } })
            .ToListAsync(cancellationToken);
        return rows.Count == 0 ? 0 : rows[0]["total"].ToInt32();
    }
}
