extern alias IdentityApi;

using System.Net;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using IdentityApi::Knock.Identity.Api.Modules.Sesiones.Data;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Identity;

/// <summary>
/// Spec 015, R-05, R-08 y R-10 sobre documentdb-local real: los índices únicos y el TTL, que el script <c>db/002_identidad_usuarios_sesiones.js</c>
/// declara lo mismo que crea el código, y que el límite de 10 sesiones se respeta aunque lleguen canjes simultáneos. Solo datos ficticios.
/// </summary>
public class SesionesIndexesAndLimitTests
{
    private const string _phone = "+525555550411";

    private static async Task<Dictionary<string, BsonDocument>> IndexesAsync(IMongoCollection<BsonDocument> collection) =>
        (await (await collection.Indexes.ListAsync()).ToListAsync()).ToDictionary(index => index["name"].AsString);

    [Fact]
    public async Task UniqueIndexesAndTtl_AreCreatedWithTheSpecDefinition_CA_015_08()
    {
        using var factory = await IdentityApiFactory.CreateAsync();

        var users = await IndexesAsync(factory.Users);
        var sessions = await IndexesAsync(factory.Sessions);

        users["sub_1"]["unique"].AsBoolean.Should().BeTrue();
        users["sub_1"]["key"].AsBsonDocument.Names.Should().Equal("sub");
        users["celularHash_1"]["unique"].AsBoolean.Should().BeTrue();
        users["celularHash_1"]["key"].AsBsonDocument.Names.Should().Equal("celularHash");
        sessions["huellaRenovacion_1"]["unique"].AsBoolean.Should().BeTrue();
        sessions["venceEn_1"]["expireAfterSeconds"].ToInt32().Should().Be(0, "el documento expira al llegar la fecha de venceEn");
        sessions["usuarioId_1_revocada_1_creadoEn_1"]["key"].AsBsonDocument.Names.Should().Equal("usuarioId", "revocada", "creadoEn");
    }

    [Fact]
    public async Task UniqueIndexes_RejectADuplicateSubPhoneHashAndFingerprint_CA_015_08()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        var hash = new BsonBinaryData(new byte[] { 1, 2, 3 });
        var fingerprint = new BsonBinaryData(new byte[] { 9, 8, 7 });
        await factory.Users.InsertOneAsync(new BsonDocument { { "sub", "a" }, { "celularHash", hash } });
        await factory.Sessions.InsertOneAsync(new BsonDocument { { "huellaRenovacion", fingerprint } });

        var sameSub = await Record.ExceptionAsync(() => factory.Users.InsertOneAsync(new BsonDocument { { "sub", "a" }, { "celularHash", new BsonBinaryData(new byte[] { 4 }) } }));
        var samePhone = await Record.ExceptionAsync(() => factory.Users.InsertOneAsync(new BsonDocument { { "sub", "b" }, { "celularHash", hash } }));
        var sameFingerprint = await Record.ExceptionAsync(() => factory.Sessions.InsertOneAsync(new BsonDocument { { "huellaRenovacion", fingerprint } }));

        foreach (var error in new[] { sameSub, samePhone, sameFingerprint })
        {
            error.Should().BeOfType<MongoWriteException>().Which.WriteError.Category.Should().Be(ServerErrorCategory.DuplicateKey);
        }
    }

    [Fact]
    public async Task TheDbScript_DeclaresTheSameIndexesAsTheCode_CA_015_08()
    {
        using var factory = await IdentityApiFactory.CreateAsync();
        var script = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "db", "002_identidad_usuarios_sesiones.js"));
        var declared = Regex.Matches(script, @"identity\.(?<col>usuarios|sesiones)\.createIndex\(\{(?<key>[^}]*)\},\s*\{(?<opts>[^}]*)\}\)")
            .Select(m => (
                Collection: m.Groups["col"].Value,
                Keys: Regex.Matches(m.Groups["key"].Value, @"(\w+):\s*1").Select(k => k.Groups[1].Value).ToArray(),
                Unique: m.Groups["opts"].Value.Contains("unique: true", StringComparison.Ordinal),
                Expire: Regex.Match(m.Groups["opts"].Value, @"expireAfterSeconds:\s*(\d+)") is { Success: true } e ? int.Parse(e.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : (int?)null,
                Name: Regex.Match(m.Groups["opts"].Value, "name:\\s*\"([^\"]+)\"").Groups[1].Value))
            .ToList();
        var actual = new Dictionary<string, Dictionary<string, BsonDocument>>
        {
            ["usuarios"] = await IndexesAsync(factory.Users),
            ["sesiones"] = await IndexesAsync(factory.Sessions),
        };

        declared.Should().HaveCount(5, "el script crea 2 índices de usuarios y 3 de sesiones");
        foreach (var index in declared)
        {
            var created = actual[index.Collection].Should().ContainKey(index.Name).WhoseValue;
            created["key"].AsBsonDocument.Names.Should().Equal(index.Keys, index.Name);
            (created.Contains("unique") && created["unique"].AsBoolean).Should().Be(index.Unique, index.Name);
            (created.Contains("expireAfterSeconds") ? created["expireAfterSeconds"].ToInt32() : (int?)null).Should().Be(index.Expire, index.Name);
        }
    }

    [Fact]
    public async Task SimultaneousExchangesOnNineOpenSessions_NeverLeaveMoreThanTen_CA_015_07()
    {
        var (idToken, _) = await FirebaseEmulator.SignInWithPhoneAsync(_phone);
        using var factory = await IdentityApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        for (var i = 0; i < 9; i++)
        {
            using var response = await AuthHttp.ExchangeAsync(client, idToken);
            response.EnsureSuccessStatusCode();
        }

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => AuthHttp.ExchangeAsync(client, idToken)));

        try
        {
            responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK, "cada canje simultáneo termina bien");
            (await factory.Sessions.CountDocumentsAsync(new BsonDocument(SesionesStore.RevokedField, false))).Should().Be(10, "nunca quedan más de 10 abiertas");
            (await factory.Sessions.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(14);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Knock.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Knock.slnx was not found above the test binaries.");
    }
}
