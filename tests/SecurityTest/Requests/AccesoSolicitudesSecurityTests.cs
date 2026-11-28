using System.Net;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using Xunit;

namespace Knock.SecurityTest.Requests;

/// <summary>
/// Specs 020, 021, 028, 029 y 048: quién puede usar una solicitud y sus cotizaciones. Mercado real en Production, MongoDB y Redis reales, con la solicitud, la cotización y los técnicos
/// sembrados. Cada caso prueba sin token, token vencido, persona ajena y rol incorrecto, que nada cambie y que quien tiene derecho sí pueda. Solo datos ficticios.
/// </summary>
public sealed class AccesoSolicitudesSecurityTests
{
    private const string _base = "/api/v1/solicitudes/";

    private static readonly Actor[] _ownerAndQuoter = [Actor.Client, Actor.Chosen];
    private static readonly Actor[] _owner = [Actor.Client];
    private static readonly Actor[] _invited = [Actor.Invited];

    private static readonly Dictionary<string, AccessCase> _cases = new(StringComparer.Ordinal)
    {
        ["ver la solicitud"] = new("GET", s => _base + s.RequestId, AccessCase.NoBody, AccessCase.Nothing, _ownerAndQuoter, HttpStatusCode.OK, Strangers()),
        ["comparar cotizaciones"] = new("GET", s => Of(s, "cotizaciones"), AccessCase.NoBody, AccessCase.Nothing, _owner, HttpStatusCode.OK, NotTheOwner()),
        ["elegir cotizacion"] = new("POST", s => Of(s, "elegir"), s => new { cotizacionId = s.QuoteId.ToString() }, AccessCase.Nothing, _owner, HttpStatusCode.OK, NotTheOwner()),
        ["cancelar"] = new("POST", s => Of(s, "cancelar"), _ => new { motivo = "other" }, AccessCase.Nothing, _owner, HttpStatusCode.OK, NotTheOwner()),
        ["ampliar"] = new("POST", s => Of(s, "ampliar"), AccessCase.NoBody, AccessCase.Nothing, _owner, HttpStatusCode.OK, NotTheOwner()),
        ["enviar cotizacion"] = new(
            "POST",
            s => Of(s, "cotizaciones"),
            s => new { tipoPrecio = "fijo", precio = 900, dia = s.Day, hora = "11:00" },
            AccessCase.Nothing,
            _invited,
            HttpStatusCode.Created,
            OnlyAnEligibleTechnician()),
        ["descartar"] = new("POST", s => Of(s, "descartar"), AccessCase.NoBody, (host, s) => host.SeedSendAsync(s), _invited, HttpStatusCode.NoContent, OnlyAnEligibleTechnician()),
    };

    public static TheoryData<string> Cases() => AccessCase.Names(_cases);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task OnlyWhoHasTheRight_CanUseTheRequest(string name)
    {
        var host = await MarketAccessHost.SharedAsync();

        await _cases[name].VerifyAsync(host, await host.SeedRequestAsync());
    }

    [Fact]
    public async Task TheList_OnlyBringsTheCallersOwnRequests()
    {
        var host = await MarketAccessHost.SharedAsync();
        var mine = await host.SeedRequestAsync();
        var theirs = await host.SeedRequestAsync();

        using var response = await host.SendAsync(HttpMethod.Get, "/api/v1/solicitudes", TokenForge.Valid(mine.Sub(Actor.Client)), null);
        using var other = await host.SendAsync(HttpMethod.Get, "/api/v1/solicitudes", TokenForge.Valid(theirs.Sub(Actor.Client)), null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadAsStringAsync();
        list.Should().Contain(mine.RequestId.ToString()).And.NotContain(theirs.RequestId.ToString());
        (await other.Content.ReadAsStringAsync()).Should().Contain(theirs.RequestId.ToString(), "la solicitud ajena existe y su dueño la ve");
    }

    [Fact]
    public async Task TheTechnicianWhoQuoted_SeesTheRequestWithoutTheAddress()
    {
        var host = await MarketAccessHost.SharedAsync();
        var scene = await host.SeedRequestAsync();

        using var owner = await host.SendAsync(HttpMethod.Get, _base + scene.RequestId, TokenForge.Valid(scene.Sub(Actor.Client)), null);
        using var technician = await host.SendAsync(HttpMethod.Get, _base + scene.RequestId, TokenForge.Valid(scene.Sub(Actor.Chosen)), null);

        (await owner.Content.ReadAsStringAsync()).Should().Contain(MarketAccessHost.Address, "el dueño ve su dirección");
        technician.StatusCode.Should().Be(HttpStatusCode.OK);
        (await technician.Content.ReadAsStringAsync()).Should().NotContain(MarketAccessHost.Address).And.NotContain(scene.Sub(Actor.Client));
    }

    private static string Of(MarketScene scene, string action) => $"{_base}{scene.RequestId}/{action}";

    private static Dictionary<Actor, HttpStatusCode> Strangers() => new()
    {
        [Actor.OtherPerson] = HttpStatusCode.NotFound,
        [Actor.OtherTechnician] = HttpStatusCode.NotFound,
    };

    /// <summary>Rutas del cliente dueño: ni siquiera el técnico que cotizó.</summary>
    private static Dictionary<Actor, HttpStatusCode> NotTheOwner() => new(Strangers()) { [Actor.Chosen] = HttpStatusCode.NotFound };

    /// <summary>Rutas del técnico: quien no es técnico (también el dueño) recibe 403; un técnico al que no le corresponde, 404.</summary>
    private static Dictionary<Actor, HttpStatusCode> OnlyAnEligibleTechnician() => new()
    {
        [Actor.Client] = HttpStatusCode.Forbidden,
        [Actor.OtherPerson] = HttpStatusCode.Forbidden,
        [Actor.OtherTechnician] = HttpStatusCode.NotFound,
    };
}
