using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.Identity.Api.Common.Security;
using Knock.Identity.Api.Modules.Sesiones.Data;
using Knock.Identity.Api.Modules.Sesiones.Domain;
using Knock.Identity.Api.Modules.Sesiones.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 016: renovación con rotación, detección de reuso y cierre de sesión, con dobles de la base y de Redis.</summary>
public class SessionRefreshServiceTests
{
    private const string _userId = "665f1b2c3d4e5f6071829304";
    private static readonly Guid _sub = Guid.Parse("0f8e7d6c-5b4a-8392-a1b0-c9d8e7f6a5b4");
    private static readonly DateTimeOffset _now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeCancelled : ISesionCancelada
    {
        public List<(string SessionId, TimeSpan Lifetime)> Entries { get; } = [];

        public Task CancelAsync(string sessionId, TimeSpan lifetime, CancellationToken cancellationToken)
        {
            Entries.Add((sessionId, lifetime));
            return Task.CompletedTask;
        }
    }

    private sealed class Fixture
    {
        public FakeSesionesStore Store { get; } = new();

        public FakeCancelled Cancelled { get; } = new();

        public SessionTokenIssuer Issuer { get; }

        public SessionRefreshService Refresh { get; }

        public LogoutService Logout { get; }

        public Fixture()
        {
            var time = new FixedTimeProvider(_now);
            Issuer = new SessionTokenIssuer(new JwtSettings("knock-api", "knock-app", new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32))), time);
            Refresh = new SessionRefreshService(Store, Cancelled, Issuer, time, NullLogger<SessionRefreshService>.Instance);
            Logout = new LogoutService(Store, Cancelled, NullLogger<LogoutService>.Instance);
            Store.Users.Add(new UserAccount(_userId, _sub.ToString("D"), AccountState.Active, false, [1]));
        }

        /// <summary>Abre una sesión como el canje y devuelve su token de renovación y su sid.</summary>
        public (string RefreshToken, string SessionId) OpenSession(DateTime? expiresAt = null)
        {
            var sessionId = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
            var issued = Issuer.Issue(_sub, sessionId);
            var session = new NewSession(sessionId, _userId, _sub.ToString("D"), Guid.NewGuid().ToString("D"), "Teléfono de prueba", RefreshTokenFingerprint.Compute(issued.RefreshToken), _now.UtcDateTime);
            Store.AddSessionAsync(session, CancellationToken.None).GetAwaiter().GetResult();
            if (expiresAt is { } expires)
            {
                Store.Stored.Single(s => s.Id == sessionId).ExpiresAt = expires;
            }

            return (issued.RefreshToken, sessionId);
        }
    }

    [Fact]
    public async Task ValidToken_ReturnsANewPairWithTheSameSidAndTheOldTokenStopsWorking_CA_016_01()
    {
        var f = new Fixture();
        var (token, sessionId) = f.OpenSession();

        var result = await f.Refresh.RefreshAsync(token, CancellationToken.None);
        var again = await f.Refresh.RefreshAsync(token, CancellationToken.None);

        result.Should().NotBeNull();
        result.RefreshToken.Should().NotBe(token);
        result.ExpiresInSeconds.Should().Be(900);
        new JsonWebTokenHandler().ReadJsonWebToken(result.AccessToken).GetClaim("sid").Value.Should().Be(sessionId);
        again.Should().BeNull("el token anterior ya no sirve");
    }

    [Fact]
    public async Task Rotation_ReplacesTheFingerprintKeepsTheLastFiveAndMovesTheExpiry_CA_016_01()
    {
        var f = new Fixture();
        var (token, sessionId) = f.OpenSession();
        var first = RefreshTokenFingerprint.Compute(token);

        var current = token;
        for (var i = 0; i < 7; i++)
        {
            current = (await f.Refresh.RefreshAsync(current, CancellationToken.None))!.RefreshToken;
        }

        var session = f.Store.Stored.Single(s => s.Id == sessionId);
        session.Fingerprint.Should().Equal(RefreshTokenFingerprint.Compute(current));
        session.Used.Should().HaveCount(5, "solo se conservan las últimas 5");
        session.Used.Any(used => used.AsSpan().SequenceEqual(first)).Should().BeFalse("la primera ya salió de la lista");
        session.ExpiresAt.Should().Be(_now.UtcDateTime.AddDays(30));
    }

    [Fact]
    public async Task TwoRotationsWithTheSameToken_OnlyOneWins_CA_016_02()
    {
        var f = new Fixture();
        var (token, _) = f.OpenSession();

        var results = await Task.WhenAll(f.Refresh.RefreshAsync(token, CancellationToken.None), f.Refresh.RefreshAsync(token, CancellationToken.None));

        results.Count(r => r is not null).Should().Be(1);
    }

    [Fact]
    public async Task UsedToken_Returns401AndRevokesEverySessionOfTheAccount_CA_016_03()
    {
        var f = new Fixture();
        var (token, firstSid) = f.OpenSession();
        var (_, otherSid) = f.OpenSession();
        var rotated = await f.Refresh.RefreshAsync(token, CancellationToken.None);

        var reuse = await f.Refresh.RefreshAsync(token, CancellationToken.None);
        var afterReuse = await f.Refresh.RefreshAsync(rotated!.RefreshToken, CancellationToken.None);

        reuse.Should().BeNull();
        afterReuse.Should().BeNull("la sesión rotada también quedó revocada");
        f.Store.Stored.Should().OnlyContain(s => s.Revoked);
        f.Cancelled.Entries.Select(e => e.SessionId).Should().BeEquivalentTo(firstSid, otherSid);
        f.Cancelled.Entries.Should().OnlyContain(e => e.Lifetime == TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task RevokedOrExpiredSession_Returns401_CA_016_04()
    {
        var f = new Fixture();
        var (revokedToken, revokedSid) = f.OpenSession();
        f.Store.Stored.Single(s => s.Id == revokedSid).Revoked = true;
        var (expiredToken, _) = f.OpenSession(expiresAt: _now.UtcDateTime.AddSeconds(-1));

        (await f.Refresh.RefreshAsync(revokedToken, CancellationToken.None)).Should().BeNull();
        (await f.Refresh.RefreshAsync(expiredToken, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task SuspendedAccount_Returns401_CA_016_04()
    {
        var f = new Fixture();
        var (token, _) = f.OpenSession();
        f.Store.Users.Clear();
        f.Store.Users.Add(new UserAccount(_userId, _sub.ToString("D"), AccountState.Suspended, false, [1]));

        (await f.Refresh.RefreshAsync(token, CancellationToken.None)).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task MalformedOrUnknownToken_Returns401WithoutThrowing_CA_016_04(string token)
    {
        var f = new Fixture();
        f.OpenSession();

        (await f.Refresh.RefreshAsync(token, CancellationToken.None)).Should().BeNull();
        f.Cancelled.Entries.Should().BeEmpty("un token desconocido no revoca nada");
    }

    [Fact]
    public async Task Logout_RevokesTheSessionAndCancelsItsSidForTheRestOfTheJwt_CA_016_05()
    {
        var f = new Fixture();
        var (_, sessionId) = f.OpenSession();

        await f.Logout.LogoutAsync(sessionId, _sub.ToString("D"), TimeSpan.FromMinutes(7), CancellationToken.None);

        f.Store.Stored.Single().Revoked.Should().BeTrue();
        f.Cancelled.Entries.Should().ContainSingle().Which.Should().Be((sessionId, TimeSpan.FromMinutes(7)));
    }

    [Theory]
    [InlineData(-30, 1)]
    [InlineData(0, 1)]
    [InlineData(20 * 60, 15 * 60)]
    public async Task Logout_ClampsTheCancellationBetweenOneSecondAndFifteenMinutes_CA_016_05(int remainingSeconds, int expectedSeconds)
    {
        var f = new Fixture();
        var (_, sessionId) = f.OpenSession();

        await f.Logout.LogoutAsync(sessionId, _sub.ToString("D"), TimeSpan.FromSeconds(remainingSeconds), CancellationToken.None);

        f.Cancelled.Entries.Single().Lifetime.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public async Task RefreshAfterLogout_Returns401_CA_016_08()
    {
        var f = new Fixture();
        var (token, sessionId) = f.OpenSession();
        await f.Logout.LogoutAsync(sessionId, _sub.ToString("D"), TimeSpan.FromMinutes(10), CancellationToken.None);

        (await f.Refresh.RefreshAsync(token, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Logout_DoesNotRevokeASessionOfAnotherAccount_CA_016_05()
    {
        var f = new Fixture();
        var (_, sessionId) = f.OpenSession();

        await f.Logout.LogoutAsync(sessionId, "00000000-0000-8000-8000-000000000000", TimeSpan.FromMinutes(10), CancellationToken.None);

        f.Store.Stored.Single().Revoked.Should().BeFalse("el sub del JWT no es el dueño de esa sesión");
        f.Cancelled.Entries.Should().BeEmpty("el gateway no debe cancelar la sesión de otra persona (KNOCK-203, H-1)");
    }
}
