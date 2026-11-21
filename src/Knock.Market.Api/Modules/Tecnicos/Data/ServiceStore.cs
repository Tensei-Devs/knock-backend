using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Tecnicos.Data;

/// <summary>Resultado de crear un servicio: creado, o el límite de 50 ya alcanzado (R-14).</summary>
internal enum ServiceCreateStatus
{
    Created,
    LimitReached,
}

/// <summary>
/// Colección <c>servicios_tecnico</c> (spec 047, §6) y lectura del técnico dueño y del visible en <c>tecnicos</c>. Toda escritura filtra por el <c>tecnicoId</c> resuelto desde el <c>sub</c> del
/// token, así un servicio ajeno no existe para quien lo pide (R-15).
/// </summary>
internal sealed class ServiceStore(
    [FromKeyedServices(TecnicosModuleExtensions.ModuleKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> options)
{
    public const string CollectionName = "servicios_tecnico";
    public const int MaxPerTechnician = 50;
    private const string _id = "_id";
    private const string _technician = "tecnicoId";

    private IMongoCollection<BsonDocument> Services => database.GetCollection<BsonDocument>(CollectionName);

    private IMongoCollection<BsonDocument> Technicians => database.GetCollection<BsonDocument>(TechnicianProfileStore.CollectionName);

    private FindOptions Find => new() { MaxTime = options.Value.QueryMaxTime };

    /// <summary>El técnico de la persona del <c>sub</c> con sus oficios, o <see langword="null"/> si la cuenta no tiene perfil.</summary>
    public async Task<(ObjectId Id, IReadOnlyList<string> Trades)?> FindOwnAsync(string sub, CancellationToken cancellationToken)
    {
        var document = await Technicians.Find(Builders<BsonDocument>.Filter.Eq("usuarioId", sub), Find)
            .Project(Builders<BsonDocument>.Projection.Include("oficios")).FirstOrDefaultAsync(cancellationToken);
        if (document is null)
        {
            return null;
        }

        IReadOnlyList<string> trades = document.TryGetValue("oficios", out var value) && value.IsBsonArray ? [.. value.AsBsonArray.Where(v => v.IsString).Select(v => v.AsString)] : [];
        return (document[_id].AsObjectId, trades);
    }

    /// <summary>Si el técnico existe y es visible (activo y no oculto).</summary>
    public async Task<bool> IsVisibleAsync(ObjectId technicianId, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        return await Technicians.Find(filters.Eq(_id, technicianId) & filters.Eq("oculto", false) & filters.Eq("estado", "activo"), Find).Limit(1).AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TechnicianService>> ListAsync(ObjectId technicianId, CancellationToken cancellationToken)
    {
        var documents = await Services.Find(Builders<BsonDocument>.Filter.Eq(_technician, technicianId), Find)
            .Sort(Builders<BsonDocument>.Sort.Ascending(_id)).Limit(MaxPerTechnician).ToListAsync(cancellationToken);
        return [.. documents.Select(ToDto)];
    }

    public async Task<(ServiceCreateStatus Status, TechnicianService? Service)> CreateAsync(ObjectId technicianId, string name, string? detail, int price, string trade, CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.Eq(_technician, technicianId);
        if (await Services.CountDocumentsAsync(filter, new CountOptions { Limit = MaxPerTechnician, MaxTime = options.Value.QueryMaxTime }, cancellationToken) >= MaxPerTechnician)
        {
            return (ServiceCreateStatus.LimitReached, null);
        }

        var document = new BsonDocument { { _id, ObjectId.GenerateNewId() }, { _technician, technicianId }, { "nombre", name }, { "precioReferencia", price }, { "oficio", trade } };
        if (detail is not null)
        {
            document["detalle"] = detail;
        }

        await Services.InsertOneAsync(document, cancellationToken: cancellationToken);
        return (ServiceCreateStatus.Created, ToDto(document));
    }

    /// <summary>Cambia solo los campos enviados del servicio del técnico; <see langword="null"/> si no existe o es de otro.</summary>
    public async Task<TechnicianService?> UpdateAsync(ObjectId technicianId, ObjectId id, string? name, string? detail, int? price, string? trade, CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.Eq(_id, id) & Builders<BsonDocument>.Filter.Eq(_technician, technicianId);
        var update = Builders<BsonDocument>.Update;
        var sets = new List<UpdateDefinition<BsonDocument>>();
        if (name is not null)
        {
            sets.Add(update.Set("nombre", name));
        }

        if (detail is not null)
        {
            sets.Add(update.Set("detalle", detail));
        }

        if (price is not null)
        {
            sets.Add(update.Set("precioReferencia", price.Value));
        }

        if (trade is not null)
        {
            sets.Add(update.Set("oficio", trade));
        }

        if (sets.Count == 0)
        {
            var current = await Services.Find(filter, Find).FirstOrDefaultAsync(cancellationToken);
            return current is null ? null : ToDto(current);
        }

        var updated = await Services.FindOneAndUpdateAsync(filter, update.Combine(sets), new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return updated is null ? null : ToDto(updated);
    }

    public async Task<bool> DeleteAsync(ObjectId technicianId, ObjectId id, CancellationToken cancellationToken) =>
        (await Services.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq(_id, id) & Builders<BsonDocument>.Filter.Eq(_technician, technicianId), cancellationToken)).DeletedCount > 0;

    private static TechnicianService ToDto(BsonDocument d) =>
        new(d[_id].AsObjectId.ToString(), d["nombre"].AsString, d.TryGetValue("detalle", out var detail) && detail.IsString ? detail.AsString : null, d["precioReferencia"].ToInt32(), d["oficio"].AsString);
}
