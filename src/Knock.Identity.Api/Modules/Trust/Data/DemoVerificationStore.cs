using Knock.Hosting.Data;
using Knock.Identity.Api.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Identity.Api.Modules.Trust.Data;

/// <summary>
/// Escritura de la verificación de demostración (spec 025, T-08): solo para la carga de demo local. Reutiliza <see cref="DemoSeedGuard"/> (host local y ningún usuario que no sea demo) y escribe
/// con <c>upsert</c> por <c>_id</c>, así que repetirla no duplica. No guarda CURP ni su HMAC: la verificación demo no tiene CURP vigente.
/// </summary>
internal sealed class DemoVerificationStore(
    [FromKeyedServices(DataExtensions.DatabaseKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> mongoOptions)
{
    private static readonly FilterDefinitionBuilder<BsonDocument> _filter = Builders<BsonDocument>.Filter;

    private IMongoCollection<BsonDocument> Users => database.GetCollection<BsonDocument>("usuarios");

    public Task ValidateAsync(CancellationToken cancellationToken) =>
        DemoSeedGuard.ValidateAsync(mongoOptions.Value.ConnectionString, Users, cancellationToken);

    /// <summary>El <c>sub</c> de la cuenta demo con ese <c>_id</c>, o <see langword="null"/> si no existe (o no es demo).</summary>
    public async Task<string?> FindDemoSubAsync(ObjectId userId, CancellationToken cancellationToken)
    {
        var document = await Users.Find(_filter.Eq("_id", userId) & _filter.Eq("demo", true))
            .Project(Builders<BsonDocument>.Projection.Include("sub")).Limit(1).FirstOrDefaultAsync(cancellationToken);
        return document is not null && document.TryGetValue("sub", out var sub) && sub.IsString ? sub.AsString : null;
    }

    public async Task UpsertAsync(BsonDocument verification, BsonDocument consent, CancellationToken cancellationToken)
    {
        var options = new ReplaceOptions { IsUpsert = true };
        await database.GetCollection<BsonDocument>(VerificationStore.VerificationsCollection)
            .ReplaceOneAsync(_filter.Eq("_id", verification["_id"]), verification, options, cancellationToken);
        await database.GetCollection<BsonDocument>(VerificationStore.ConsentsCollection)
            .ReplaceOneAsync(_filter.Eq("verificacionId", consent["verificacionId"]), consent, options, cancellationToken);
    }
}
