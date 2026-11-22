using Knock.Messaging.Api.Modules.Avisos.Domain;
using Knock.Messaging.Api.Modules.Avisos.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Messaging.Api.Modules.Avisos.Data;

internal sealed record DeviceRegistration(DeviceRegistrationStatus Status, string? Id);

// DeviceStoreFactory only creates this store when Avisos:Devices:WritesEnabled is true: cross-replica HMAC key consistency is an operator decision.
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
            catch (MongoCommandException exception) when (IsWriteConflict(exception) && attempt < 5)
            {
                await Task.Delay(WriteConflictBackoff(attempt), cancellationToken);
                continue;
            }
            catch (MongoCommandException exception) when (IsWriteConflict(exception))
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

    /// <summary>WriteConflict (112) o, en DocumentDB, el bloqueo mutuo entre inserciones concurrentes del mismo identificador.</summary>
    private static bool IsWriteConflict(MongoCommandException exception) =>
        exception.Code == 112 || exception.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase);

    private static TimeSpan WriteConflictBackoff(int attempt) => TimeSpan.FromMilliseconds(50 * (attempt + 1));

    public async Task<bool> DeleteAsync(string person, string id, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var result = await _devices.DeleteOneAsync(filters.Eq("id", id) & filters.Eq("personaId", person), cancellationToken);
        return result.DeletedCount == 1;
    }

    // Spec 037 R-01: un teléfono cambia de cuenta al iniciar sesión, así que el último registro gana y la persona anterior deja de recibir avisos. Se cambia el id y se vuelve a cifrar con el
    // nuevo dueño (el dueño y el id forman el contexto autenticado del cifrado); el identificador de la persona anterior nunca se devuelve. Si el registro desapareció entre la lectura y la
    // escritura devuelve null y el llamador reintenta el alta.
    private async Task<DeviceRegistration?> ReassignAsync(
        BsonDocument other, string person, string session, string platform, string token, DateTime now, CancellationToken cancellationToken)
    {
        var newId = DispositivoId.Create();
        var update = Builders<BsonDocument>.Update
            .Set("id", newId)
            .Set("personaId", person)
            .Set("sid", session)
            .Set("plataforma", platform)
            .Set("identificadorCifrado", protection.Encrypt(token, person, newId))
            .Set("renovadoEn", new BsonDateTime(now));
        var filter = Builders<BsonDocument>.Filter.Eq("_id", other["_id"]) & Builders<BsonDocument>.Filter.Eq("id", other["id"].AsString);
        var replaced = await _devices.FindOneAndUpdateAsync(filter, update, cancellationToken: cancellationToken);
        return replaced is null ? null : new DeviceRegistration(DeviceRegistrationStatus.Created, newId);
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
            return other is null ? null : await ReassignAsync(other, person, session, platform, token, now, cancellationToken);
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
