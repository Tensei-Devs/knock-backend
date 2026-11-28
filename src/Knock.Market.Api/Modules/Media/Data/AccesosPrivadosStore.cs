using Knock.Market.Api.Modules.Media.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Media.Data;

/// <summary>Registro de solo inserción de accesos a blobs privados (spec 032, R-16).</summary>
internal interface IAccesosPrivadosStore
{
    Task InsertAsync(AccesoPrivado acceso, CancellationToken cancellationToken);
}

internal sealed class AccesosPrivadosStore([FromKeyedServices(MediaMongo.ModuleKey)] IMongoDatabase database) : IAccesosPrivadosStore
{
    public const string Coleccion = "accesos_privados";

    public Task InsertAsync(AccesoPrivado acceso, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(acceso);
        var documento = new BsonDocument
        {
            ["motivo"] = acceso.Motivo,
            ["blobId"] = acceso.BlobId,
            ["subHmac"] = acceso.SubHmac,
            ["emitidoEn"] = acceso.EmitidoEn,
        };
        return database.GetCollection<BsonDocument>(Coleccion).InsertOneAsync(documento, cancellationToken: cancellationToken);
    }
}
