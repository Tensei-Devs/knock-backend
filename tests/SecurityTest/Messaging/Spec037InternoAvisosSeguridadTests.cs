using System.Net;
using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Knock.SecurityTest.Support;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.SecurityTest.Messaging;

/// <summary>Spec 037 T-05 y T-07: seguridad negativa de <c>POST /api/v1/interno/avisos</c> (R-11) y humo sin sesión (CA-11, CA-13, CA-14).</summary>
public sealed class Spec037InternoAvisosSeguridadTests
{
    private const string _path = "/api/v1/interno/avisos";

    [Fact]
    public async Task InternalNotices_RejectsAnonymousForeignKeysAndMissingPermissions_CA_037_14()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        var name = "s037_int_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var marketKey = RandomNumberGenerator.GetBytes(32);
        using var mongo = new MongoClient(fixture.ConnectionString);
        try
        {
            await using var host = await CreateHostAsync(fixture.ConnectionString, name, marketKey);
            using var client = host.CreateClient();
            var person = Guid.NewGuid().ToString("D");
            var body = new { eventoId = Guid.NewGuid().ToString("N"), tipo = "solicitud_nueva", destinatarioId = person, rol = "tecnico", recurso = ObjectId.GenerateNewId().ToString() };

            using var anonymous = await SecureHttp.PostJsonAsync(client, _path, body);
            using var foreign = await SecureHttp.PostJsonAsync(client, _path, body, Mint("knock-market", RandomNumberGenerator.GetBytes(32), "solicitud_nueva"));
            using var userToken = await SecureHttp.PostJsonAsync(client, _path, body, TokenForge.Valid());
            using var forbidden = await SecureHttp.PostJsonAsync(client, _path, body, Mint("knock-market", marketKey, "cotizacion_nueva"));
            using var longLived = await SecureHttp.PostJsonAsync(client, _path, body, Mint("knock-market", marketKey, "solicitud_nueva", lifetime: TimeSpan.FromMinutes(30)));

            anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            foreign.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "una clave ajena no firma como Mercado");
            userToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "un token de persona no es un token de servicio");
            forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            longLived.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "el token de servicio vive 5 minutos como máximo");
            using var wrongAudience = await SecureHttp.PostJsonAsync(client, _path, body, Mint("knock-market", marketKey, "solicitud_nueva", audience: "knock-otro"));
            using var wrongIssuer = await SecureHttp.PostJsonAsync(client, _path, body, Mint("knock-otro", marketKey, "solicitud_nueva"));
            using var expired = await SecureHttp.PostJsonAsync(client, _path, body, Mint("knock-market", marketKey, "solicitud_nueva", issuedAgo: TimeSpan.FromMinutes(30)));
            wrongAudience.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "el destinatario del token debe ser Mensajería");
            wrongIssuer.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "un emisor fuera de Mercado e Identidad no firma");
            expired.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "un token vencido no entra");
            (await forbidden.Content.ReadAsStringAsync()).Should().Contain("service_not_allowed");
            (await mongo.GetDatabase(name).GetCollection<BsonDocument>("notifications.enviados").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(0);
        }
        finally
        {
            await mongo.DropDatabaseAsync(name);
        }
    }

    [Fact]
    public async Task InternalNotices_SameCredentialTwice_IsAcceptedWithoutDuplicatingTheNotice_CA_056_10()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        var name = "s056_rep_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var marketKey = RandomNumberGenerator.GetBytes(32);
        using var mongo = new MongoClient(fixture.ConnectionString);
        try
        {
            await using var host = await CreateHostAsync(fixture.ConnectionString, name, marketKey);
            using var client = host.CreateClient();
            var body = new { eventoId = Guid.NewGuid().ToString("N"), tipo = "solicitud_nueva", destinatarioId = Guid.NewGuid().ToString("D"), rol = "tecnico", recurso = ObjectId.GenerateNewId().ToString() };
            var credential = Mint("knock-market", marketKey, "solicitud_nueva");
            var notices = mongo.GetDatabase(name).GetCollection<BsonDocument>("notifications.enviados");

            using var first = await SecureHttp.PostJsonAsync(client, _path, body, credential);
            var afterFirst = await notices.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
            using var second = await SecureHttp.PostJsonAsync(client, _path, body, credential);
            var afterSecond = await notices.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

            first.StatusCode.Should().Be(HttpStatusCode.Accepted);
            second.StatusCode.Should().Be(first.StatusCode, "la segunda presentación de la misma credencial se acepta (sin jti guardado)");
            afterSecond.Should().Be(afterFirst, "el receptor es idempotente por eventoId: un solo efecto");
        }
        finally
        {
            await mongo.DropDatabaseAsync(name);
        }
    }

    [Fact]
    public async Task InternalNotices_RejectsExtraFieldsAndWrongRoleWithoutEchoingValues()
    {
        var marketKey = RandomNumberGenerator.GetBytes(32);
        var settings = new Dictionary<string, string?>
        {
            ["Security:InternalTokens:MarketKey"] = Convert.ToBase64String(marketKey),
            ["Security:InternalTokens:IdentityKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        };
        await using var host = await SecurityMessagingFactory.CreateAsync(settings);
        using var client = host.CreateClient();
        var bearer = Mint("knock-market", marketKey, "solicitud_nueva");
        var freeText = "texto-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
        var resource = ObjectId.GenerateNewId().ToString();

        using var extra = await SecureHttp.PostJsonAsync(
            client, _path, new { eventoId = Guid.NewGuid().ToString("N"), tipo = "solicitud_nueva", destinatarioId = Guid.NewGuid().ToString("D"), rol = "tecnico", recurso = resource, mensaje = freeText }, bearer);
        using var role = await SecureHttp.PostJsonAsync(
            client, _path, new { eventoId = Guid.NewGuid().ToString("N"), tipo = "solicitud_nueva", destinatarioId = Guid.NewGuid().ToString("D"), rol = "cliente", recurso = resource }, bearer);
        using var empty = await SecureHttp.PostJsonAsync(client, _path, new { }, bearer);

        extra.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        role.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        empty.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await extra.Content.ReadAsStringAsync()).Should().NotContain(freeText);
    }

    [Fact]
    public async Task Devices_WithoutSession_Return401OnEveryRoute_CA_037_13()
    {
        await using var host = await SecurityMessagingFactory.CreateAsync();
        using var client = host.CreateClient();

        using var register = await SecureHttp.PostJsonAsync(client, "/api/v1/dispositivos", new { identificador = "synthetic-token", plataforma = "ios" });
        using var delete = await SecureHttp.SendAsync(client, HttpMethod.Delete, "/api/v1/dispositivos/" + Guid.NewGuid().ToString("N"));

        register.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        delete.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InternalNotices_SessionRevoked_DeletesOnlyTheDevicesOfThatSession_CA_037_18()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        var name = "s037_sid_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var identityKey = RandomNumberGenerator.GetBytes(32);
        using var mongo = new MongoClient(fixture.ConnectionString);
        try
        {
            await using var host = await CreateHostAsync(fixture.ConnectionString, name, RandomNumberGenerator.GetBytes(32), identityKey: identityKey);
            using var client = host.CreateClient();
            var devices = mongo.GetDatabase(name).GetCollection<BsonDocument>("notifications.dispositivos");
            var closed = Guid.NewGuid().ToString("N");
            var kept = Guid.NewGuid().ToString("N");
            await devices.InsertManyAsync(
            [
                Device("persona-a", closed),
                Device("persona-a", kept),
                Device("persona-b", Guid.NewGuid().ToString("N")),
            ]);
            var credential = Mint("knock-identity", identityKey, "sesion_revocada");
            var body = new { eventoId = Guid.NewGuid().ToString("N"), tipo = "sesion_revocada", sid = closed };

            using var first = await SecureHttp.PostJsonAsync(client, _path, body, credential);
            using var repeated = await SecureHttp.PostJsonAsync(client, _path, body, credential);
            using var market = await SecureHttp.PostJsonAsync(client, _path, new { eventoId = Guid.NewGuid().ToString("N"), tipo = "sesion_revocada", sid = kept }, Mint("knock-market", RandomNumberGenerator.GetBytes(32), "sesion_revocada"));

            first.StatusCode.Should().Be(HttpStatusCode.Accepted);
            repeated.StatusCode.Should().Be(HttpStatusCode.Accepted, "repetir el aviso no falla");
            market.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await devices.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("sid", closed))).Should().Be(0, "la sesión terminada ya no recibe avisos");
            (await devices.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("sid", kept))).Should().Be(1, "las demás sesiones de la persona siguen");
            (await devices.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(2);
        }
        finally
        {
            await mongo.DropDatabaseAsync(name);
        }
    }

    private static BsonDocument Device(string person, string session) => new()
    {
        ["_id"] = ObjectId.GenerateNewId(),
        ["id"] = Guid.NewGuid().ToString("N"),
        ["personaId"] = person,
        ["sid"] = session,
        ["plataforma"] = "android",
        ["tokenHuella"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
        ["identificadorCifrado"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)),
        ["renovadoEn"] = new BsonDateTime(DateTime.UtcNow),
    };

    /// <summary>
    /// Anfitrión de Mensajería con MongoDB real. Con <paramref name="pushEnabled"/> el despachador queda encendido (por omisión <c>Notificaciones:PushHabilitado</c> es falso y el receptor no registra
    /// nada): se usa una cuenta de servicio ficticia generada en cada ejecución y, al no haber dispositivos, nunca se llama a FCM; solo queda el registro de envíos.
    /// </summary>
    internal static Task<SecurityMessagingFactory> CreateHostAsync(string connectionString, string databaseName, byte[] marketKey, bool pushEnabled = false, byte[]? identityKey = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:KnockMongo"] = connectionString,
            ["Mongo:Databases:Messaging"] = databaseName,
            ["Security:InternalTokens:MarketKey"] = Convert.ToBase64String(marketKey),
            ["Security:InternalTokens:IdentityKey"] = Convert.ToBase64String(identityKey ?? RandomNumberGenerator.GetBytes(32)),
        };
        if (pushEnabled)
        {
            settings["Notificaciones:PushHabilitado"] = "true";
            settings["Notificaciones:Fcm:ProjectId"] = "project-test";
            settings["Notificaciones:Fcm:ServiceAccount"] = FakeServiceAccountJson();
        }

        return SecurityMessagingFactory.CreateAsync(settings);
    }

    /// <summary>
    /// Crea el índice único de deduplicación que en producción pone <c>db/037-notificaciones.js</c> (el servicio no crea índices): sin él, la base temporal de la prueba acepta dos registros del mismo evento.
    /// </summary>
    internal static Task EnsureDeduplicationIndexAsync(IMongoClient mongo, string databaseName) =>
        mongo.GetDatabase(databaseName).GetCollection<BsonDocument>("notifications.enviados").Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("eventoId").Ascending("destinatario"),
                new CreateIndexOptions { Name = "eventoId_1_destinatario_1", Unique = true }));

    private static string FakeServiceAccountJson()
    {
        using var rsa = RSA.Create(2048);
        return System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["client_email"] = "svc@project-test.example",
            ["private_key"] = rsa.ExportPkcs8PrivateKeyPem(),
            ["token_uri"] = "https://oauth.example/token",
        });
    }

    internal static string Mint(string issuer, byte[] key, string permission, TimeSpan? lifetime = null, string audience = "knock-notifications", TimeSpan? issuedAgo = null)
    {
        var now = DateTime.UtcNow - (issuedAgo ?? TimeSpan.Zero);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(lifetime ?? TimeSpan.FromMinutes(4)),
            Claims = new Dictionary<string, object> { ["permisos"] = permission, ["jti"] = Guid.NewGuid().ToString("N") },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
