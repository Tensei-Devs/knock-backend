using System.Security.Cryptography;
using Knock.Messaging.Api.Modules.Avisos.Push;
using Knock.Messaging.Api.Modules.Avisos.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Messaging.Api.Modules.Avisos.Data;

/// <summary>
/// Dispositivos de una persona para el despachador (spec 037, R-03, R-13): descifra el identificador solo al enviar y lo borra si FCM lo rechaza. Sin la configuración de claves del módulo
/// (<see cref="DeviceKeyConfiguration"/>) no hay dispositivos que leer, así que el despachador no envía nada (falla cerrado).
/// </summary>
internal sealed class DeviceTokens(IMongoDatabase database, DeviceKeyConfiguration keys, ILogger<DeviceTokens> logger) : IDeviceTokens
{
    private readonly IMongoCollection<BsonDocument> _devices = database.GetCollection<BsonDocument>("notifications.dispositivos");

    public async Task<IReadOnlyList<string>> ListAsync(string person, CancellationToken cancellationToken)
    {
        if (keys.Protection is not { } protection)
        {
            return [];
        }

        var documents = await _devices.Find(Builders<BsonDocument>.Filter.Eq("personaId", person)).ToListAsync(cancellationToken);
        var tokens = new List<string>(documents.Count);
        foreach (var document in documents)
        {
            try
            {
                tokens.Add(protection.Decrypt(document["identificadorCifrado"].AsString, person, document["id"].AsString));
            }
            catch (Exception exception) when (exception is CryptographicException or FormatException or KeyNotFoundException)
            {
                // Sin el identificador ni el registro en el mensaje: el dispositivo se omite y queda para el TTL.
                logger.LogWarning("A device registration could not be decrypted and was skipped.");
            }
        }

        return tokens;
    }

    public async Task DeleteAsync(string person, string deviceToken, CancellationToken cancellationToken)
    {
        if (keys.Protection is not { } protection)
        {
            return;
        }

        var filters = Builders<BsonDocument>.Filter;
        await _devices.DeleteOneAsync(filters.Eq("tokenHuella", protection.Fingerprint(deviceToken)) & filters.Eq("personaId", person), cancellationToken);
    }

    public Task DeleteBySessionAsync(string session, CancellationToken cancellationToken) =>
        _devices.DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("sid", session), cancellationToken);
}
