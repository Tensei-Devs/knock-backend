using System.Globalization;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.DatabaseTest.Support;

/// <summary>Índices de una colección como texto comparable entre motores.</summary>
internal static class MongoIndexes
{
    /// <summary>Nombre, clave (<c>campo:tipo</c>; los números como entero, cada motor los da con otro tipo) y unicidad, ordenados por nombre.</summary>
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
                index.GetValue("unique", false).ToBoolean() ? "unique" : "-"))
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}
