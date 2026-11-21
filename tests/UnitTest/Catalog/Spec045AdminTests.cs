using System.Security.Claims;
using AwesomeAssertions;
using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Catalog.Domain;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Knock.Market.Api.Modules.Catalog.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Knock.UnitTest.Catalog;

/// <summary>Spec 045 (OFP-02, LIS-02): cursor, validador de paginación y puerta de permisos del panel.</summary>
public sealed class Spec045AdminTests
{
    private sealed class StubGate(PanelDecision decision) : IPanelPermissionGate
    {
        public string? Permission { get; private set; }

        public Task<PanelDecision> DecideAsync(string subject, string sessionId, string permissionsVersion, string permission, CancellationToken cancellationToken)
        {
            Permission = permission;
            return Task.FromResult(decision);
        }
    }

    private static async Task<bool> AuthorizeAsync(IPanelPermissionGate gate, params Claim[] claims)
    {
        var requirement = new PanelPermissionRequirement(PanelAuthentication.MetricsReadPermission);
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), null);
        await new PanelPermissionHandler(gate, new HttpContextAccessor()).HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact]
    public void Cursor_RoundTripsTheCountAndTheKey_CA_045_28()
    {
        var decoded = AdminCursor.Decode(AdminCursor.Encode(7, "plomero;con punto y coma"));

        decoded.Should().Be((7L, "plomero;con punto y coma"));
    }

    [Theory]
    [InlineData("esto-no-es-un-cursor")]
    [InlineData("____")]
    [InlineData("")]
    public void Cursor_Unreadable_Throws400WithItsCode_CA_045_28(string cursor)
    {
        Action act = () => AdminCursor.Decode(cursor);

        var thrown = act.Should().Throw<Exception>().Which;
        (thrown is ArgumentException || (thrown as DomainException)?.Code == AdminErrorCodes.InvalidCursorCode).Should().BeTrue();
    }

    [Fact]
    public void Cursor_WellFormedButWithBadParts_IsRejected_CA_045_28()
    {
        var forged = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes("-5;texto"));

        Action act = () => AdminCursor.Decode(forged);

        act.Should().Throw<DomainException>().Which.Code.Should().Be(AdminErrorCodes.InvalidCursorCode);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(20, true)]
    [InlineData(50, true)]
    [InlineData(0, false)]
    [InlineData(51, false)]
    [InlineData(-1, false)]
    public void PageQuery_Limit_IsOneToFifty_CA_045_32(int limit, bool valid) =>
        new AdminPageQueryValidator().Validate(new AdminPageQuery { Limite = limit }).IsValid.Should().Be(valid);

    [Fact]
    public void PageQuery_DefaultsToTwenty_CA_045_28() => new AdminPageQuery().Limite.Should().Be(20);

    [Fact]
    public async Task Handler_AsksTheGateForTheRoutePermission_AndAllowsWhenTheGateAllows_CA_045_25()
    {
        var gate = new StubGate(PanelDecision.Allowed);

        (await AuthorizeAsync(gate, new Claim("sub", "a"), new Claim("sid", "s"), new Claim("pv", "1"))).Should().BeTrue();

        gate.Permission.Should().Be("metrics:read");
    }

    [Fact]
    public async Task Handler_DeniesWhenTheGateDenies_CA_045_27() =>
        (await AuthorizeAsync(new StubGate(PanelDecision.Denied), new Claim("sub", "a"), new Claim("sid", "s"), new Claim("pv", "1"))).Should().BeFalse();

    [Fact]
    public async Task Handler_DeniesATokenWithoutSessionClaims_AndNeverAsksTheGate_CA_045_27()
    {
        var gate = new StubGate(PanelDecision.Allowed);

        (await AuthorizeAsync(gate, new Claim("sub", "a"))).Should().BeFalse();

        gate.Permission.Should().BeNull();
    }

    [Fact]
    public async Task DefaultGate_FailsClosed_CA_045_27() =>
        (await new DenyAllPanelPermissionGate().DecideAsync("a", "s", "1", "metrics:read", CancellationToken.None)).Should().Be(PanelDecision.Denied);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-base64!")]
    [InlineData("AAAA")]
    public void ReadKey_WithoutAValidKey_ReturnsARandomOneSoNoTokenValidates_CA_045_26(string? configured)
    {
        var first = InternalServiceAuthentication.ReadKey(configured);
        var second = InternalServiceAuthentication.ReadKey(configured);

        first.Should().HaveCount(32);
        first.Should().NotEqual(second);
    }

    [Fact]
    public void ReadKey_WithAValidKey_ReturnsIt_CA_045_26()
    {
        var key = new byte[40];
        key[0] = 7;

        InternalServiceAuthentication.ReadKey(Convert.ToBase64String(key)).Should().Equal(key);
    }
}
