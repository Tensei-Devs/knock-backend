using Knock.Hosting.Data;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Requests.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Requests.Data;

/// <summary>Solicitud tal como se guarda (spec 020, §6). <c>OficioNombre</c> se copia del catálogo junto con la clave para devolverlo sin otra consulta.</summary>
internal sealed record RequestRecord(
    ObjectId Id,
    string ClienteId,
    string OficioClave,
    string OficioNombre,
    string Modalidad,
    string Descripcion,
    IReadOnlyList<string> Fotos,
    string Dia,
    string Franja,
    string ColoniaId,
    string ColoniaNombre,
    string ColoniaCp,
    string? DireccionCifrada,
    RequestState Estado,
    DateTime VenceEnUtc,
    DateTime CreadoEnUtc,
    string? ElementoId = null);

/// <summary>Una página de solicitudes propias y el cursor de la siguiente, o <see langword="null"/> en la última.</summary>
internal sealed record RequestRecordPage(IReadOnlyList<RequestRecord> Items, string? NextCursor);

/// <summary>
/// Colecciones <c>solicitudes</c> y <c>cotizaciones</c> de la base del módulo (<c>Mongo:Databases:Requests</c>). Es el único punto del módulo con acceso a MongoDB para
/// crear y leer solicitudes (constitución, principio 10). Los filtros son tipados y cada consulta lleva <c>maxTimeMS</c>.
/// </summary>
internal sealed class RequestsStore(
    [FromKeyedServices(RequestsModuleExtensions.ModuleKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> mongoOptions,
    IOutbox outbox,
    IOptions<OleadasOptions>? waves = null)
{
    public const string RequestsCollection = "solicitudes";
    public const string QuotesCollection = "cotizaciones";

    private static readonly string[] _liveQuoteStates = ["enviada", "aceptada"];

    private readonly FindOptions _findOptions = new() { MaxTime = mongoOptions.Value.QueryMaxTime };

    private IMongoCollection<BsonDocument> Requests => database.GetCollection<BsonDocument>(RequestsCollection);

    private IMongoCollection<BsonDocument> Quotes => database.GetCollection<BsonDocument>(QuotesCollection);

    /// <summary>Guarda la solicitud y su evento en la misma transacción: o quedan las dos cosas o ninguna.</summary>
    public async Task InsertAsync(RequestRecord record, OutboxMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        await session.WithTransactionAsync(
            async (transaction, token) =>
            {
                await Requests.InsertOneAsync(transaction, ToDocument(record), cancellationToken: token);
                await outbox.AddAsync(transaction, message, token);
                // Spec 029, R-08: la tarea de vencimiento nace en la misma transacción que la solicitud.
                await new TareasProgramadasStore(database).ScheduleByKeyAsync(transaction, "VencerSolicitud", record.Id.ToString(), record.VenceEnUtc, record.CreadoEnUtc, token);
                if (waves?.Value.Habilitado == true)
                {
                    await new TareasProgramadasStore(database).ScheduleAsync(transaction, "RepartirOleada", record.Id, 1, record.CreadoEnUtc, record.CreadoEnUtc, token);
                }
                return true;
            },
            cancellationToken: cancellationToken);
    }

    public async Task<RequestRecord?> FindAsync(ObjectId id, CancellationToken cancellationToken)
    {
        var document = await Requests.Find(Builders<BsonDocument>.Filter.Eq("_id", id), _findOptions).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToRecord(document);
    }

    /// <summary>
    /// Solicitudes del cliente (spec 020, R-09 enmendada). Sin <paramref name="estado"/>: primero las activas y luego las terminadas, cada grupo de la más reciente a la más
    /// antigua (<c>creadoEn</c> y <c>_id</c> descendentes), con dos lecturas acotadas por grupo; con <paramref name="estado"/>, una sola lectura. Pide una fila de más para saber
    /// si hay otra página y arma el cursor (grupo, <c>creadoEn</c>, <c>_id</c>) con la última fila devuelta.
    /// </summary>
    public async Task<RequestRecordPage> ListAsync(string clienteId, string? estado, (int Group, DateTime CreatedAtUtc, ObjectId Id)? after, int limit, CancellationToken cancellationToken)
    {
        var documents = new List<BsonDocument>();
        if (estado is not null)
        {
            documents.AddRange(await ReadAsync(clienteId, [estado], after, limit + 1, cancellationToken));
        }
        else
        {
            if ((after?.Group ?? 0) == 0)
            {
                documents.AddRange(await ReadAsync(clienteId, [.. RequestStates.Active.Select(RequestStates.ToStored)], after, limit + 1, cancellationToken));
            }

            if (documents.Count < limit + 1)
            {
                var finishedAfter = after is { Group: 1 } ? after : null;
                documents.AddRange(await ReadAsync(clienteId, [.. RequestStates.Finished.Select(RequestStates.ToStored)], finishedAfter, limit + 1 - documents.Count, cancellationToken));
            }
        }

        var page = documents.Take(limit).Select(ToRecord).ToList();
        var next = documents.Count > limit ? RequestCursor.EncodeGrouped(RequestStates.GroupOf(page[^1].Estado), page[^1].CreadoEnUtc, page[^1].Id) : null;
        return new RequestRecordPage(page, next);
    }

    private Task<List<BsonDocument>> ReadAsync(string clienteId, string[] states, (int Group, DateTime CreatedAtUtc, ObjectId Id)? after, int take, CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter;
        var conditions = new List<FilterDefinition<BsonDocument>> { filter.Eq("clienteId", clienteId), filter.In("estado", states) };
        if (after is { } cursor)
        {
            conditions.Add(filter.Or(
                filter.Lt("creadoEn", cursor.CreatedAtUtc),
                filter.And(filter.Eq("creadoEn", cursor.CreatedAtUtc), filter.Lt("_id", cursor.Id))));
        }

        return Requests.Find(filter.And(conditions), _findOptions)
            .Sort(Builders<BsonDocument>.Sort.Descending("creadoEn").Descending("_id"))
            .Limit(take)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Cotizaciones vivas (<c>enviada</c> o <c>aceptada</c>) de cada solicitud dada; las que no tienen ninguna no salen.</summary>
    public async Task<IReadOnlyDictionary<ObjectId, int>> CountLiveQuotesAsync(IReadOnlyCollection<ObjectId> requestIds, CancellationToken cancellationToken)
    {
        if (requestIds.Count == 0)
        {
            return new Dictionary<ObjectId, int>();
        }

        var filter = Builders<BsonDocument>.Filter;
        var documents = await Quotes.Aggregate(new AggregateOptions { MaxTime = mongoOptions.Value.QueryMaxTime })
            .Match(filter.In("solicitudId", requestIds) & filter.In("estado", _liveQuoteStates))
            .Group(new BsonDocument { { "_id", "$solicitudId" }, { "total", new BsonDocument("$sum", 1) } })
            .ToListAsync(cancellationToken);
        return documents.ToDictionary(d => d["_id"].AsObjectId, d => d["total"].ToInt32());
    }

    /// <summary>Si ese técnico ya cotizó esa solicitud (cualquier estado de la cotización).</summary>
    public async Task<bool> HasQuotedAsync(string technicianUserId, ObjectId requestId, CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter;
        return await Quotes.Find(filter.Eq("solicitudId", requestId) & filter.Eq("tecnicoUsuarioId", technicianUserId), _findOptions)
            .Project(Builders<BsonDocument>.Projection.Include("_id"))
            .Limit(1)
            .AnyAsync(cancellationToken);
    }

    private static BsonDocument ToDocument(RequestRecord record)
    {
        var document = new BsonDocument
        {
            { "_id", record.Id },
            { "clienteId", record.ClienteId },
            { "oficioClave", record.OficioClave },
            { "oficioNombre", record.OficioNombre },
            { "modalidad", record.Modalidad },
            { "descripcion", record.Descripcion },
            { "fotos", new BsonArray(record.Fotos) },
            { "dia", record.Dia },
            { "franja", record.Franja },
            { "coloniaId", record.ColoniaId },
            { "coloniaNombre", record.ColoniaNombre },
            { "coloniaCp", record.ColoniaCp },
        };
        if (record.DireccionCifrada is not null)
        {
            document.Add("direccionCifrada", record.DireccionCifrada);
        }

        if (record.ElementoId is not null)
        {
            // Spec 041, R-19: opcional y aditivo; sin él el documento queda como antes.
            document.Add("elementoId", record.ElementoId);
        }

        document.Add("estado", RequestStates.ToStored(record.Estado));
        document.Add("oleadaActual", 0);
        document.Add("cotizacionesActivas", 0);
        document.Add("venceEn", record.VenceEnUtc);
        document.Add("creadoEn", record.CreadoEnUtc);
        document.Add("actualizadoEn", record.CreadoEnUtc);
        return document;
    }

    internal static RequestRecord ToRecord(BsonDocument document) => new(
        document["_id"].AsObjectId,
        document["clienteId"].AsString,
        document["oficioClave"].AsString,
        document.GetValue("oficioNombre", document["oficioClave"]).AsString,
        document["modalidad"].AsString,
        document["descripcion"].AsString,
        [.. document["fotos"].AsBsonArray.Select(x => x.AsString)],
        document["dia"].AsString,
        document["franja"].AsString,
        document["coloniaId"].ToString()!,
        document["coloniaNombre"].AsString,
        document["coloniaCp"].AsString,
        document.TryGetValue("direccionCifrada", out var address) && address.IsString ? address.AsString : null,
        RequestStates.ParseRequest(document["estado"].AsString),
        document["venceEn"].ToUniversalTime(),
        document["creadoEn"].ToUniversalTime(),
        document.TryGetValue("elementoId", out var elementId) && elementId.IsString ? elementId.AsString : null);
}
