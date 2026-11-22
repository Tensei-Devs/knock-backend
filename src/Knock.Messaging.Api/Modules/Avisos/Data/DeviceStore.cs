using Knock.Messaging.Api.Modules.Avisos.Domain;
using Knock.Messaging.Api.Modules.Avisos.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Messaging.Api.Modules.Avisos.Data;

internal sealed record DeviceRegistration(DeviceRegistrationStatus Status, string? Id);

// This store is not registered in DI while cross-replica HMAC consistency remains unverified.
internal sealed class DeviceStore(IMongoDatabase database, TokenProtection protection)
{
    private readonly IMongoCollection<BsonDocument> _devices = database.GetCollection<BsonDocument>("notifications.dispositivos");

    public async Task<DeviceRegistration> RegisterAsync(
        string person, string session, string platform, string token, DateTime now, CancellationToken cancellationToken)
    {
        var fingerprint = protection.Fingerprint(token);
        var id = DispositivoId.Create();
        var encrypted = protection.Encrypt(token, person, id);
        var document = new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["id"] = id,
            ["personaId"] = person,
            ["sid"] = session,
            ["plataforma"] = platform,
            ["tokenHuella"] = fingerprint,
            ["identificadorCifrado"] = encrypted,
            ["renovadoEn"] = new BsonDateTime(now),
        };

        for (var attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                try
                {
                    await _devices.InsertOneAsync(document, cancellationToken: cancellationToken);
                    return new DeviceRegistration(DeviceRegistrationStatus.Created, id);
                }
                catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
                {
                    var result = await UpdateExistingAsync(person, session, platform, token, fingerprint, now, cancellationToken);
                    if (result is not null)
                    {
                        return result;
                    }
                }
            }
            catch (MongoCommandException exception) when (exception.Code == 112 && attempt < 5)
            {
                await Task.Delay(WriteConflictBackoff(attempt), cancellationToken);
                continue;
            }
            catch (MongoCommandException exception) when (exception.Code == 112)
            {
                throw new InvalidOperationException("Device registration is temporarily unavailable.");
            }

            if (attempt < 5)
            {
                await Task.Delay(WriteConflictBackoff(attempt), cancellationToken);
            }
        }

        throw new InvalidOperationException("Device registration retries exhausted.");
    }

    private static TimeSpan WriteConflictBackoff(int attempt) => TimeSpan.FromMilliseconds(50 * (attempt + 1));

    public async Task<bool> DeleteAsync(string person, string id, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var result = await _devices.DeleteOneAsync(filters.Eq("id", id) & filters.Eq("personaId", person), cancellationToken);
        return result.DeletedCount == 1;
    }

    private async Task<DeviceRegistration?> UpdateExistingAsync(
        string person, string session, string platform, string token, string fingerprint, DateTime now, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var own = filters.Eq("tokenHuella", fingerprint) & filters.Eq("personaId", person);
        var existing = await _devices.Find(own).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null && !string.Equals(protection.Decrypt(existing["identificadorCifrado"].AsString, person, existing["id"].AsString), token, StringComparison.Ordinal))
        {
            return new DeviceRegistration(DeviceRegistrationStatus.Conflict, null);
        }

        if (existing is null)
        {
            var other = await _devices.Find(filters.Eq("tokenHuella", fingerprint)).FirstOrDefaultAsync(cancellationToken);
            return other is null ? null : new DeviceRegistration(DeviceRegistrationStatus.Conflict, null);
        }

        own &= filters.Eq("_id", existing["_id"]);
        var update = Builders<BsonDocument>.Update.Set("sid", session).Set("plataforma", platform).Set("renovadoEn", new BsonDateTime(now));
        var found = await _devices.FindOneAndUpdateAsync(own, update, cancellationToken: cancellationToken);
        if (found is not null)
        {
            return new DeviceRegistration(DeviceRegistrationStatus.Existing, found["id"].AsString);
        }

        var any = await _devices.Find(filters.Eq("tokenHuella", fingerprint)).FirstOrDefaultAsync(cancellationToken);
        return any is null ? null : new DeviceRegistration(DeviceRegistrationStatus.Conflict, null);
    }
}
