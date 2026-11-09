using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Common.Security;
using Knock.Identity.Api.Modules.Sesiones.Data;
using Knock.Identity.Api.Modules.Sesiones.Domain;
using Knock.Identity.Api.Modules.Sesiones.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 015: flujo del canje con dobles del verificador y de la base (sin MongoDB; la base real se prueba en integración).</summary>
public class LoginServiceTests
{
    private const string _phone = "+525512345678";
    private const string _otherPhone = "+525598765432";
    private const string _uid = "uid-prueba-0001";
    private const string _deviceId = "3f2b8c1e-5a7d-4e9b-8c3a-1d2e3f4a5b6c";

    private static readonly DateTimeOffset _now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static readonly LoginRequest _request = new("synthetic-id-token", _deviceId, "Teléfono de prueba");

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeVerifier(FirebaseIdentity? identity) : IFirebaseIdTokenVerifier
    {
        public Task<FirebaseIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken) => Task.FromResult(identity);
    }

    private static (LoginService Service, FakeSesionesStore Store, PhoneProtector Phones, JwtSettings Jwt) Build(FirebaseIdentity? identity = null, bool rejected = false)
    {
        var store = new FakeSesionesStore();
        var phones = new PhoneProtector(PhoneProtectionTestKeys.Keys());
        var time = new FixedTimeProvider(_now);
        var jwt = new JwtSettings("knock-api", "knock-app", new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)));
        var service = new LoginService(
            new FakeVerifier(rejected ? null : identity ?? new FirebaseIdentity(_uid, _phone)),
            new SessionTokenIssuer(jwt, time),
            phones,
            store,
            Options.Create(new ConsentVersionsOptions { AvisoPrivacidad = "1.0", Terminos = "1.0" }),
            time,
            NullLogger<LoginService>.Instance);
        return (service, store, phones, jwt);
    }

    private static UserAccount ExistingAccount(PhoneProtector phones, string state = AccountState.Active, bool profileComplete = false, string phone = _phone, string uid = _uid) =>
        new("665f1b2c3d4e5f6071829304", UserIdDerivation.Derive(uid).ToString("D"), state, profileComplete, phones.Hash(phone).Value);

    [Fact]
    public async Task NewPhone_CreatesTheAccountWithConsentsAndTheFirstSession_CA_015_01()
    {
        var (service, store, phones, _) = Build();

        var result = await service.LoginAsync(_request, CancellationToken.None);

        result.Should().NotBeNull();
        result.IsNew.Should().BeTrue();
        result.ProfileComplete.Should().BeFalse();
        var created = store.Created.Should().ContainSingle().Subject;
        created.Sub.Should().Be(UserIdDerivation.Derive(_uid).ToString("D"));
        created.PrivacyNoticeVersion.Should().Be("1.0");
        created.TermsVersion.Should().Be("1.0");
        created.CreatedAt.Should().Be(_now.UtcDateTime, "la fecha sale del reloj inyectado");
        created.PhoneHash.Should().Equal(phones.Hash(_phone).Value);
        created.PhoneCiphertext.AsSpan().IndexOf(System.Text.Encoding.UTF8.GetBytes(_phone)).Should().Be(-1, "el celular no se guarda en claro");
        var session = created.Session;
        session.UserId.Should().Be(created.Id);
        session.DeviceId.Should().Be(_deviceId);
        session.DeviceName.Should().Be("Teléfono de prueba");
        session.Fingerprint.Should().Equal(RefreshTokenFingerprint.Compute(result.Session.RefreshToken), "solo se guarda la huella del token de renovación");
        session.ExpiresAt.Should().Be(_now.UtcDateTime.AddDays(30));
    }

    [Fact]
    public async Task ExistingAccount_ReturnsASessionWithoutCreatingAnotherAccount_CA_015_02()
    {
        var (service, store, phones, _) = Build();
        var account = ExistingAccount(phones, profileComplete: true);
        store.Users.Add(account);

        var result = await service.LoginAsync(_request, CancellationToken.None);

        result.Should().NotBeNull();
        result.IsNew.Should().BeFalse();
        result.ProfileComplete.Should().BeTrue("devuelve el perfilCompleto de la cuenta");
        store.Created.Should().BeEmpty("no se crean usuarios ni consentimientos");
        store.Sessions.Should().ContainSingle().Which.UserId.Should().Be(account.Id);
    }

    [Fact]
    public async Task IssuedAccessToken_CarriesSubAndSidAndTheSessionIdIsTheStoredOne_CA_015_01()
    {
        var (service, store, phones, _) = Build();
        store.Users.Add(ExistingAccount(phones));

        var result = await service.LoginAsync(_request, CancellationToken.None);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(result!.Session.AccessToken);
        jwt.GetClaim("sub").Value.Should().Be(UserIdDerivation.Derive(_uid).ToString("D"));
        jwt.GetClaim("sid").Value.Should().Be(store.Sessions.Single().Id, "el sid del JWT es el id de la sesión guardada");
        jwt.Claims.Select(c => c.Type).Should().NotContain("jti");
        jwt.ValidTo.Should().Be(_now.UtcDateTime.AddMinutes(15));
    }

    [Fact]
    public async Task SuspendedAccount_IsRefusedBeforeCreatingAnySession_CA_015_05()
    {
        var (service, store, phones, _) = Build();
        store.Users.Add(ExistingAccount(phones, AccountState.Suspended));

        var error = await Assert.ThrowsAsync<DomainException>(() => service.LoginAsync(_request, CancellationToken.None));

        error.Kind.Should().Be(DomainErrorKind.Forbidden);
        error.Code.Should().Be("account_suspended");
        store.Sessions.Should().BeEmpty("no se crea ninguna sesión");
        error.Message.Should().NotContain(_phone);
    }

    [Fact]
    public async Task AccountWithScheduledDeletion_StillSignsIn_CA_015_06()
    {
        var (service, store, phones, _) = Build();
        store.Users.Add(ExistingAccount(phones, AccountState.DeletionScheduled));

        var result = await service.LoginAsync(_request, CancellationToken.None);

        result.Should().NotBeNull();
        store.Sessions.Should().ContainSingle();
    }

    [Fact]
    public async Task RejectedToken_ReturnsNullAndTouchesNothing_CA_015_04()
    {
        var (service, store, _, _) = Build(rejected: true);

        var result = await service.LoginAsync(_request, CancellationToken.None);

        result.Should().BeNull();
        store.Users.Should().BeEmpty();
        store.Sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task LosingTheCreationRace_ReturnsTheExistingAccount_CA_015_08()
    {
        var (service, store, phones, _) = Build();
        store.WinnerOfRace = ExistingAccount(phones);

        var result = await service.LoginAsync(_request, CancellationToken.None);

        result.Should().NotBeNull();
        result.IsNew.Should().BeFalse("el perdedor de la carrera recibe 200, no un error");
        store.Created.Should().BeEmpty();
        store.Sessions.Should().ContainSingle().Which.UserId.Should().Be("665f1b2c3d4e5f6071829304");
    }

    [Fact]
    public async Task AccountConfirmedBetweenTheTwoReads_IsTreatedAsExisting_CA_015_08()
    {
        var (service, store, phones, _) = Build();
        store.Users.Add(ExistingAccount(phones));
        store.HideSubOnFirstRead = true;

        var result = await service.LoginAsync(_request, CancellationToken.None);

        result.Should().NotBeNull();
        result.IsNew.Should().BeFalse("la segunda lectura ve la cuenta que otro canje confirmó");
        store.Created.Should().BeEmpty();
    }

    [Fact]
    public async Task PhoneWithAnotherUidOrUidWithAnotherPhone_Returns409WithoutRevealingTheOtherAccount_CA_015_09()
    {
        var (service, store, phones, _) = Build(new FirebaseIdentity("uid-prueba-otro", _phone));
        store.Users.Add(ExistingAccount(phones));
        var (serviceB, storeB, phonesB, _) = Build(new FirebaseIdentity(_uid, _otherPhone));
        storeB.Users.Add(ExistingAccount(phonesB));

        var a = await Assert.ThrowsAsync<DomainException>(() => service.LoginAsync(_request, CancellationToken.None));
        var b = await Assert.ThrowsAsync<DomainException>(() => serviceB.LoginAsync(_request, CancellationToken.None));

        foreach (var error in new[] { a, b })
        {
            error.Kind.Should().Be(DomainErrorKind.Conflict);
            error.Code.Should().Be("account_conflict");
            error.Message.Should().NotContain(store.Users[0].Sub, "no revela la otra cuenta");
        }

        store.Created.Should().BeEmpty();
        store.Sessions.Should().BeEmpty();
        storeB.Created.Should().BeEmpty();
        storeB.Sessions.Should().BeEmpty();
    }

    [Fact]
    public void FirebaseIdentityToString_DoesNotExposeUidOrPhone_CA_015_12()
    {
        var text = new FirebaseIdentity(_uid, _phone).ToString();

        text.Should().NotContain(_uid).And.NotContain(_phone).And.NotContain("5512345678");
    }
}
