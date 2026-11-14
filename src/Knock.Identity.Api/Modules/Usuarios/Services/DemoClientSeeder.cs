using Knock.Identity.Api.Modules.Sesiones.Services;
using Knock.Identity.Api.Modules.Usuarios.Data;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Knock.Identity.Api.Modules.Usuarios.Services;

/// <summary>Preparación parcial de la demo; no se registra ni conecta al arranque hasta implementar las guardas.</summary>
internal sealed class DemoClientSeeder(UsuariosStore store, PhoneProtector phones)
{
    public async Task SeedAsync(string path, CancellationToken cancellationToken)
    {
        var input = BsonSerializer.Deserialize<BsonArray>(await File.ReadAllTextAsync(path, cancellationToken));
        foreach (var entry in input.Select(item => item.AsBsonDocument))
        {
            var id = entry["_id"].AsObjectId;
            var phone = entry["celular"].AsString;
            var encrypted = phones.Protect(phone, id.ToString());
            var hash = phones.Hash(phone);
            var document = entry.DeepClone().AsBsonDocument;
            document.Remove("uid");
            document.Remove("celular");
            document["sub"] = UserIdDerivation.Derive(entry["uid"].AsString).ToString("D");
            document["celularCifrado"] = new BsonBinaryData(encrypted.Ciphertext);
            document["celularCifradoVersion"] = encrypted.KeyVersion;
            document["celularHash"] = new BsonBinaryData(hash.Value);
            document["celularHashVersion"] = hash.KeyVersion;
            document["esTecnico"] = false;
            document["creadoEn"] = BsonDateTime.Create(DateTime.Parse(entry["creadoEn"].AsString, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));
            foreach (var consent in document["consentimientos"].AsBsonArray.Select(item => item.AsBsonDocument))
            {
                consent["aceptadoEn"] = BsonDateTime.Create(DateTime.Parse(consent["aceptadoEn"].AsString, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal));
            }

            await store.UpsertDemoAsync(document, cancellationToken);
        }
    }
}
