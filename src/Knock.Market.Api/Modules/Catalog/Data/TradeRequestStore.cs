using Knock.Market.Api.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Catalog.Data;

/// <summary>Lo que «Otro oficio» necesita de MongoDB; existe para probar el servicio con dobles.</summary>
internal interface ITradeRequestStore
{
    /// <summary>La cuenta tiene perfil de técnico (registro hecho).</summary>
    Task<bool> TechnicianExistsAsync(string subject, CancellationToken cancellationToken);

    /// <summary>Guarda el texto de la persona; si ya había guardado el mismo texto normalizado, devuelve el registro existente sin duplicarlo (índice único <c>usuarioId</c> + <c>textoNormalizado</c>).</summary>
    Task<string> SaveAsync(string subject, string text, string normalizedText, DateTime createdAtUtc, CancellationToken cancellationToken);
}

/// <summary>
/// Colección <c>oficios_pedidos</c> (escritura, spec 045, OFP-01) y existencia del perfil de técnico en <c>tecnicos</c> (solo lectura, como hacen la búsqueda y las solicitudes). El guardado es un
/// <c>upsert</c> por <c>usuarioId</c> y <c>textoNormalizado</c>: la repetición por mala señal no deja duplicados (CA-41) y dos personas distintas sí cuentan por separado.
/// </summary>
internal sealed class TradeRequestStore(
    [FromKeyedServices(CatalogModuleExtensions.ModuleKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> options) : ITradeRequestStore
{
    private const string _technicians = "tecnicos";
    private const string _userField = "usuarioId";
    private const string _textField = "texto";
    private const string _normalizedField = "textoNormalizado";
    private const string _createdField = "creadoEn";

    private FindOptions Find => new() { MaxTime = options.Value.QueryMaxTime };

    public async Task<bool> TechnicianExistsAsync(string subject, CancellationToken cancellationToken) =>
        await database.GetCollection<BsonDocument>(_technicians)
            .Find(Builders<BsonDocument>.Filter.Eq(_userField, subject), Find)
            .Project(Builders<BsonDocument>.Projection.Include(CatalogStore.IdField))
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken) is not null;

    public async Task<string> SaveAsync(string subject, string text, string normalizedText, DateTime createdAtUtc, CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.Eq(_userField, subject) & Builders<BsonDocument>.Filter.Eq(_normalizedField, normalizedText);
        var update = Builders<BsonDocument>.Update
            .SetOnInsert(_textField, text)
            .SetOnInsert(_createdField, createdAtUtc);
        var saved = await database.GetCollection<BsonDocument>(CatalogAdminStore.TradeRequestsCollection).FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            cancellationToken);
        return saved[CatalogStore.IdField].AsObjectId.ToString();
    }
}
