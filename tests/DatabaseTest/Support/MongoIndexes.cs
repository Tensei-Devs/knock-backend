using System.Globalization;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.DatabaseTest.Support;

/// <summary>Índices de una colección como texto comparable entre motores.</summary>
internal static class MongoIndexes
{
    /// <summary>
    /// Nombre, clave (<c>campo:tipo</c>; los números como entero, cada motor los da con otro tipo), unicidad y TTL
    /// (<c>ttl:&lt;expireAfterSeconds&gt;</c>, o <c>ttl:-</c> si no tiene), ordenados por nombre.
    /// </summary>
    public static async Task<List<string>> DescribeAsync(IMongoDatabase database, string collection)
    {
        ArgumentNullException.ThrowIfNull(database);
        var indexes = await (await database.GetCollection<BsonDocument>(collection).Indexes.ListAsync()).ToListAsync();
        return indexes
            .Select(index => string.Join(
                ' ',
                index["name"].AsString,
                string.Join(',', index["key"].AsBsonDocument.Select(element => element.Name + ":" + (element.Value.IsNumeric
                    ? element.Value.ToInt32().ToString(CultureInfo.InvariantCulture)
                    : element.Value.AsString))),
                index.GetValue("unique", false).ToBoolean() ? "unique" : "-",
                "ttl:" + (index.TryGetValue("expireAfterSeconds", out var ttl) && ttl.IsNumeric
                    ? ttl.ToInt64().ToString(CultureInfo.InvariantCulture)
                    : "-")))
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}
