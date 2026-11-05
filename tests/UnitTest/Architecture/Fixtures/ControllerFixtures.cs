// Controller de muestra que lee una colección de MongoDB: nunca se ejecuta, la regla solo lee sus dependencias.
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.UnitTest.Architecture.Fixtures.ControllerMongo.Modules.Alpha.Controllers;

internal static class AlphaController
{
    public static long Count(IMongoCollection<BsonDocument> collection) => collection.CountDocuments(FilterDefinition<BsonDocument>.Empty);
}
