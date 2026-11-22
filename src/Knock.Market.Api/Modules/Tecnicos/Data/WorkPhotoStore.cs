using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Knock.Market.Api.Modules.Tecnicos.Services;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Tecnicos.Data;

/// <summary>
/// Colección <c>fotos_trabajos</c> (spec 046, TEC-07): una foto por documento <c>{ tecnicoId, visitaId, url, permiso, moderada, creadoEn }</c>. Quien publica o retira
/// las fotos (spec 050, VIS-11, con la moderación de la 031) escribe aquí; esta lectura solo devuelve las que tienen <c>permiso</c> y <c>moderada</c> verdaderos, de la
/// más reciente a la más antigua (el <c>_id</c> crece con el tiempo y es el cursor).
/// </summary>
internal sealed class WorkPhotoStore(
    [FromKeyedServices(TecnicosModuleExtensions.ModuleKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> options) : IWorkPhotoReader
{
    public const string CollectionName = "fotos_trabajos";

    public async Task<IReadOnlyList<WorkPhotoResponse>> ListAsync(ObjectId technicianId, ObjectId? after, int limit, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var filter = filters.Eq("tecnicoId", technicianId) & filters.Eq("permiso", true) & filters.Eq("moderada", true);
        if (after is { } cursor)
        {
            filter &= filters.Lt("_id", cursor);
        }

        var documents = await database.GetCollection<BsonDocument>(CollectionName).Find(filter, new FindOptions { MaxTime = options.Value.QueryMaxTime })
            .Project(Builders<BsonDocument>.Projection.Include("url").Include("creadoEn")).Sort(Builders<BsonDocument>.Sort.Descending("_id"))
            .Limit(limit + 1).ToListAsync(cancellationToken);
        return [.. documents.Where(d => d.GetValue("url", BsonNull.Value).IsString).Select(d => new WorkPhotoResponse(
            d["_id"].AsObjectId.ToString(),
            d["url"].AsString,
            d.GetValue("creadoEn", BsonNull.Value) is { IsValidDateTime: true } created ? created.ToUniversalTime() : d["_id"].AsObjectId.CreationTime))];
    }
}
