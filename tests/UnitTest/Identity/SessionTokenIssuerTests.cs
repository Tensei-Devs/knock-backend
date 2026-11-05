using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.Identity.Api.Common.Security;
using Knock.Identity.Api.Modules.Sesiones.Services;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 015, R-08, R-09 y CA-10: el JWT de acceso y el token de renovación.</summary>
public class SessionTokenIssuerTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _sub = Guid.Parse("0f8e7d6c-5b4a-8392-a1b0-c9d8e7f6a5b4");
    private const string _sid = "665f1b2c3d4e5f6071829304";

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static (SessionTokenIssuer Issuer, SymmetricSecurityKey Key) Build(TimeProvider? time = null)
    {
        var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        return (new SessionTokenIssuer(new JwtSettings("knock-api", "knock-app", key), time ?? new FixedTimeProvider(_now)), key);
    }

    [Fact]
    public async Task AccessToken_IsHs256With15MinutesAndOnlySubAndSid_CA_015_01()
    {
        var (issuer, key) = Build(TimeProvider.System);
        var before = DateTimeOffset.UtcNow;

        var token = issuer.Issue(_sub, _sid).AccessToken;

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);
        jwt.Alg.Should().Be(SecurityAlgorithms.HmacSha256);
        jwt.GetClaim("sub").Value.Should().Be(_sub.ToString("D"));
        jwt.GetClaim("sid").Value.Should().Be(_sid);
        jwt.Claims.Select(c => c.Type).Should().NotContain("jti");
        new DateTimeOffset(jwt.ValidTo).Should().BeCloseTo(before.AddMinutes(15), TimeSpan.FromSeconds(10));
        jwt.Claims.Select(c => c.Type).Should().NotContain(["phone_number", "role", "celular"]);
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = "knock-api",
            ValidAudience = "knock-app",
            IssuerSigningKey = key,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero,
        });
        validation.IsValid.Should().BeTrue();
    }

    [Fact]
    public void EachIssueGetsAFreshRefreshToken_CA_015_01()
    {
        var (issuer, _) = Build();

        var first = issuer.Issue(_sub, _sid);
        var second = issuer.Issue(_sub, _sid);

        first.RefreshToken.Should().NotBe(second.RefreshToken);
    }

    [Fact]
    public void RefreshToken_Is256RandomBitsAndItsFingerprintIsComputable_CA_015_10()
    {
        var (issuer, _) = Build();

        var refresh = issuer.Issue(_sub, _sid).RefreshToken;

        Base64UrlEncoder.DecodeBytes(refresh).Should().HaveCount(32);
        RefreshTokenFingerprint.Compute(refresh).Should().HaveCount(32);
    }

    [Fact]
    public void MissingSessionId_IsRejected_CA_015_01()
    {
        var (issuer, _) = Build();

        var act = () => issuer.Issue(_sub, " ");

        act.Should().Throw<ArgumentException>();
    }
}
