using System.Net;
using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.Identity.Api.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>
/// Spec 053 T-11 (R-25, CA-39): la clave del contador del panel lee el <c>sid</c> sin validar la firma, así que debe incluir la IP del cliente para que un token inventado con el <c>sid</c> de otra sesión
/// no agote el cupo de esa sesión desde otra red.
/// </summary>
public sealed class Spec053PanelSessionKeyTests
{
    private static string Token(string sid)
    {
        var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object> { ["sub"] = Guid.NewGuid().ToString("N"), ["sid"] = sid },
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        });
    }

    private static DefaultHttpContext Context(string? bearer, string ip)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        if (bearer is not null)
        {
            context.Request.Headers.Authorization = "Bearer " + bearer;
        }

        return context;
    }

    [Fact]
    public void ASessionIdWrittenFromAnotherNetwork_DoesNotShareTheCounterOfTheRealSession_CA_053_39()
    {
        var sid = Guid.NewGuid().ToString("N");

        var owner = IdentityRateLimits.PanelSessionKey(Context(Token(sid), "198.51.100.10"));
        var forged = IdentityRateLimits.PanelSessionKey(Context(Token(sid), "203.0.113.77"));

        owner.Should().NotBeNull();
        forged.Should().NotBeNull().And.NotBe(owner, "otra IP no gasta el cupo de la sesión ajena");
    }

    [Fact]
    public void TheSameSessionFromTheSameNetwork_CountsInOneBucketEvenWithDifferentTokens_CA_053_39()
    {
        var sid = Guid.NewGuid().ToString("N");

        IdentityRateLimits.PanelSessionKey(Context(Token(sid), "198.51.100.10"))
            .Should().Be(IdentityRateLimits.PanelSessionKey(Context(Token(sid), "198.51.100.10")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("no-es-un-token")]
    public void WithoutAReadableToken_TheKeyIsNull_SoTheLimiterCountsByIp_CA_053_39(string? bearer) =>
        IdentityRateLimits.PanelSessionKey(Context(bearer, "198.51.100.10")).Should().BeNull();

    [Fact]
    public void ASessionIdWithUnexpectedCharacters_IsNotUsedAsKey_CA_053_39() =>
        IdentityRateLimits.PanelSessionKey(Context(Token("a b/../c"), "198.51.100.10")).Should().BeNull();
}
