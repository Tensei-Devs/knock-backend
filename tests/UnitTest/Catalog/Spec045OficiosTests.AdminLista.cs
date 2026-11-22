using System.Reflection;
using System.Security.Claims;
using AwesomeAssertions;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Catalog.Controllers.V1;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Knock.Market.Api.Modules.Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Knock.UnitTest.Catalog;

/// <summary>Spec 045, T-03: <c>GET /api/v1/admin/oficios</c> (OFI-03), criterios CA-05 a CA-07.</summary>
public sealed partial class Spec045OficiosTests
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

    private static async Task<bool> CatalogManageAllowedAsync(IPanelPermissionGate gate)
    {
        var requirement = new PanelPermissionRequirement(PanelAuthentication.CatalogManagePermission);
        var claims = new[] { new Claim("sub", "cuenta-de-prueba"), new Claim("sid", "sesion-de-prueba"), new Claim("pv", "1") };
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), null);
        await new PanelPermissionHandler(gate, new HttpContextAccessor()).HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact]
    public async Task AdminList_ReturnsEveryTradeIncludingTheInactiveOnes_CA_045_05()
    {
        var store = new FakeTradeAdminStore();
        store.Trades.AddRange([Trade("plomeria", "Plomería"), Trade("electricidad", "Electricidad", active: false)]);

        var list = await new AdminTradesController(new TradeAdminService(store)).List(CancellationToken.None);

        var items = list.Value!.Items;
        items.Select(i => i.Clave).Should().Equal("electricidad", "plomeria");
        items.Single(i => i.Clave == "electricidad").Activo.Should().BeFalse();
        items[0].TopePorcentaje.Should().Be(30m);
        items[0].VisitaMaxima.Should().Be(450);
    }

    [Fact]
    public async Task AdminList_AllowsOnlyWhenThePanelGateGrantsCatalogManage_CA_045_05_CA_045_07()
    {
        var allowing = new StubGate(PanelDecision.Allowed);

        (await CatalogManageAllowedAsync(allowing)).Should().BeTrue();
        allowing.Permission.Should().Be("catalog:manage");
        (await CatalogManageAllowedAsync(new StubGate(PanelDecision.Denied))).Should().BeFalse("sin el permiso la política responde 403");
        (await CatalogManageAllowedAsync(new DenyAllPanelPermissionGate())).Should().BeFalse("sin la puerta real del panel nada se permite (falla cerrado)");
    }

    [Fact]
    public void TheAdminTradeRoutes_RequireThePanelSchemeAndTheCatalogManagePolicy_AndNeverTheAppToken_CA_045_06_CA_045_07()
    {
        var authorize = typeof(AdminTradesController).GetCustomAttribute<AuthorizeAttribute>();

        authorize.Should().NotBeNull();
        authorize!.AuthenticationSchemes.Should().Be(PanelAuthentication.Scheme);
        authorize.Policy.Should().Be(PanelAuthentication.CatalogManagePolicy);
        typeof(AdminTradesController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Should().OnlyContain(m => !m.GetCustomAttributes<AllowAnonymousAttribute>().Any(), "ninguna ruta de administración es pública");
    }

    [Fact]
    public void TheAdminTradeRoutes_AreNeverCached_CA_045_05()
    {
        typeof(AdminTradesController).GetCustomAttribute<PrivateNoStoreAttribute>().Should().NotBeNull();
    }
}
