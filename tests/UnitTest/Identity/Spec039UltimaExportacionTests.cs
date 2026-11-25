using AwesomeAssertions;
using Knock.Identity.Api.Common.Errors;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 039, R-22 y CA-35 (enmienda del 2026-11-23): <c>GET /me/exportaciones/ultima</c> firma el enlace de 24 horas al consultarlo. Sin MongoDB.</summary>
public sealed class Spec039UltimaExportacionTests
{
    private const string _userId = "665f1b2c3d4e5f6071829304";
    private const string _sub = "f0000000-0000-8000-8000-000000000001";
    private const string _blob = "665f1b2c3d4e5f6071829304-1764000000.zip";
    private static readonly DateTime _now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    private sealed class RecordingStorage(bool fail = false) : IExportStorage
    {
        public List<(string Name, TimeSpan ValidFor)> Links { get; } = [];

        public Task SaveAsync(string name, byte[] zip, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Uri> CreateLinkAsync(string name, TimeSpan validFor, CancellationToken cancellationToken)
        {
            if (fail)
            {
                throw new InvalidOperationException("container down");
            }

            Links.Add((name, validFor));
            return Task.FromResult(new Uri($"https://exports.invalid/{name}"));
        }
    }

    private static (LatestExportService Service, FakeAccountStore Store, RecordingStorage Storage) Build(bool storageFails = false)
    {
        var store = new FakeAccountStore();
        store.Accounts.Add(new AccountSnapshot(_userId, _sub, AccountState.Active, false, [1, 2, 3], null));
        var storage = new RecordingStorage(storageFails);
        return (new LatestExportService(store, storage, new Spec039Clock(new DateTimeOffset(_now)), NullLogger<LatestExportService>.Instance), store, storage);
    }

    [Fact]
    public async Task WhenTheZipIsReady_SignsALinkOf24HoursAtThatMoment_CA_039_35()
    {
        var (service, store, storage) = Build();
        store.LastExports[_userId] = (_blob, _now.AddHours(-3));

        var result = await service.GetAsync(_sub, CancellationToken.None);

        result.Estado.Should().Be("lista");
        result.Enlace.Should().Be($"https://exports.invalid/{_blob}");
        result.VenceEn.Should().Be(_now.AddHours(24));
        storage.Links.Should().ContainSingle().Which.Should().Be((_blob, TimeSpan.FromHours(24)));
    }

    [Fact]
    public async Task WhileAnExportIsInProgress_AnswersEnPreparacionWithoutALink_CA_039_35()
    {
        var (service, store, storage) = Build();
        store.ExportsInProgress.Add(_userId);
        store.LastExports[_userId] = (_blob, _now.AddDays(-1));

        var result = await service.GetAsync(_sub, CancellationToken.None);

        result.Should().Be(new LatestExportResponse("en_preparacion", null, null));
        storage.Links.Should().BeEmpty();
    }

    [Fact]
    public async Task AfterSevenDays_TheZipIsGoneAndTheExportIsVencida_CA_039_35()
    {
        var (service, store, storage) = Build();
        store.LastExports[_userId] = (_blob, _now.AddDays(-7));

        var result = await service.GetAsync(_sub, CancellationToken.None);

        result.Should().Be(new LatestExportResponse("vencida", null, null));
        storage.Links.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenThePersonNeverAskedForAnExport_Is404ExportNotFound_CA_039_35()
    {
        var (service, _, _) = Build();

        var act = () => service.GetAsync(_sub, CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Kind.Should().Be(DomainErrorKind.NotFound);
    }

    [Fact]
    public async Task WithoutAContainer_Is503AndNoLinkIsInvented_CA_039_35()
    {
        var (service, store, _) = Build(storageFails: true);
        store.LastExports[_userId] = (_blob, _now.AddHours(-1));

        var act = () => service.GetAsync(_sub, CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Kind.Should().Be(DomainErrorKind.ServiceUnavailable);
    }
}
