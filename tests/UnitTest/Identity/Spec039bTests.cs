using System.IO.Compression;
using System.Text.Json;
using AwesomeAssertions;
using Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;
using Knock.Identity.Api.Modules.Account.Data;
using Knock.Identity.Api.Modules.Account.Services;
using Knock.Identity.Api.Modules.Addresses;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 039b (T-05 y T-04): manejadores <c>PrepararExportacion</c> y <c>VencerRecuperacion</c> con dobles en memoria. Valores ficticios y sin MongoDB.</summary>
public sealed class Spec039bTests
{
    private static readonly string _userId = ObjectId.GenerateNewId().ToString();
    private static readonly string _sub = Guid.NewGuid().ToString("D");
    private static readonly DateTime _now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FakeSource(AccountExportRecord? record) : IAccountExportSource
    {
        public Task<AccountExportRecord?> FindAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(userId == _userId ? record : null);
    }

    private sealed class FakeAddresses(params AddressExport[] stored) : IAddressesApi
    {
        public string ModuleName => "Addresses";

        public Task<IReadOnlyList<AddressExport>> ExportAsync(string accountId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AddressExport>>(stored);
    }

    private sealed class FakeMarket(string json, bool fail = false) : IMarketExport
    {
        public Task<string> ExportJsonAsync(string sub, CancellationToken cancellationToken) =>
            fail ? throw UnavailableMarketAccountClient.Unavailable() : Task.FromResult(json);
    }

    private sealed class FakeMessaging(string json, bool fail = false) : IMessagingExport
    {
        public Task<string> ExportJsonAsync(string sub, CancellationToken cancellationToken) =>
            fail ? throw MessagingExportClient.Unavailable() : Task.FromResult(json);
    }

    private sealed class FakeStorage : IExportStorage
    {
        public Dictionary<string, byte[]> Saved { get; } = [];

        public Task SaveAsync(string name, byte[] zip, CancellationToken cancellationToken)
        {
            Saved[name] = zip;
            return Task.CompletedTask;
        }

        public Task<Uri> CreateLinkAsync(string name, TimeSpan validFor, CancellationToken cancellationToken) =>
            Task.FromResult(new Uri($"https://exports.invalid/{name}?ttl={(int)validFor.TotalHours}"));
    }

    private sealed class FakeNotice : IExportNotice
    {
        public List<(string UserId, string Sub, string ExportKey)> Sent { get; } = [];

        public Task NotifyReadyAsync(string userId, string sub, string exportKey, CancellationToken cancellationToken)
        {
            Sent.Add((userId, sub, exportKey));
            return Task.CompletedTask;
        }
    }

    private sealed record Rig(PrepararExportacionHandler Handler, FakeAccountStore Accounts, FakeStorage Storage, FakeNotice Notice);

    private static AccountExportRecord Account() =>
        new(_sub, "activa", false, "Persona de prueba", "Centro", "20000", _now, [new StoredConsent("promociones", "1", _now, true)]);

    private static Rig Build(IMarketExport? market = null, IMessagingExport? messaging = null, IExportStorage? storage = null, AccountExportRecord? account = null, params AddressExport[] addresses)
    {
        var accounts = new FakeAccountStore();
        accounts.ExportsInProgress.Add(_userId);
        var fake = storage as FakeStorage ?? new FakeStorage();
        var notice = new FakeNotice();
        var handler = new PrepararExportacionHandler(
            new FakeSource(account ?? Account()), new FakeAddresses(addresses), market ?? new FakeMarket("{\"solicitudes\":[]}"), messaging ?? new FakeMessaging("{\"conversaciones\":[]}"), storage ?? fake, notice, accounts, new Spec039Clock(new DateTimeOffset(_now)), NullLogger<PrepararExportacionHandler>.Instance);
        return new Rig(handler, accounts, fake, notice);
    }

    private static TareaReservada Reserved(string type, string key, int attempts = 0) => new(ObjectId.GenerateNewId(), type, key, "lease", attempts);

    private static Dictionary<string, string> Unzip(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes));
        return zip.Entries.ToDictionary(entry => entry.FullName, entry =>
        {
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        });
    }

    [Fact]
    public async Task Export_BuildsAZipWithOneJsonPerService_StoresItAndNotifiesWithoutALink_CA_039_35()
    {
        var rig = Build();
        var key = $"{_userId}:1764000000";

        await rig.Handler.HandleAsync(Reserved("PrepararExportacion", key), CancellationToken.None);

        var (name, bytes) = rig.Storage.Saved.Should().ContainSingle().Subject;
        name.Should().Be($"{_userId}-1764000000.zip");
        var files = Unzip(bytes);
        files.Keys.Should().BeEquivalentTo(ExportArchive.IdentityFile, ExportArchive.MarketFile, ExportArchive.MessagingFile);
        files[ExportArchive.MessagingFile].Should().Be("{\"conversaciones\":[]}");
        JsonElement.Parse(files[ExportArchive.IdentityFile]).GetProperty("perfil").GetProperty("name").GetString().Should().Be("Persona de prueba");
        files[ExportArchive.MarketFile].Should().Be("{\"solicitudes\":[]}");
        var sent = rig.Notice.Sent.Should().ContainSingle().Subject;
        sent.UserId.Should().Be(_userId);
        sent.Sub.Should().Be(_sub);
        sent.ExportKey.Should().Be($"{_userId}-1764000000", "el aviso lleva solo identificadores y ningún enlace");
        rig.Accounts.LastExports.Should().ContainKey(_userId).WhoseValue.Should().Be((name, _now), "la cuenta guarda el ZIP para firmar el enlace al consultarlo");
        rig.Accounts.ExportsInProgress.Should().BeEmpty("la marca de exportación en curso se quita al terminar");
    }

    [Fact]
    public async Task Export_IncludesTheAddressesOfTheirOwner_R_22()
    {
        var street = $"Calle {Guid.NewGuid():N} 7";
        var rig = Build(addresses: new AddressExport(street, "Centro", null, true, _now));

        await rig.Handler.HandleAsync(Reserved("PrepararExportacion", $"{_userId}:1"), CancellationToken.None);

        var identity = JsonElement.Parse(Unzip(rig.Storage.Saved.Values.Single())[ExportArchive.IdentityFile]);
        identity.GetProperty("direcciones")[0].GetProperty("street").GetString().Should().Be(street);
    }

    [Fact]
    public async Task Export_WhenMarketIsDown_FailsAndKeepsTheMarkWhileRetriesRemain_ThenClearsItOnTheLastOne()
    {
        var rig = Build(market: new FakeMarket(string.Empty, fail: true));

        var early = () => rig.Handler.HandleAsync(Reserved("PrepararExportacion", $"{_userId}:1", attempts: 0), CancellationToken.None);
        await early.Should().ThrowAsync<Exception>();
        rig.Accounts.ExportsInProgress.Should().Contain(_userId);
        rig.Notice.Sent.Should().BeEmpty();

        var last = () => rig.Handler.HandleAsync(Reserved("PrepararExportacion", $"{_userId}:1", attempts: 4), CancellationToken.None);
        await last.Should().ThrowAsync<Exception>();
        rig.Accounts.ExportsInProgress.Should().BeEmpty("sin más reintentos la persona puede pedirla de nuevo");
    }

    [Fact]
    public async Task Export_WhenMessagingIsDown_FailsClosedSavesNothingAndNeverNotifies_CA_039_35()
    {
        var rig = Build(messaging: new FakeMessaging(string.Empty, fail: true));

        var act = () => rig.Handler.HandleAsync(Reserved("PrepararExportacion", $"{_userId}:1"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        rig.Storage.Saved.Should().BeEmpty("no se guarda un ZIP sin mensajeria.json");
        rig.Notice.Sent.Should().BeEmpty();
        rig.Accounts.ExportsInProgress.Should().Contain(_userId);
    }

    [Fact]
    public async Task Export_WithoutAContainer_FailsClosedAndNeverNotifies()
    {
        var rig = Build(storage: new UnavailableExportStorage());

        var act = () => rig.Handler.HandleAsync(Reserved("PrepararExportacion", $"{_userId}:1"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        rig.Notice.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Export_ForAnAccountThatNoLongerExists_DoesNothing()
    {
        var rig = Build();
        var other = ObjectId.GenerateNewId().ToString();

        await rig.Handler.HandleAsync(Reserved("PrepararExportacion", $"{other}:1"), CancellationToken.None);

        rig.Storage.Saved.Should().BeEmpty();
        rig.Notice.Sent.Should().BeEmpty();
    }

    [Fact]
    public void BlobName_CarriesOnlyIdentifiers()
    {
        ExportArchive.BlobName("a:12").Should().Be("a-12.zip");
    }

    [Fact]
    public async Task RecoveryExpiry_ExpiresTheOpenCaseOfThatMoment_AndIsIdempotent_CA_039_13()
    {
        var store = new FakeAccountStore();
        var phones = new PhoneProtector(PhoneProtectionTestKeys.Keys());
        store.RecoveryCases.Add(new(_userId, phones.Protect("+525512340000", _userId), phones.Hash("+525512340001"), "cliente", _now));
        var seconds = new DateTimeOffset(_now, TimeSpan.Zero).ToUnixTimeSeconds();
        var handler = new VencerRecuperacionHandler(store, new Spec039Clock(new DateTimeOffset(_now.AddDays(7))), NullLogger<VencerRecuperacionHandler>.Instance);

        await handler.HandleAsync(Reserved("VencerRecuperacion", $"{_userId}:{seconds}"), CancellationToken.None);
        await handler.HandleAsync(Reserved("VencerRecuperacion", $"{_userId}:{seconds}"), CancellationToken.None);

        store.ExpiredRecoveries.Should().ContainSingle().Which.Should().Be((_userId, seconds));
        store.RecoveryCases.Should().BeEmpty();
    }

    [Fact]
    public async Task RecoveryExpiry_IgnoresABadBusinessKey()
    {
        var store = new FakeAccountStore();
        var handler = new VencerRecuperacionHandler(store, new Spec039Clock(new DateTimeOffset(_now)), NullLogger<VencerRecuperacionHandler>.Instance);

        await handler.HandleAsync(Reserved("VencerRecuperacion", "sin-formato"), CancellationToken.None);

        store.ExpiredRecoveries.Should().BeEmpty();
    }

    [Fact]
    public void RecoveryExpiryKey_IsDeterministicPerCase()
    {
        AccountOutbox.RecoveryExpiryKey(_userId, _now).Should().Be(AccountOutbox.RecoveryExpiryKey(_userId, _now.AddMilliseconds(5)));
        AccountOutbox.RecoveryExpiryKey(_userId, _now).Should().NotBe(AccountOutbox.RecoveryExpiryKey(_userId, _now.AddSeconds(1)));
    }
}
