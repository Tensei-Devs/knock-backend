using System.Globalization;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Tecnicos.Data;

/// <summary>
/// Páginas de TEC-02 (spec 046, R-06) siguiendo el orden pedido. El cursor es el <c>_id</c> del último ítem; para continuar se lee su clave de orden y se piden los que van
/// después de él. Comparar solo el <c>_id</c> repetía y omitía técnicos. Si el técnico del cursor ya no existe, la página sale vacía.
/// </summary>
internal sealed class TechnicianListPaging(IMongoCollection<BsonDocument> technicians, TimeSpan maxTime)
{
    private const string _id = "_id";
    private const string _rating = "calificacion";
    private const string _location = "ubicacionBase";
    private const string _distance = "distanciaMetros";

    private static ProjectionDefinition<BsonDocument> Profile =>
        Builders<BsonDocument>.Projection.Combine(TechnicianProfileStore.ProfileFields.Select(f => Builders<BsonDocument>.Projection.Include(f)));

    /// <summary>Calificación de mayor a menor y, en empate, <c>_id</c> descendente; los que no tienen calificación van al final.</summary>
    public async Task<IReadOnlyList<BsonDocument>> ByRatingAsync(string? cursor, FilterDefinition<BsonDocument> filter, int take, CancellationToken cancellationToken)
    {
        if (cursor is not null)
        {
            var last = await technicians.Find(Builders<BsonDocument>.Filter.Eq(_id, ObjectId.Parse(cursor)), new FindOptions { MaxTime = maxTime })
                .Project(Builders<BsonDocument>.Projection.Include(_rating)).FirstOrDefaultAsync(cancellationToken);
            if (last is null)
            {
                return [];
            }

            filter &= AfterRating(last);
        }

        return await technicians.Find(filter, new FindOptions { MaxTime = maxTime })
            .Sort(Builders<BsonDocument>.Sort.Descending(_rating).Descending(_id))
            .Project(Profile).Limit(take).ToListAsync(cancellationToken);
    }

    /// <summary>De cerca a lejos y, en empate (los técnicos de la misma colonia base comparten punto), <c>_id</c> ascendente.</summary>
    public async Task<IReadOnlyList<BsonDocument>> ByDistanceAsync(TechnicianListQuery query, FilterDefinition<BsonDocument> filter, int take, CancellationToken cancellationToken)
    {
        var near = Near(query, Render(filter));
        if (query.RadioKm is not null && int.TryParse(query.RadioKm, NumberStyles.Integer, CultureInfo.InvariantCulture, out var radius))
        {
            near["maxDistance"] = radius * 1000.0;
        }

        var stages = new List<BsonDocument> { new("$geoNear", near) };
        if (query.Cursor is not null)
        {
            var after = await AfterDistanceAsync(query, ObjectId.Parse(query.Cursor), cancellationToken);
            if (after is null)
            {
                return [];
            }

            stages.Add(after);
        }

        stages.Add(new BsonDocument("$sort", new BsonDocument { { _distance, 1 }, { _id, 1 } }));
        stages.Add(new BsonDocument("$limit", take));
        stages.Add(new BsonDocument("$project", new BsonDocument(TechnicianProfileStore.ProfileFields.Select(f => new BsonElement(f, 1)))));
        return await AggregateAsync(stages, cancellationToken);
    }

    private async Task<BsonDocument?> AfterDistanceAsync(TechnicianListQuery query, ObjectId id, CancellationToken cancellationToken)
    {
        // La distancia del último ítem la calcula el mismo servidor y desde el mismo punto, así que el empate se compara con igualdad exacta.
        var last = await AggregateAsync([new BsonDocument("$geoNear", Near(query, new BsonDocument(_id, id)))], cancellationToken);
        if (last.Count == 0)
        {
            return null;
        }

        var distance = last[0][_distance];
        return new BsonDocument("$match", new BsonDocument("$or", new BsonArray
        {
            new BsonDocument(_distance, new BsonDocument("$gt", distance)),
            new BsonDocument { { _distance, distance }, { _id, new BsonDocument("$gt", id) } },
        }));
    }

    private static FilterDefinition<BsonDocument> AfterRating(BsonDocument last)
    {
        var filters = Builders<BsonDocument>.Filter;
        var id = last[_id].AsObjectId;
        // «Igual a nulo» cubre el campo ausente y el nulo: en orden descendente van juntos al final.
        var unrated = filters.Eq(_rating, BsonNull.Value);
        if (!last.TryGetValue(_rating, out var rating) || !rating.IsNumeric)
        {
            return unrated & filters.Lt(_id, id);
        }

        return filters.Lt(_rating, rating) | (filters.Eq(_rating, rating) & filters.Lt(_id, id)) | unrated;
    }

    private static BsonDocument Near(TechnicianListQuery query, BsonDocument filter) => new()
    {
        ["near"] = new BsonDocument
        {
            ["type"] = "Point",
            ["coordinates"] = new BsonArray { double.Parse(query.Lng!, CultureInfo.InvariantCulture), double.Parse(query.Lat!, CultureInfo.InvariantCulture) },
        },
        ["key"] = _location,
        ["distanceField"] = _distance,
        ["spherical"] = true,
        ["query"] = filter,
    };

    private BsonDocument Render(FilterDefinition<BsonDocument> filter) =>
        filter.Render(new RenderArgs<BsonDocument>(technicians.DocumentSerializer, technicians.Settings.SerializerRegistry));

    private async Task<List<BsonDocument>> AggregateAsync(IEnumerable<BsonDocument> stages, CancellationToken cancellationToken)
    {
        var pipeline = PipelineDefinition<BsonDocument, BsonDocument>.Create(stages);
        using var cursor = await technicians.AggregateAsync(pipeline, new AggregateOptions { MaxTime = maxTime }, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}
