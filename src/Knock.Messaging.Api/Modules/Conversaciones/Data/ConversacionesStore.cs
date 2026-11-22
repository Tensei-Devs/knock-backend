using Knock.Contracts.Conversations;
using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Conversaciones.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Messaging.Api.Modules.Conversaciones.Data;

/// <summary>Último mensaje guardado en la conversación (texto ya ocultado, recortado a 80 caracteres).</summary>
internal sealed record StoredLastMessage(string Texto, DateTime EnviadoEn, string AutorRol);

/// <summary>
/// Una conversación guardada (spec 030, §6). <see cref="ClienteId"/> y <see cref="TecnicoUsuarioId"/> son los <c>sub</c> de los dos participantes y no salen de este módulo. Los contadores y las
/// marcas de lectura son por parte; <see cref="RoleOf"/> dice cuál es la de quien pide.
/// </summary>
internal sealed record StoredConversation(
    ObjectId Id,
    string SolicitudId,
    string TecnicoId,
    string ClienteId,
    string TecnicoUsuarioId,
    string? VisitaId,
    string Servicio,
    string ColoniaNombre,
    string TecnicoNombre,
    string? TecnicoFotoUrl,
    string Estado,
    string? MotivoCierre,
    bool Elegido,
    StoredLastMessage? UltimoMensaje,
    DateTime UltimoMensajeEn,
    int NoLeidosCliente,
    int NoLeidosTecnico,
    DateTime? LeidoHastaCliente,
    DateTime? LeidoHastaTecnico,
    DateTime CreadoEn)
{
    /// <summary>El rol de <paramref name="subject"/> en la conversación, o <see langword="null"/> si no participa.</summary>
    public string? RoleOf(string subject)
    {
        if (string.Equals(subject, ClienteId, StringComparison.Ordinal))
        {
            return ConversacionRoles.Cliente;
        }

        return string.Equals(subject, TecnicoUsuarioId, StringComparison.Ordinal) ? ConversacionRoles.Tecnico : null;
    }

    public int UnreadFor(string role) => role == ConversacionRoles.Cliente ? NoLeidosCliente : NoLeidosTecnico;

    public DateTime? ReadUntilFor(string role) => role == ConversacionRoles.Cliente ? LeidoHastaCliente : LeidoHastaTecnico;
}

/// <summary>
/// Colección <c>conversaciones</c> (spec 030, §6). Es el único acceso a MongoDB del módulo para las conversaciones (constitución, principio 10). Toda lectura por identificador lleva el
/// <c>sub</c> de quien pide en el filtro: una conversación ajena y una inexistente son lo mismo (R-05).
/// </summary>
internal sealed class ConversacionesStore(
    [FromKeyedServices(DataExtensions.DatabaseKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> mongoOptions)
{
    public const string ConversationsCollection = "conversaciones";
    public const string PairIndexName = "solicitudId_1_tecnicoId_1";
    public const string ClientListIndexName = "clienteId_1_ultimoMensajeEn_-1__id_-1";
    public const string TechnicianListIndexName = "tecnicoUsuarioId_1_ultimoMensajeEn_-1__id_-1";

    private const string _idField = "_id";
    private const string _requestField = "solicitudId";
    private const string _technicianField = "tecnicoId";
    private const string _clientField = "clienteId";
    private const string _technicianUserField = "tecnicoUsuarioId";
    private const string _lastAtField = "ultimoMensajeEn";
    private const string _unreadClientField = "noLeidosCliente";
    private const string _unreadTechnicianField = "noLeidosTecnico";
    private const string _readUntilClientField = "leidoHastaCliente";
    private const string _readUntilTechnicianField = "leidoHastaTecnico";
    private const int _duplicateKey = 11000;
    private const int _writeConflict = 112;
    private const int _lockError = 24;
    private const int _maxInsertAttempts = 6;
    private const int _insertBackoffMilliseconds = 50;

    private readonly FindOptions _findOptions = new() { MaxTime = mongoOptions.Value.QueryMaxTime };

    private IMongoCollection<BsonDocument> Conversations => database.GetCollection<BsonDocument>(ConversationsCollection);

    /// <summary>Índices de la spec 030 (§6), con los nombres de <c>db/030-conversaciones.js</c>. Idempotente; lo usan las pruebas, y el script de <c>db/</c> hace lo mismo.</summary>
    public Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var keys = Builders<BsonDocument>.IndexKeys;
        return Conversations.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<BsonDocument>(keys.Ascending(_requestField).Ascending(_technicianField), new CreateIndexOptions { Unique = true, Name = PairIndexName }),
                new CreateIndexModel<BsonDocument>(keys.Ascending(_clientField).Descending(_lastAtField).Descending(_idField), new CreateIndexOptions { Name = ClientListIndexName }),
                new CreateIndexModel<BsonDocument>(keys.Ascending(_technicianUserField).Descending(_lastAtField).Descending(_idField), new CreateIndexOptions { Name = TechnicianListIndexName }),
            ],
            cancellationToken);
    }

    /// <summary>La conversación del par (solicitud, técnico), o <see langword="null"/>.</summary>
    public async Task<StoredConversation?> FindByPairAsync(string requestId, string technicianId, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var document = await Conversations.Find(filters.Eq(_requestField, requestId) & filters.Eq(_technicianField, technicianId), _findOptions).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : Read(document);
    }

    /// <summary>La conversación solo si <paramref name="subject"/> es una de sus dos partes; si no, <see langword="null"/> (404 uniforme).</summary>
    public async Task<StoredConversation?> FindForParticipantAsync(ObjectId id, string subject, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var filter = filters.Eq(_idField, id) & (filters.Eq(_clientField, subject) | filters.Eq(_technicianUserField, subject));
        var document = await Conversations.Find(filter, _findOptions).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : Read(document);
    }

    /// <summary>
    /// Inserta la conversación; falso si el par ya existía. Causa raíz de CA-030-15 intermitente: varios <c>insert</c> a la vez sobre el mismo índice único del par se bloquean entre sí en
    /// documentdb-local («Could not acquire lock ... due to deadlock», código 24) y el error llegaba como 500. Dentro de la instancia, las creaciones de un mismo par se turnan (candado por
    /// franja del par) y quien entra después relee: si ya existe responde «ya existía» sin escribir. Entre instancias valen el índice único, la relectura y el reintento ante conflicto.
    /// </summary>
    public async Task<bool> InsertAsync(StoredConversation conversation, CancellationToken cancellationToken)
    {
        var gate = _gates[(uint)StringComparer.Ordinal.GetHashCode(conversation.SolicitudId + "|" + conversation.TecnicoId) % _gates.Length];
        await gate.WaitAsync(cancellationToken);
        try
        {
            var attempt = 0;
            while (true)
            {
                attempt++;
                if (await FindByPairAsync(conversation.SolicitudId, conversation.TecnicoId, cancellationToken) is not null)
                {
                    return false;
                }

                try
                {
                    await Conversations.InsertOneAsync(Write(conversation), cancellationToken: cancellationToken);
                    return true;
                }
                catch (MongoException exception) when (IsDuplicate(exception))
                {
                    return false;
                }
                catch (MongoException exception) when (IsRetriable(exception) && attempt < _maxInsertAttempts)
                {
                    await Task.Delay(_insertBackoffMilliseconds * attempt, cancellationToken);
                }
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static bool IsDuplicate(MongoException exception) =>
        exception is MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey } or MongoCommandException { Code: _duplicateKey };

    /// <summary>Conflicto de escritura (112) o interbloqueo de documentdb-local (24, solo si el mensaje dice «deadlock»): la escritura no se aplicó y se puede repetir.</summary>
    private static bool IsRetriable(MongoException exception) =>
        exception is MongoWriteException { WriteError.Code: _writeConflict }
        or MongoCommandException { Code: _writeConflict }
        or MongoCommandException { Code: _lockError } && exception.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase);

    private static readonly SemaphoreSlim[] _gates = [.. Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1))];

    /// <summary>Guarda lo que Mercado dijo del permiso (R-09): estado, motivo de cierre, si es el elegido y la visita.</summary>
    public async Task RefreshPermissionAsync(ObjectId id, PermisoConversacion permission, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var update = Builders<BsonDocument>.Update
            .Set("estado", permission.Abierta ? ConversacionEstados.Abierta : ConversacionEstados.Cerrada)
            .Set("motivoCierre", ToBson(permission.MotivoCierre))
            .Set("elegido", permission.Elegido)
            .Set("visitaId", ToBson(permission.VisitaId))
            .Set("actualizadoEn", nowUtc);
        await Conversations.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq(_idField, id), update, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Las conversaciones de <paramref name="subject"/> (como cliente o como técnico) de la más reciente a la más antigua. Cada rol usa su propio índice y se mezclan en memoria: un solo
    /// <c>$or</c> ordenado no puede usar los dos índices a la vez. Pide <paramref name="take"/> de cada lado y devuelve los primeros <paramref name="take"/> del conjunto.
    /// </summary>
    public async Task<IReadOnlyList<StoredConversation>> ListAsync(string subject, PageCursor? after, int take, CancellationToken cancellationToken)
    {
        var asClient = await ListByAsync(_clientField, subject, after, take, cancellationToken);
        var asTechnician = await ListByAsync(_technicianUserField, subject, after, take, cancellationToken);
        return [.. asClient.Concat(asTechnician)
            .OrderByDescending(c => c.UltimoMensajeEn)
            .ThenByDescending(c => c.Id)
            .Take(take)];
    }

    /// <summary>Suma de los no leídos de <paramref name="subject"/> con los contadores guardados (R-18), sin contar mensajes.</summary>
    public async Task<int> TotalUnreadAsync(string subject, CancellationToken cancellationToken)
    {
        var asClient = await SumAsync(_clientField, _unreadClientField, subject, cancellationToken);
        var asTechnician = await SumAsync(_technicianUserField, _unreadTechnicianField, subject, cancellationToken);
        return checked((int)(asClient + asTechnician));
    }

    /// <summary>
    /// Mueve la marca de lectura de <paramref name="role"/> a <paramref name="readUntil"/> y fija su contador, solo si el último mensaje sigue siendo <paramref name="lastMessageAt"/>: si llegó
    /// otro mientras se contaba, devuelve falso y el servicio vuelve a contar (el contador no se pisa con un valor viejo).
    /// </summary>
    public async Task<bool> ApplyReadAsync(ObjectId id, string role, DateTime readUntil, int unread, DateTime lastMessageAt, CancellationToken cancellationToken)
    {
        var client = role == ConversacionRoles.Cliente;
        var filters = Builders<BsonDocument>.Filter;
        var update = Builders<BsonDocument>.Update
            .Set(client ? _readUntilClientField : _readUntilTechnicianField, readUntil)
            .Set(client ? _unreadClientField : _unreadTechnicianField, unread);
        var result = await Conversations.UpdateOneAsync(filters.Eq(_idField, id) & filters.Eq(_lastAtField, lastMessageAt), update, cancellationToken: cancellationToken);
        return result.MatchedCount > 0;
    }

    private async Task<List<StoredConversation>> ListByAsync(string participantField, string subject, PageCursor? after, int take, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var filter = filters.Eq(participantField, subject);
        if (after is { } cursor)
        {
            filter &= filters.Lt(_lastAtField, cursor.Date) | (filters.Eq(_lastAtField, cursor.Date) & filters.Lt(_idField, cursor.Id));
        }

        var documents = await Conversations.Find(filter, _findOptions)
            .Sort(Builders<BsonDocument>.Sort.Descending(_lastAtField).Descending(_idField))
            .Limit(take)
            .ToListAsync(cancellationToken);
        return [.. documents.Select(Read)];
    }

    private async Task<long> SumAsync(string participantField, string counterField, string subject, CancellationToken cancellationToken)
    {
        var groups = await Conversations.Aggregate(new AggregateOptions { MaxTime = mongoOptions.Value.QueryMaxTime })
            .Match(Builders<BsonDocument>.Filter.Eq(participantField, subject))
            .Group(new BsonDocument { [_idField] = BsonNull.Value, ["total"] = new BsonDocument("$sum", "$" + counterField) })
            .ToListAsync(cancellationToken);
        return groups.Count == 0 ? 0 : groups[0]["total"].ToInt64();
    }

    private static StoredConversation Read(BsonDocument document)
    {
        var technician = document["tecnicoPublico"].AsBsonDocument;
        var last = document.GetValue("ultimoMensaje", BsonNull.Value);
        return new StoredConversation(
            document[_idField].AsObjectId,
            document[_requestField].AsString,
            document[_technicianField].AsString,
            document[_clientField].AsString,
            document[_technicianUserField].AsString,
            OptionalString(document, "visitaId"),
            document["servicio"].AsString,
            document["coloniaNombre"].AsString,
            technician["nombre"].AsString,
            OptionalString(technician, "fotoUrl"),
            document["estado"].AsString,
            OptionalString(document, "motivoCierre"),
            document["elegido"].AsBoolean,
            last.IsBsonDocument ? new StoredLastMessage(last["texto"].AsString, last["enviadoEn"].ToUniversalTime(), last["autorRol"].AsString) : null,
            document[_lastAtField].ToUniversalTime(),
            document[_unreadClientField].ToInt32(),
            document[_unreadTechnicianField].ToInt32(),
            OptionalDate(document, _readUntilClientField),
            OptionalDate(document, _readUntilTechnicianField),
            document["creadoEn"].ToUniversalTime());
    }

    private static BsonDocument Write(StoredConversation c) => new()
    {
        [_idField] = c.Id,
        [_requestField] = c.SolicitudId,
        [_technicianField] = c.TecnicoId,
        [_clientField] = c.ClienteId,
        [_technicianUserField] = c.TecnicoUsuarioId,
        ["visitaId"] = ToBson(c.VisitaId),
        ["servicio"] = c.Servicio,
        ["coloniaNombre"] = c.ColoniaNombre,
        ["tecnicoPublico"] = new BsonDocument { ["nombre"] = c.TecnicoNombre, ["fotoUrl"] = ToBson(c.TecnicoFotoUrl) },
        ["estado"] = c.Estado,
        ["motivoCierre"] = ToBson(c.MotivoCierre),
        ["elegido"] = c.Elegido,
        [_lastAtField] = c.UltimoMensajeEn,
        [_unreadClientField] = c.NoLeidosCliente,
        [_unreadTechnicianField] = c.NoLeidosTecnico,
        ["creadoEn"] = c.CreadoEn,
        ["actualizadoEn"] = c.CreadoEn,
    };

    private static BsonValue ToBson(string? value) => value is null ? BsonNull.Value : value;

    private static string? OptionalString(BsonDocument document, string field) =>
        document.TryGetValue(field, out var value) && value.IsString ? value.AsString : null;

    private static DateTime? OptionalDate(BsonDocument document, string field) =>
        document.TryGetValue(field, out var value) && value.IsValidDateTime ? value.ToUniversalTime() : null;
}
