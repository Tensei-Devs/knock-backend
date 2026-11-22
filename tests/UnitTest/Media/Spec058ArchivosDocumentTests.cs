using AwesomeAssertions;
using Knock.Market.Api.Modules.Media.Data;
using Knock.Market.Api.Modules.Media.Domain;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Media;

/// <summary>Spec 058 (T-02): la forma del documento de <c>archivos</c> y las fechas de caducidad, sin base de datos.</summary>
public sealed class Spec058ArchivosDocumentTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Document_UsesCamelCaseKeys_ObjectIdAndHexIds_CA_058_03()
    {
        var file = MediaFile.CreatePending("synthetic-sub", MediaUsage.Request, "image/jpeg", 1000, _now);

        var document = MongoMediaFileStore.ToDocument(file);

        document["_id"].BsonType.Should().Be(BsonType.ObjectId);
        document.Names.Where(name => name != "_id").Should().OnlyContain(name => !name.Contains('_') && char.IsLower(name[0]));
        document["archivoId"].AsString.Should().MatchRegex("^[0-9a-f]{32}$");
        document["blobKey"].AsString.Should().MatchRegex("^[0-9a-f]{32}$").And.NotBe(document["archivoId"].AsString);
        document["estado"].AsString.Should().Be("pendiente");
        document["version"].ToInt32().Should().Be(1);
        document.Names.Should().NotContain(["sub", "nombre", "celular", "sas"]);
    }

    [Fact]
    public void Document_RoundTripsAReadyFile_CA_058_03()
    {
        var ready = MediaFile.CreatePending("synthetic-sub", MediaUsage.Profile, "image/png", 4096, _now).MarkReady(640, 480, _now.AddMinutes(2));

        var back = MongoMediaFileStore.ToDomain(MongoMediaFileStore.ToDocument(ready));

        back.Should().Be(ready);
    }

    [Fact]
    public void Document_StoresConfirmationWindowAndExpiry_CA_058_04()
    {
        var pending = MediaFile.CreatePending("synthetic-sub", MediaUsage.Job, "image/webp", 10, _now);
        var rejected = pending.MarkRejected(_now.AddMinutes(1));
        var ready = pending.MarkReady(10, 10, _now.AddMinutes(1));

        MongoMediaFileStore.ToDocument(pending)["confirmarAntesDe"].ToUniversalTime().Should().Be(_now.AddMinutes(30).UtcDateTime);
        MongoMediaFileStore.ToDocument(pending)["expiraEn"].ToUniversalTime().Should().Be(_now.AddDays(7).UtcDateTime);
        MongoMediaFileStore.ToDocument(rejected)["expiraEn"].ToUniversalTime().Should().Be(_now.AddDays(7).UtcDateTime);
        MongoMediaFileStore.ToDocument(ready)["expiraEn"].ToUniversalTime().Should().Be(new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("desconocido")]
    [InlineData("")]
    public void ParseState_RejectsAnUnknownState_R_08(string state)
    {
        var parse = () => MongoMediaFileStore.ParseState(state);

        parse.Should().Throw<InvalidOperationException>();
    }
}
