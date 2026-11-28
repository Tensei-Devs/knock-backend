extern alias IdentityApi;

using System.Net;
using System.Security.Cryptography;
using AwesomeAssertions;
using IdentityApi::Knock.Identity.Api.Modules.Trust.Data;
using Knock.SecurityTest.Support;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.SecurityTest.Identity;

/// <summary>
/// Spec 025 (R-10, R-13, CA-17): una persona con sesión válida no confirma las fotos ni envía a revisión la verificación de otra. Identidad real en Production con MongoDB y Redis reales
/// y una verificación sembrada que existe; el control es la misma petición de su dueña, que sí pasa la comprobación de propiedad. Solo datos ficticios.
/// </summary>
public sealed class AccesoVerificacionSecurityTests
{
    private const string _route = "/api/v1/me/verificacion/";

    [Fact]
    public async Task AnotherPerson_NeitherConfirmsPhotosNorSubmitsMyVerification_CA_025_17()
    {
        await using var factory = await SecurityIdentityFactory.CreateAsync(new Dictionary<string, string?>
        {
            ["Verificacion:CurpHmac:ActiveVersion"] = "1",
            ["Verificacion:CurpHmac:Keys:1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        });
        using var client = factory.CreateClient();
        await factory.Services.GetRequiredService<VerificationStore>().EnsureIndexesAsync(CancellationToken.None);
        await MongoCollections.CreateIfMissingAsync(factory.Database, "tareas_programadas", "outbox");
        var owner = await factory.SignInAsync(client);
        var other = await factory.SignInAsync(client);
        var id = await SeedCaptureAsync(factory, owner.Sub);
        var confirm = $"{_route}{id}/fotos/selfie/confirmar";
        var submit = $"{_route}{id}/enviar";
        var verifications = factory.Database.GetCollection<BsonDocument>("verificaciones");
        var byId = Builders<BsonDocument>.Filter.Eq("_id", id);
        var before = (await verifications.Find(byId).SingleAsync()).ToJson();

        using var foreignConfirm = await SecureHttp.SendAsync(client, HttpMethod.Post, confirm, bearer: other.AccessToken);
        using var foreignSubmit = await SecureHttp.SendAsync(client, HttpMethod.Post, submit, bearer: other.AccessToken);

        await SecureHttp.AssertProblemAsync(foreignConfirm, HttpStatusCode.NotFound, owner.Sub);
        await SecureHttp.AssertProblemAsync(foreignSubmit, HttpStatusCode.NotFound, owner.Sub);
        (await verifications.Find(byId).SingleAsync()).ToJson().Should().Be(before, "una petición rechazada no cambia nada");

        // Control: la foto ya está cifrada, así que su dueña recibe 409 (pasó la propiedad) y sí envía a revisión.
        using var ownConfirm = await SecureHttp.SendAsync(client, HttpMethod.Post, confirm, bearer: owner.AccessToken);
        using var ownSubmit = await SecureHttp.SendAsync(client, HttpMethod.Post, submit, bearer: owner.AccessToken);

        ownConfirm.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ownConfirm.Content.ReadAsStringAsync()).Should().Contain("photo_already_confirmed");
        ownSubmit.StatusCode.Should().Be(HttpStatusCode.OK, await ownSubmit.Content.ReadAsStringAsync());
        (await verifications.Find(byId).SingleAsync())["estado"].AsString.Should().Be("en_revision");
    }

    private static async Task<ObjectId> SeedCaptureAsync(SecurityIdentityFactory factory, string owner)
    {
        var id = ObjectId.GenerateNewId();
        await factory.Database.GetCollection<BsonDocument>("verificaciones").InsertOneAsync(new BsonDocument
        {
            ["_id"] = id,
            ["usuarioId"] = owner,
            ["nivel"] = "basica",
            ["estado"] = "en_captura",
            ["curpHmac"] = id.ToString(),
            ["curpVigente"] = false,
            ["llaveEnvuelta"] = "llave-ficticia",
            ["llaveKekVersion"] = "test-kek",
            ["fotos"] = new BsonArray
            {
                new BsonDocument { ["tipo"] = "ine_frente", ["estado"] = "cifrada" },
                new BsonDocument { ["tipo"] = "selfie", ["estado"] = "cifrada" },
            },
            ["creadoEn"] = DateTime.UtcNow,
            ["capturaVenceEn"] = DateTime.UtcNow.AddHours(1),
        });
        return id;
    }
}
