using AwesomeAssertions;
using Knock.Contracts.Colonias;
using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Usuarios.Controllers.V1;
using Knock.Identity.Api.Modules.Usuarios.Data;
using Knock.Identity.Api.Modules.Usuarios.Domain;
using Knock.Identity.Api.Modules.Usuarios.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 017: lectura con caché, escritura que valida la colonia y borra la caché, y el ETag débil. Con dobles de MongoDB, Redis y Mercado.</summary>
public class ProfileServiceTests
{
    private const string _sub = "0f8e7d6c-5b4a-8392-a1b0-c9d8e7f6a5b4";
    private static readonly DateTime _now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeStore : IUsuariosStore
    {
        public UserProfile? Stored { get; set; }

        public int Reads { get; private set; }

        public int Writes { get; private set; }

        public Task<UserProfile?> FindProfileAsync(string sub, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(Stored is { } p && p.Id == sub ? p : null);
        }

        public Task<(ProfileUpdateOutcome Outcome, UserProfile? Profile)> UpdateProfileAsync(string sub, ProfileUpdate update, DateTime now, CancellationToken cancellationToken)
        {
            if (Stored is null || Stored.Id != sub)
            {
                return Task.FromResult((ProfileUpdateOutcome.NotFound, (UserProfile?)null));
            }

            if (Stored.State == ProfileStates.DeletionScheduled)
            {
                return Task.FromResult((ProfileUpdateOutcome.DeletionScheduled, (UserProfile?)null));
            }

            Writes++;
            Stored = Stored with { Name = update.Name, PhotoUrl = update.PhotoUrl, Colonia = update.Colonia, PostalCode = update.PostalCode, ProfileComplete = true, Version = Stored.Version + 1 };
            return Task.FromResult((ProfileUpdateOutcome.Updated, (UserProfile?)Stored));
        }
    }

    private sealed class FakeCache : IProfileCache
    {
        public Dictionary<string, UserProfile> Entries { get; } = [];

        public int Removals { get; private set; }

        public Task<UserProfile?> GetAsync(string sub, CancellationToken cancellationToken) => Task.FromResult(Entries.GetValueOrDefault(sub));

        public Task SetAsync(UserProfile profile, CancellationToken cancellationToken)
        {
            Entries[profile.Id] = profile;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string sub, CancellationToken cancellationToken)
        {
            Removals++;
            Entries.Remove(sub);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeColonias : IColoniaCatalog
    {
        public HashSet<string> Known { get; } = ["Centro", "Tultengo"];

        public bool Down { get; set; }

        public int Calls { get; private set; }

        public Task<bool> ExistsAsync(string colonia, CancellationToken cancellationToken)
        {
            Calls++;
            return Down ? throw new HttpRequestException("synthetic") : Task.FromResult(Known.Contains(colonia));
        }
    }

    private static UserProfile NewAccount(string state = "activa") =>
        new(_sub, string.Empty, null, null, null, false, state, false, _now.AddDays(-1), 1);

    private static (ProfileService Service, FakeStore Store, FakeCache Cache, FakeColonias Colonias) Build(UserProfile? stored = null)
    {
        var store = new FakeStore { Stored = stored ?? NewAccount() };
        var cache = new FakeCache();
        var colonias = new FakeColonias();
        return (new ProfileService(store, cache, colonias, new FixedTimeProvider(_now), NullLogger<ProfileService>.Instance), store, cache, colonias);
    }

    [Fact]
    public async Task SecondRead_IsServedFromTheCacheWithoutTouchingTheDatabase_CA_017_02()
    {
        var (service, store, _, _) = Build();

        var first = await service.GetAsync(_sub, CancellationToken.None);
        var second = await service.GetAsync(_sub, CancellationToken.None);

        second.Should().Be(first);
        store.Reads.Should().Be(1);
    }

    [Fact]
    public async Task ReadOfAnUnknownAccount_IsNotFoundAndNothingIsCached_CA_017_01()
    {
        var (service, _, cache, _) = Build();

        var error = await Assert.ThrowsAsync<DomainException>(() => service.GetAsync("otro-sub", CancellationToken.None));

        error.Kind.Should().Be(DomainErrorKind.NotFound);
        cache.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidUpdate_SavesBumpsTheVersionCompletesTheProfileAndClearsTheCache_CA_017_05()
    {
        var (service, store, cache, _) = Build();
        await service.GetAsync(_sub, CancellationToken.None);
        cache.Entries.Should().ContainKey(_sub);

        var saved = await service.UpdateAsync(_sub, new ProfileUpdate("Ana", null, "Centro", "42800"), CancellationToken.None);

        saved.Name.Should().Be("Ana");
        saved.ProfileComplete.Should().BeTrue();
        saved.Version.Should().Be(2);
        store.Writes.Should().Be(1);
        cache.Entries.Should().NotContainKey(_sub, "la clave se borra antes de responder");
        (await service.GetAsync(_sub, CancellationToken.None)).Name.Should().Be("Ana");
    }

    [Fact]
    public async Task UnknownColonia_Returns422AndSavesNothing_CA_017_07()
    {
        var (service, store, cache, _) = Build();

        var error = await Assert.ThrowsAsync<DomainException>(() => service.UpdateAsync(_sub, new ProfileUpdate("Ana", null, "Inventada", null), CancellationToken.None));

        error.Kind.Should().Be(DomainErrorKind.Unprocessable);
        error.Code.Should().Be(ProfileService.ColoniaNotFoundCode);
        store.Writes.Should().Be(0);
        cache.Removals.Should().Be(0);
    }

    [Fact]
    public async Task MarketDown_Returns503AndSavesNothing_CA_017_12()
    {
        var (service, store, _, colonias) = Build();
        colonias.Down = true;

        var error = await Assert.ThrowsAsync<DomainException>(() => service.UpdateAsync(_sub, new ProfileUpdate("Ana", null, "Centro", null), CancellationToken.None));

        error.Kind.Should().Be(DomainErrorKind.ServiceUnavailable);
        error.Code.Should().Be(ProfileService.MarketUnavailableCode);
        store.Writes.Should().Be(0);
        store.Stored!.Name.Should().BeEmpty();
    }

    [Fact]
    public async Task WithoutColonia_MarketIsNotAsked_CA_017_12()
    {
        var (service, _, _, colonias) = Build();
        colonias.Down = true;

        var saved = await service.UpdateAsync(_sub, new ProfileUpdate("Ana", null, null, null), CancellationToken.None);

        saved.Name.Should().Be("Ana");
        colonias.Calls.Should().Be(0);
    }

    [Fact]
    public async Task AccountWithScheduledDeletion_Returns409AndSavesNothing_CA_017_13()
    {
        var (service, store, _, colonias) = Build(NewAccount(ProfileStates.DeletionScheduled));

        var error = await Assert.ThrowsAsync<DomainException>(() => service.UpdateAsync(_sub, new ProfileUpdate("Ana", null, "Centro", null), CancellationToken.None));

        error.Kind.Should().Be(DomainErrorKind.Conflict);
        error.Code.Should().Be(ProfileService.DeletionScheduledCode);
        store.Writes.Should().Be(0);
        colonias.Calls.Should().Be(0, "no se consulta a Mercado si la cuenta ya no se puede cambiar");
    }

    [Fact]
    public async Task UpdateOfAnUnknownAccount_IsNotFound_CA_017_09()
    {
        var (service, _, _, _) = Build();

        var error = await Assert.ThrowsAsync<DomainException>(() => service.UpdateAsync("otro-sub", new ProfileUpdate("Ana", null, null, null), CancellationToken.None));

        error.Kind.Should().Be(DomainErrorKind.NotFound);
    }

    [Fact]
    public void ETag_IsWeakChangesWithTheVersionAndDiffersBetweenPeople_CA_017_03()
    {
        var one = MeController.ETag(NewAccount());
        var next = MeController.ETag(NewAccount() with { Version = 2 });
        var other = MeController.ETag(NewAccount() with { Id = "otra-persona" });

        one.Should().StartWith("W/\"").And.EndWith("\"");
        next.Should().NotBe(one);
        other.Should().NotBe(one, "un cliente que cambia de cuenta nunca debe recibir un 304 ajeno");
    }

    [Theory]
    [InlineData(true, "{0}")]
    [InlineData(true, "{1}")]
    [InlineData(true, "\"otro\", {0}")]
    [InlineData(true, "*")]
    [InlineData(false, "\"otro\"")]
    [InlineData(false, "")]
    [InlineData(false, "   ")]
    public void IfNoneMatch_IsComparedWithTheWeakTag_CA_017_03(bool expected, string header)
    {
        var etag = MeController.ETag(NewAccount());

        MeController.Matches(string.Format(System.Globalization.CultureInfo.InvariantCulture, header, etag, etag[2..]), etag).Should().Be(expected);
    }
}
