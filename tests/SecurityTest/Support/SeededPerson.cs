using MongoDB.Bson;

namespace Knock.SecurityTest.Support;

/// <summary>Una cuenta activa ficticia en la base de Identidad, para las pruebas de acceso entre dos personas.</summary>
internal static class SeededPerson
{
    public static async Task<(string Sub, ObjectId UserId)> CreateAsync(SecurityHosts hosts)
    {
        var sub = TokenForge.Sub();
        var id = ObjectId.GenerateNewId();
        await hosts.Identity.Users.InsertOneAsync(new BsonDocument
        {
            ["_id"] = id,
            ["sub"] = sub,
            ["celularHash"] = new BsonBinaryData(Guid.NewGuid().ToByteArray()),
            ["estado"] = "activa",
            ["esTecnico"] = false,
            ["perfilCompleto"] = false,
            ["creadoEn"] = DateTime.UtcNow,
            ["version"] = 1,
        });
        return (sub, id);
    }
}
