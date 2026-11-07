using AwesomeAssertions;
using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Sesiones.Data;
using Knock.Identity.Api.Modules.Sesiones.Domain;
using Knock.Identity.Api.Modules.Sesiones.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Knock.UnitTest.Identity;

public sealed class SessionManagementServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly UserAccount _account = new(
        "665f1b2c3d4e5f6071829304",
        "301b89d0-e86f-8a5d-96a1-2bf3b40ad82e",
        AccountState.Active,
        false,
        []);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeStore : ISesionesStore
    {
        public UserAccount? Account { get; set; } = _account;

        public List<StoredSession> Sessions { get; set; } = [];

        public bool MarkResult { get; set; } = true;

        public int MarkCalls { get; private set; }

        public Task<UserAccount?> FindBySubAsync(string sub, CancellationToken cancellationToken) =>
            Task.FromResult(Account?.Sub == sub ? Account : null);

        public Task<UserAccount?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(Account?.Id == userId ? Account : null);

        public Task<UserAccount?> FindByPhoneHashAsync(byte[] phoneHash, CancellationToken cancellationToken) =>
            Task.FromResult<UserAccount?>(null);

        public Task<AccountCreation> CreateAccountAsync(NewAccount account, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddSessionAsync(NewSession session, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StoredSession?> FindSessionByFingerprintAsync(byte[] fingerprint, CancellationToken cancellationToken) =>
            Task.FromResult<StoredSession?>(null);

        public Task<StoredSession?> FindSessionByUsedFingerprintAsync(byte[] fingerprint, CancellationToken cancellationToken) =>
            Task.FromResult<StoredSession?>(null);

        public Task<bool> RotateAsync(string sessionId, byte[] currentFingerprint, byte[] newFingerprint, DateTime now, DateTime newExpiresAt, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<string>> RevokeAllAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<bool> RevokeAsync(string sessionId, string sub, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<StoredSession>> FindOpenSessionsAsync(string userId, DateTime now, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StoredSession>>(
                Sessions.Where(session => session.UserId == userId && !session.Revoked && session.ExpiresAt > now).ToArray());

        public Task<StoredSession?> FindSessionByOwnerAsync(string id, string userId, CancellationToken cancellationToken) =>
            Task.FromResult(Sessions.FirstOrDefault(session => session.Id == id && session.UserId == userId));

        public Task<bool> MarkSessionRevokedAsync(string id, string userId, CancellationToken cancellationToken)
        {
            MarkCalls++;
            var session = Sessions.FirstOrDefault(item => item.Id == id && item.UserId == userId);
            if (session is not null && MarkResult)
            {
                var index = Sessions.IndexOf(session);
                Sessions[index] = session with { Revoked = true };
            }

            return Task.FromResult(MarkResult);
        }
    }

    private sealed class FakeRevocationStore : ISesionCancelada
    {
        public List<(string SessionId, TimeSpan Lifetime)> Cancellations { get; } = [];

        public bool Fail { get; set; }

        public Task CancelAsync(string sessionId, TimeSpan lifetime, CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new TimeoutException("synthetic Redis failure");
            }

            Cancellations.Add((sessionId, lifetime));
            return Task.CompletedTask;
        }
    }

    private static SessionManagementService Build(FakeStore store, FakeRevocationStore revocations) =>
        new(store, revocations, new FixedTimeProvider(_now), NullLogger<SessionManagementService>.Instance);

    private static StoredSession Session(
        string id,
        DateTime? lastActivity = null,
        bool revoked = false,
        DateTime? expiresAt = null) =>
        new(
            id,
            _account.Id,
            _account.Sub,
            "Teléfono de prueba",
            lastActivity ?? _now.UtcDateTime.AddMinutes(-1),
            revoked,
            expiresAt ?? _now.UtcDateTime.AddDays(1));

    [Fact]
    public async Task ListOpenSessions_ReturnsOnlyOpenSessionsAndMarksCurrent_CA_018_01_CA_018_02_CA_018_03()
    {
        var current = Session("665f1b2c3d4e5f6071829301");
        var store = new FakeStore
        {
            Sessions =
            [
                Session("665f1b2c3d4e5f6071829302"),
                current,
                Session("665f1b2c3d4e5f6071829303", revoked: true),
                Session("665f1b2c3d4e5f6071829304", expiresAt: _now.UtcDateTime),
            ],
        };
        var service = Build(store, new FakeRevocationStore());

        var result = await service.ListOpenSessionsAsync(_account.Sub, current.Id, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Count(session => session.Actual).Should().Be(1);
        result.Single(session => session.Actual).Id.Should().Be(current.Id);
        result.Should().OnlyContain(session => session.UltimaActividad.Kind == DateTimeKind.Utc);
        result.Select(session => session.GetType().GetProperties().Select(property => property.Name))
            .Should().AllSatisfy(properties =>
                properties.Should().BeEquivalentTo("Id", "Dispositivo", "UltimaActividad", "Actual"));
    }

    [Fact]
    public async Task ListOpenSessions_WhenThereAreNoOpenSessions_ReturnsEmptyArray_CA_018_02()
    {
        var service = Build(new FakeStore(), new FakeRevocationStore());

        var result = await service.ListOpenSessionsAsync(_account.Sub, "665f1b2c3d4e5f6071829301", CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RevokeSession_WhenOwnedAndActive_CancelsRedisBeforeMarkingMongo_CA_018_04()
    {
        var session = Session("665f1b2c3d4e5f6071829302");
        var store = new FakeStore { Sessions = [session] };
        var revocations = new FakeRevocationStore();
        var service = Build(store, revocations);

        await service.RevokeSessionAsync(_account.Sub, "665f1b2c3d4e5f6071829301", session.Id, CancellationToken.None);

        revocations.Cancellations.Should().ContainSingle()
            .Which.Should().Be((session.Id, TimeSpan.FromMinutes(14)));
        store.MarkCalls.Should().Be(1);
    }

    [Fact]
    public async Task RevokeSession_WhenAlreadyRevoked_IsIdempotent_CA_018_04()
    {
        var session = Session("665f1b2c3d4e5f6071829302", revoked: true);
        var store = new FakeStore { Sessions = [session] };
        var revocations = new FakeRevocationStore();

        await Build(store, revocations).RevokeSessionAsync(
            _account.Sub,
            "665f1b2c3d4e5f6071829301",
            session.Id,
            CancellationToken.None);

        revocations.Cancellations.Should().ContainSingle();
        store.MarkCalls.Should().Be(1);
    }

    [Fact]
    public async Task RevokeSession_WhenTokenAlreadyExpired_DoesNotWriteRedisButMarksMongo_CA_018_04()
    {
        var session = Session(
            "665f1b2c3d4e5f6071829302",
            lastActivity: _now.UtcDateTime.AddMinutes(-16),
            expiresAt: _now.UtcDateTime.AddDays(-1));
        var store = new FakeStore { Sessions = [session] };
        var revocations = new FakeRevocationStore();

        await Build(store, revocations).RevokeSessionAsync(
            _account.Sub,
            "665f1b2c3d4e5f6071829301",
            session.Id,
            CancellationToken.None);

        revocations.Cancellations.Should().BeEmpty();
        store.MarkCalls.Should().Be(1);
    }

    [Fact]
    public async Task RevokeSession_WhenRedisFails_DoesNotMarkMongo_CA_018_04()
    {
        var session = Session("665f1b2c3d4e5f6071829302");
        var store = new FakeStore { Sessions = [session] };
        var revocations = new FakeRevocationStore { Fail = true };

        var act = () => Build(store, revocations).RevokeSessionAsync(
            _account.Sub,
            "665f1b2c3d4e5f6071829301",
            session.Id,
            CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>();
        store.MarkCalls.Should().Be(0);
    }

    [Theory]
    [InlineData("not-an-object-id", DomainErrorKind.BadRequest)]
    [InlineData("665f1b2c3d4e5f6071829301", DomainErrorKind.Conflict)]
    public async Task RevokeSession_InvalidOrCurrentId_ReturnsContractErrorWithoutWrites_CA_018_07(
        string requestedId,
        DomainErrorKind expected)
    {
        var store = new FakeStore { Sessions = [Session("665f1b2c3d4e5f6071829302")] };
        var revocations = new FakeRevocationStore();

        var act = () => Build(store, revocations).RevokeSessionAsync(
            _account.Sub,
            "665f1b2c3d4e5f6071829301",
            requestedId,
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<DomainException>();
        exception.Which.Kind.Should().Be(expected);
        store.MarkCalls.Should().Be(0);
        revocations.Cancellations.Should().BeEmpty();
    }

    [Fact]
    public async Task RevokeSession_WhenNotOwnedOrMissing_Returns404WithoutWrites_CA_018_05_CA_018_06()
    {
        var store = new FakeStore();
        var revocations = new FakeRevocationStore();

        var act = () => Build(store, revocations).RevokeSessionAsync(
            _account.Sub,
            "665f1b2c3d4e5f6071829301",
            "665f1b2c3d4e5f6071829302",
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<DomainException>();
        exception.Which.Kind.Should().Be(DomainErrorKind.NotFound);
        store.MarkCalls.Should().Be(0);
        revocations.Cancellations.Should().BeEmpty();
    }

    [Fact]
    public async Task RevokeSession_WhenMongoDoesNotMatch_Returns404AfterRedisFailsClosed_CA_018_05()
    {
        var session = Session("665f1b2c3d4e5f6071829302");
        var store = new FakeStore { Sessions = [session], MarkResult = false };
        var revocations = new FakeRevocationStore();

        var act = () => Build(store, revocations).RevokeSessionAsync(
            _account.Sub,
            "665f1b2c3d4e5f6071829301",
            session.Id,
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<DomainException>();
        exception.Which.Kind.Should().Be(DomainErrorKind.NotFound);
        revocations.Cancellations.Should().ContainSingle();
        store.MarkCalls.Should().Be(1);
    }
}
