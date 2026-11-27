using MongoDB.Driver;

namespace Knock.SecurityTest.Support;

/// <summary>Las rutas probadas escriben en transacciones: sus colecciones se crean antes, como las dejan los scripts de <c>db/</c>.</summary>
internal static class MongoCollections
{
    public static async Task CreateIfMissingAsync(IMongoDatabase database, params string[] names)
    {
        using var cursor = await database.ListCollectionNamesAsync();
        var existing = await cursor.ToListAsync();
        foreach (var name in names.Except(existing, StringComparer.Ordinal))
        {
            await database.CreateCollectionAsync(name);
        }
    }
}
