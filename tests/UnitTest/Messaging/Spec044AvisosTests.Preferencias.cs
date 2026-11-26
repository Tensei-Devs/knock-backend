extern alias messaging;

using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Controllers = messaging::Knock.Messaging.Api.Modules.Notices.Controllers.V1;
using Domain = messaging::Knock.Messaging.Api.Modules.Notices.Domain;
using Dtos = messaging::Knock.Messaging.Api.Modules.Notices.Dtos;
using Validators = messaging::Knock.Messaging.Api.Modules.Notices.Validators;

namespace Knock.UnitTest.Messaging;

/// <summary>Spec 044, T-06: <c>GET</c> (PRE-01, CA-14, CA-15 y CA-19) y <c>PUT</c> (PRE-02, R-13 y CA-21) de <c>/api/v1/me/preferencias-avisos</c>.</summary>
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

    private static Dtos.UpdateNoticePreferencesRequest Change(params (string? Type, bool? On)[] changes) =>
        new([.. changes.Select(c => new Dtos.NoticePreferenceChange(c.Type, c.On))]);

    [Fact]
    public async Task UpdatePreferences_SavesOnlyTheListedTypesAndReturnsTheEffectiveOnes_PRE_02()
    {
        var store = new FakeStore();

        var result = PreferencesOf(await Preferencias(store).Update(Change(("opiniones", false), ("promociones", true)), CancellationToken.None));

        store.Preferences[_me].Should().BeEquivalentTo(new Dictionary<string, bool> { ["opiniones"] = false, ["promociones"] = true });
        result.Tipos.Single(t => t.Tipo == "opiniones").Activo.Should().BeFalse();
        result.Tipos.Single(t => t.Tipo == "promociones").Activo.Should().BeTrue();
        result.Tipos.Single(t => t.Tipo == "visitas").Activo.Should().BeTrue("lo que no se cambió conserva su valor");
    }

    [Fact]
    public async Task UpdatePreferences_KeepsEarlierChangesAndAcceptsSecurityOnlyAsOn_R_13()
    {
        var store = new FakeStore();
        store.Preferences[_me] = new Dictionary<string, bool> { ["mensajes"] = false };

        var result = PreferencesOf(await Preferencias(store).Update(Change(("cotizaciones", false), ("seguridad", true)), CancellationToken.None));

        store.Preferences[_me].Should().NotContainKey("seguridad", "la seguridad siempre está activa y no se guarda");
        result.Tipos.Single(t => t.Tipo == "mensajes").Activo.Should().BeFalse();
        result.Tipos.Single(t => t.Tipo == "cotizaciones").Activo.Should().BeFalse();
        result.Tipos.Single(t => t.Tipo == "seguridad").Activo.Should().BeTrue();
    }

    [Fact]
    public async Task UpdatePreferences_OnlyTouchesTheTokenPerson_CA_044_21()
    {
        var store = new FakeStore();
        store.Preferences[_other] = new Dictionary<string, bool> { ["visitas"] = true };

        await Preferencias(store).Update(Change(("visitas", false)), CancellationToken.None);

        store.Preferences[_other]["visitas"].Should().BeTrue("la preferencia de otra persona no cambia");
        store.LastUser.Should().Be(_me);
    }

    [Fact]
    public async Task UpdatePreferences_WithoutSub_ReturnsUnauthorizedAndWritesNothing_CA_044_15()
    {
        var store = new FakeStore();

        var result = await Preferencias(store, sub: null).Update(Change(("visitas", false)), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        store.SetCalls.Should().Be(0);
    }

    [Fact]
    public async Task UpdatePreferences_Repeated_LeavesTheSameState_CA_044_24()
    {
        var store = new FakeStore();

        await Preferencias(store).Update(Change(("visitas", false)), CancellationToken.None);
        await Preferencias(store).Update(Change(("visitas", false)), CancellationToken.None);

        store.Preferences.Should().HaveCount(1);
        store.Preferences[_me].Should().BeEquivalentTo(new Dictionary<string, bool> { ["visitas"] = false });
    }

    [Fact]
    public async Task UpdateValidator_AcceptsKnownTypes_PRE_02()
    {
        var result = await new Validators.UpdateNoticePreferencesRequestValidator().ValidateAsync(Change(("mensajes", false), ("seguridad", true), ("promociones", true)));

        result.IsValid.Should().BeTrue();
    }

    private static Dtos.UpdateNoticePreferencesRequest InvalidUpdate(string label) => label switch
    {
        "missing list" => new Dtos.UpdateNoticePreferencesRequest(null),
        "empty list" => new Dtos.UpdateNoticePreferencesRequest([]),
        "unknown type" => Change(("llamame-al-7711234567", true)),
        "missing type" => Change((null, true)),
        "missing value" => Change(("visitas", null)),
        "repeated type" => Change(("visitas", true), ("visitas", false)),
        "security off" => Change(("seguridad", false)),
        _ => Change([.. Domain.NoticeRules.Types.Append("extra").Select(t => ((string?)t, (bool?)true))]),
    };

    [Theory]
    [InlineData("missing list")]
    [InlineData("empty list")]
    [InlineData("unknown type")]
    [InlineData("missing type")]
    [InlineData("missing value")]
    [InlineData("repeated type")]
    [InlineData("security off")]
    [InlineData("too many entries")]
    public async Task UpdateValidator_RejectsInvalidBodiesWithoutEchoingTheValue_CA_044_03(string label)
    {
        var request = InvalidUpdate(label);
        var result = await new Validators.UpdateNoticePreferencesRequestValidator().ValidateAsync(request);

        result.IsValid.Should().BeFalse(label);
        result.Errors.Should().OnlyContain(e => !e.ErrorMessage.Contains("7711234567", StringComparison.Ordinal));
    }
}
