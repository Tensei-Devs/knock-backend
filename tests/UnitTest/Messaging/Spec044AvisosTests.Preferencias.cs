extern alias messaging;

using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Controllers = messaging::Knock.Messaging.Api.Modules.Notices.Controllers.V1;
using Domain = messaging::Knock.Messaging.Api.Modules.Notices.Domain;
using Dtos = messaging::Knock.Messaging.Api.Modules.Notices.Dtos;

namespace Knock.UnitTest.Messaging;

/// <summary>Spec 044, T-06: <c>GET /api/v1/me/preferencias-avisos</c> (PRE-01), criterios CA-14, CA-15 y CA-19.</summary>
public sealed partial class Spec044AvisosTests
{
    private static Controllers.MePreferenciasAvisosController Preferencias(FakeStore store, string? sub = _me) =>
        Controller(s => new Controllers.MePreferenciasAvisosController(s), store, sub);

    private static Dtos.NoticePreferencesResponse PreferencesOf(IActionResult result) =>
        (Dtos.NoticePreferencesResponse)result.Should().BeOfType<OkObjectResult>().Which.Value!;

    [Fact]
    public async Task Preferences_WithoutSavedChanges_AreAllOnExceptPromotionsAndSecurityCannotBeTurnedOff_CA_044_14()
    {
        var store = new FakeStore();

        var result = PreferencesOf(await Preferencias(store).Get(CancellationToken.None));

        result.Tipos.Select(t => t.Tipo).Should().Equal(Domain.NoticeRules.Types);
        result.Tipos.Where(t => t.Tipo != "promociones").Should().OnlyContain(t => t.Activo);
        result.Tipos.Single(t => t.Tipo == "promociones").Activo.Should().BeFalse();
        result.Tipos.Single(t => t.Tipo == "seguridad").Apagable.Should().BeFalse();
        result.Tipos.Where(t => t.Tipo != "seguridad").Should().OnlyContain(t => t.Apagable);
    }

    [Fact]
    public async Task Preferences_WithoutSub_ReturnsUnauthorizedAndTouchesNothing_CA_044_15()
    {
        var store = new FakeStore();

        var result = await Preferencias(store, sub: null).Get(CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        store.LastUser.Should().BeNull();
    }

    [Fact]
    public async Task Preferences_SavedValuesApply_ButSecurityIsAlwaysOn_CA_044_19()
    {
        var store = new FakeStore();
        store.Preferences[_me] = new Dictionary<string, bool> { ["opiniones"] = false, ["promociones"] = true, ["seguridad"] = false };

        var result = PreferencesOf(await Preferencias(store).Get(CancellationToken.None));

        result.Tipos.Single(t => t.Tipo == "opiniones").Activo.Should().BeFalse();
        result.Tipos.Single(t => t.Tipo == "promociones").Activo.Should().BeTrue();
        result.Tipos.Single(t => t.Tipo == "seguridad").Activo.Should().BeTrue("la seguridad no se apaga");
    }

    [Fact]
    public async Task Preferences_OfAnotherPersonDoNotLeak_CA_044_19()
    {
        var store = new FakeStore();
        store.Preferences[_other] = new Dictionary<string, bool> { ["cotizaciones"] = false };

        var result = PreferencesOf(await Preferencias(store).Get(CancellationToken.None));

        result.Tipos.Single(t => t.Tipo == "cotizaciones").Activo.Should().BeTrue();
        store.LastUser.Should().Be(_me);
    }
}
