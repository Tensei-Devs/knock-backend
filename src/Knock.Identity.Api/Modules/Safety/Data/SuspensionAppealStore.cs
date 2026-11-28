using Knock.Hosting.Data;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.Panel;
using Knock.Identity.Api.Modules.Safety.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Identity.Api.Modules.Safety.Data;

/// <summary>
/// La apelación de una persona suspendida sobre la colección <c>reportes</c> (spec 060, §6): <c>apelacion.origen</c> y <c>apelacion.resuelta</c>. Las escrituras son operaciones condicionadas al estado
/// leído, así que de dos apelaciones o de dos resoluciones simultáneas gana una. El texto nunca sale de aquí hacia un registro.
/// </summary>
internal sealed class SuspensionAppealStore(
    [FromKeyedServices(DataExtensions.DatabaseKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> mongoOptions)
{
    private const string _idField = "_id";
    private const string _stateField = "estado";
    private const string _reportedField = "reportadoId";
    private const string _appealField = "apelacion";
    private const string _originField = "apelacion.origen";
    private const string _resolvedField = "apelacion.resuelta";

    private static readonly FilterDefinitionBuilder<BsonDocument> _filter = Builders<BsonDocument>.Filter;

    private IMongoCollection<BsonDocument> Reports => database.GetCollection<BsonDocument>(ReportesStore.ReportsCollection);

    public async Task<ReportForAppeal?> FindAsync(ObjectId id, CancellationToken cancellationToken)
    {
        var document = await Reports.Find(_filter.Eq(_idField, id), new FindOptions { MaxTime = mongoOptions.Value.QueryMaxTime }).Limit(1).FirstOrDefaultAsync(cancellationToken);
        if (document is null || !document.TryGetValue(_reportedField, out var reported) || !reported.IsString)
        {
            return null;
        }

        return new ReportForAppeal(
            reported.AsString,
            document[_stateField].AsString,
            document.TryGetValue("decision", out var decision) && decision is BsonDocument stored && stored.TryGetValue("decididoPor", out var by) && by.IsString ? by.AsString : null,
            document.TryGetValue(_appealField, out var appeal) && appeal is BsonDocument appealDocument ? ToAppeal(appealDocument) : null);
    }

    /// <summary>Guarda la apelación de la suspensión en una operación condicionada a que el reporte esté decidido, sea de esa persona y no tenga apelación. Falso si no se aplicó.</summary>
    public async Task<bool> OpenAsync(ObjectId id, string reportedId, string text, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var result = await Reports.UpdateOneAsync(
            _filter.Eq(_idField, id) & _filter.Eq(_reportedField, reportedId) & _filter.Eq(_stateField, ReportStates.Decided) & _filter.Exists("decision") & _filter.Exists(_appealField, exists: false),
            Builders<BsonDocument>.Update
                .Set(_appealField, new BsonDocument { ["texto"] = text, ["creadoEn"] = new BsonDateTime(nowUtc), ["origen"] = AppealOrigins.Suspension })
                .Set(_stateField, ReportStates.Appealed),
            cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    /// <summary>
    /// Cierra la apelación de la suspensión dentro de la transacción de quien llama: el reporte vuelve a <c>decidido</c> y <c>apelacion.resuelta</c> guarda la decisión, quién resolvió y la marca «misma persona».
    /// Falso si ya estaba resuelta o dejó de ser una apelación de suspensión.
    /// </summary>
    public async Task<bool> ResolveAsync(StoreTransaction transaction, ObjectId id, string decision, string by, bool samePerson, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var result = await Reports.UpdateOneAsync(
            (IClientSessionHandle)transaction.Handle,
            _filter.Eq(_idField, id) & _filter.Eq(_stateField, ReportStates.Appealed) & _filter.Eq(_originField, AppealOrigins.Suspension) & _filter.Exists(_resolvedField, exists: false),
            Builders<BsonDocument>.Update
                .Set(_stateField, ReportStates.Decided)
                .Set(_resolvedField, new BsonDocument { ["decision"] = decision, ["en"] = new BsonDateTime(nowUtc), ["por"] = by, ["mismaPersona"] = samePerson }),
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    private static StoredAppeal ToAppeal(BsonDocument appeal)
    {
        var resolved = appeal.TryGetValue("resuelta", out var value) && value is BsonDocument document ? document : null;
        return new StoredAppeal(
            appeal.TryGetValue("texto", out var text) && text.IsString ? text.AsString : string.Empty,
            appeal.TryGetValue("creadoEn", out var created) && created.IsValidDateTime ? created.ToUniversalTime() : DateTime.MinValue,
            appeal.TryGetValue("origen", out var origin) && origin.IsString ? origin.AsString : AppealOrigins.Session,
            resolved is not null && resolved.TryGetValue("decision", out var decision) && decision.IsString ? decision.AsString : null,
            resolved is not null && resolved.TryGetValue("en", out var at) && at.IsValidDateTime ? at.ToUniversalTime() : null);
    }
}
