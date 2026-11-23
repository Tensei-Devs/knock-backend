using AwesomeAssertions;
using Knock.Identity.Api.Modules.Panel.Authorization;
using Knock.Identity.Api.Modules.Panel.Controllers.V1;
using Knock.Identity.Api.Modules.Panel.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Knock.UnitTest.Panel;

/// <summary>
/// Spec 061 (R-03, CA-05, CA-06): la consulta interna de permisos que usa Mercado decide con los roles vigentes, igual que la petición local de Identidad (spec 033, R-21), y falla cerrado. El doble factor
/// no es un claim: la sesión del panel solo existe después de verificar el TOTP, así que sin segundo factor no hay token que presentar.
/// </summary>
public class Spec061PermisoPanelServicioTests
{
    private static readonly byte[] _secret = System.Text.Encoding.ASCII.GetBytes("12345678901234567890");

    private sealed class Holder : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = new DefaultHttpContext();
    }

    private static (PermisoPanelHandler Decider, PanelWorld World) Create(string[]? roles = null)
    {
        var world = new PanelWorld();
        world.SeedAccount(roles: roles, secret: _secret);
        return (new PermisoPanelHandler(world.Cache, world.Accounts, world.Sessions, world.Cancelled, world.Audit, new Holder()), world);
    }

    private static async Task<string> OpenSessionAsync(PanelWorld world)
    {
        var phone = (PhoneOutcome.Challenge)await world.Access.VerifyPhoneAsync(world.TokenFor(), "203.0.113.7", CancellationToken.None);
        await world.Access.VerifyTotpAsync(phone.ChallengeId, world.CurrentCode(_secret), "203.0.113.7", CancellationToken.None);
        return world.Sessions.Sessions.Single().Id;
    }

    [Fact]
    public async Task Moderador_Con_Sesion_Tiene_Reviews_Moderate_Y_Verificador_No_CA_061_05()
    {
        var (moderator, moderatorWorld) = Create(["moderador"]);
        var moderatorSid = await OpenSessionAsync(moderatorWorld);
        var (verifier, verifierWorld) = Create(["verificador"]);
        var verifierSid = await OpenSessionAsync(verifierWorld);

        (await moderator.IsAllowedAsync(PanelWorld.AppSub, moderatorSid, 1, "reviews:moderate", CancellationToken.None)).Should().BeTrue();
        (await verifier.IsAllowedAsync(PanelWorld.AppSub, verifierSid, 1, "reviews:moderate", CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task Version_Distinta_Cuenta_Inexistente_O_Permiso_Desconocido_Se_Deniega_CA_061_05()
    {
        var (decider, world) = Create(["moderador"]);
        var sid = await OpenSessionAsync(world);

        (await decider.IsAllowedAsync(PanelWorld.AppSub, sid, 2, "reviews:moderate", CancellationToken.None)).Should().BeFalse("la versión de permisos cambió");
        (await decider.IsAllowedAsync("sub-que-no-existe", sid, 1, "reviews:moderate", CancellationToken.None)).Should().BeFalse();
        (await decider.IsAllowedAsync(PanelWorld.AppSub, sid, 1, "otro:permiso", CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task Sin_Segundo_Factor_No_Hay_Sesion_Ni_Token_Para_Presentar_CA_061_06()
    {
        var (_, world) = Create(["moderador"]);

        _ = (PhoneOutcome.Challenge)await world.Access.VerifyPhoneAsync(world.TokenFor(), "203.0.113.7", CancellationToken.None);

        world.Sessions.Sessions.Should().BeEmpty("Identidad solo abre la sesión del panel tras verificar el TOTP");
    }

    [Theory]
    [InlineData(null, "s", 1, "reviews:moderate")]
    [InlineData("a", "", 1, "reviews:moderate")]
    [InlineData("a", "s", -1, "reviews:moderate")]
    [InlineData("a", "s", 1, "REVIEWS:MODERATE")]
    [InlineData("a|b", "s", 1, "reviews:moderate")]
    public void Consulta_Mal_Formada_Es_Invalida_Sin_Repetir_El_Valor_CA_061_05(string? sub, string sid, int version, string permission)
    {
        new PanelPermissionCheckBodyValidator().Validate(new PanelPermissionCheckBody(sub, sid, version, permission)).IsValid.Should().BeFalse();
        new PanelPermissionCheckBodyValidator().Validate(new PanelPermissionCheckBody("a", "s", 1, "reviews:moderate")).IsValid.Should().BeTrue();
    }
}
