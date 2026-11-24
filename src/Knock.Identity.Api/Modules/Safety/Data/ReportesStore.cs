using Knock.Hosting.Concurrency;
using Knock.Hosting.Data;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.Panel;
using Knock.Identity.Api.Modules.Safety.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Identity.Api.Modules.Safety.Data;

/// <summary>Un reporte tal como lo necesita el servicio: nunca lleva el texto ni la evidencia al leerlo para listar (spec 043, R-22).</summary>
internal sealed record StoredReport(ObjectId Id, string ReporterId, string ReportedId, string Reason, string State, bool HasDecision, bool HasAppeal, DateTime CreatedAtUtc);

/// <summary>Lo que hace falta para guardar un reporte nuevo.</summary>
internal sealed record NewReport(string ReporterId, string ReportedId, string? ReferenceKind, string? ReferenceId, string Reason, string Text, IReadOnlyList<string> Evidence, DateTime CreatedAtUtc);

/// <summary>Cuenta las advertencias que cuentan para la regla gradual (spec 059, R-16). Existe para probar <c>SafetyApi</c> sin MongoDB.</summary>
internal interface IWarningCounter
{
    /// <summary>
    /// Las advertencias <b>decididas por un moderador y vigentes</b> contra esa persona, leídas dentro de la transacción de la decisión (así incluyen la que se está decidiendo). Un reporte abierto,
    /// en apelación o sin decidir nunca cuenta (R-17).
    /// </summary>
    Task<int> CountDecidedWarningsAsync(StoreTransaction transaction, string reportedId, DateTime since, CancellationToken cancellationToken);
}

/// <summary>
/// Colección <c>reportes</c> (spec 043, §6). Es el único acceso a MongoDB de los reportes. La persona que reporta y la reportada se guardan con su identificador público (el <c>sub</c>).
/// Toda lectura de la lista lleva el <c>sub</c> de quien llama (R-24). El texto y la evidencia no salen de aquí hacia quien llama: solo los lee el panel (R-22).
/// </summary>
internal sealed class ReportesStore(
    [FromKeyedServices(DataExtensions.DatabaseKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> mongoOptions) : IWarningCounter
{
    public const string ReportsCollection = "reportes";
    private const string _reporterField = "reportaId";
    private const string _reportedField = "reportadoId";
    private const string _stateField = "estado";
    private const string _decisionField = "decision";
    private const string _appealField = "apelacion";
    private const string _createdField = "creadoEn";
    private const string _idField = "_id";

    private readonly FindOptions _findOptions = new() { MaxTime = mongoOptions.Value.QueryMaxTime };

    private IMongoCollection<BsonDocument> Reports => database.GetCollection<BsonDocument>(ReportsCollection);

    /// <summary>Índices de la spec 043 (§6). Idempotente; lo usan las pruebas y el script de <c>db/</c> hace lo mismo.</summary>
    public Task EnsureIndexesAsync(CancellationToken cancellationToken) =>
        Reports.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("gravedad").Ascending(_stateField).Ascending(_createdField), new CreateIndexOptions { Name = "gravedad_1_estado_1_creadoEn_1" }),
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending(_reportedField), new CreateIndexOptions { Name = "reportadoId_1" }),
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending(_reporterField), new CreateIndexOptions { Name = "reportaId_1" }),
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending(ReportRules.NotFinishedVisitField),
                    new CreateIndexOptions { Name = "visitaNoTerminadaId_1", Unique = true, Sparse = true }),
            ],
            cancellationToken);

    public async Task<int> CountDecidedWarningsAsync(StoreTransaction transaction, string reportedId, DateTime since, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var count = await Reports.CountDocumentsAsync(
            (IClientSessionHandle)transaction.Handle,
            filters.Eq(_reportedField, reportedId) & filters.Eq(_stateField, "decidido") & filters.Eq(_decisionField + ".respuesta", "advertencia") & filters.Gte(_decisionField + ".decididoEn", new BsonDateTime(since)),
            cancellationToken: cancellationToken);
        return (int)count;
    }

    /// <summary>El reporte abierto de esa persona contra la misma otra por el mismo motivo, si ya existe: una repetición por mala señal no abre otro.</summary>
    public async Task<StoredReport?> FindOpenDuplicateAsync(string reporterId, string reportedId, string reason, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var document = await Reports
            .Find(filters.Eq(_reporterField, reporterId) & filters.Eq(_reportedField, reportedId) & filters.Eq("motivo", reason) & filters.Eq(_stateField, "abierto"), _findOptions)
            .FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToStored(document);
    }

    public async Task<StoredReport> InsertAsync(NewReport report, CancellationToken cancellationToken)
    {
        var document = new BsonDocument
        {
            [_idField] = ObjectId.GenerateNewId(),
            [_reporterField] = report.ReporterId,
            [_reportedField] = report.ReportedId,
            ["motivo"] = report.Reason,
            ["descripcion"] = report.Text,
            ["evidencia"] = new BsonArray(report.Evidence),
            [_stateField] = "abierto",
            [_createdField] = new BsonDateTime(report.CreatedAtUtc),
        };
        if (report.ReferenceKind is not null)
        {
            document["referencia"] = new BsonDocument { ["tipo"] = report.ReferenceKind, ["id"] = report.ReferenceId };
        }

        await Reports.InsertOneAsync(document, cancellationToken: cancellationToken);
        return ToStored(document);
    }

    /// <summary>
    /// Abre el reporte de «no terminado» de una visita (spec 053): un solo reporte por visita, aunque el evento llegue repetido o dos entregas corran a la vez (upsert por el campo <c>visitaNoTerminadaId</c>, con índice único y disperso).
    /// <see langword="true"/> si lo abrió ahora, <see langword="false"/> si ya existía.
    /// </summary>
    public Task<bool> OpenFromVisitAsync(NewReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);

        // Dentro de la instancia, las entregas de la misma visita se turnan (KeyedGate): varios upsert a la vez sobre el índice único `visitaNoTerminadaId_1` se bloqueaban entre sí en
        // documentdb-local (código 24 o 112) y respondían 500. Quien entra después encuentra el reporte y responde «ya existía». Entre instancias vale el índice único.
        return KeyedGate.RunAsync(report.ReferenceId ?? string.Empty, () => OpenFromVisitSerializedAsync(report, cancellationToken), cancellationToken);
    }

    private async Task<bool> OpenFromVisitSerializedAsync(NewReport report, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        try
        {
            var result = await Reports.UpdateOneAsync(
                filters.Eq(ReportRules.NotFinishedVisitField, report.ReferenceId),
                Builders<BsonDocument>.Update
                    .SetOnInsert(_reporterField, report.ReporterId)
                    .SetOnInsert(_reportedField, report.ReportedId)
                    .SetOnInsert("referencia.tipo", report.ReferenceKind)
                    .SetOnInsert("referencia.id", report.ReferenceId)
                    .SetOnInsert("motivo", report.Reason)
                    .SetOnInsert("descripcion", report.Text)
                    .SetOnInsert("evidencia", new BsonArray(report.Evidence))
                    .SetOnInsert(_stateField, "abierto")
                    .SetOnInsert(_createdField, new BsonDateTime(report.CreatedAtUtc)),
                new UpdateOptions { IsUpsert = true },
                cancellationToken);
            return result.UpsertedId is not null;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Otra entrega del mismo evento ganó la carrera: el reporte ya existe.
            return false;
        }
    }

    /// <summary>Los reportes hechos por quien llama, del más reciente al más antiguo (por <c>_id</c>). Pide uno de más para saber si hay otra página.</summary>
    public async Task<IReadOnlyList<StoredReport>> ListByReporterAsync(string reporterId, ObjectId? after, int take, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var filter = filters.Eq(_reporterField, reporterId);
        if (after is { } cursor)
        {
            filter &= filters.Lt(_idField, cursor);
        }

        var documents = await Reports.Find(filter, _findOptions).Sort(Builders<BsonDocument>.Sort.Descending(_idField)).Limit(take).ToListAsync(cancellationToken);
        return [.. documents.Select(ToStored)];
    }

    public async Task<StoredReport?> FindAsync(ObjectId id, CancellationToken cancellationToken)
    {
        var document = await Reports.Find(Builders<BsonDocument>.Filter.Eq(_idField, id), _findOptions).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToStored(document);
    }

    /// <summary>
    /// Guarda la apelación en una sola operación condicionada a que haya decisión y no haya apelación previa: de dos apelaciones simultáneas gana una. Falso si no se aplicó.
    /// </summary>
    public async Task<bool> AppealAsync(ObjectId id, string text, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var result = await Reports.UpdateOneAsync(
            filters.Eq(_idField, id) & filters.Eq(_stateField, "decidido") & filters.Exists(_decisionField) & filters.Exists(_appealField, exists: false),
            Builders<BsonDocument>.Update
                .Set(_appealField, new BsonDocument { ["texto"] = text, [_createdField] = new BsonDateTime(nowUtc) })
                .Set(_stateField, "en_apelacion"),
            cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    private static StoredReport ToStored(BsonDocument d) => new(
        d[_idField].AsObjectId,
        d[_reporterField].AsString,
        d[_reportedField].AsString,
        d["motivo"].AsString,
        d[_stateField].AsString,
        d.Contains(_decisionField),
        d.Contains(_appealField),
        d[_createdField].ToUniversalTime());
}
