using System.Net;
using AwesomeAssertions;
using Knock.SecurityTest.Support;

namespace Knock.SecurityTest.Requests;

/// <summary>Spec 050: la agenda de un técnico no trae las visitas de otro. Cada una tiene su visita sembrada y la ve en su propia agenda. Solo datos ficticios.</summary>
public sealed class AccesoAgendaSecurityTests
{
    private const string _agenda = "/api/v1/agenda";

    [Fact]
    public async Task TheAgenda_OnlyBringsTheCallersOwnVisits_CA_050_01()
    {
        var host = await MarketAccessHost.SharedAsync();
        var mine = await host.SeedVisitAsync();
        var theirs = await host.SeedVisitAsync();

        using var anonymous = await host.SendAsync(HttpMethod.Get, _agenda, null, null);
        using var response = await host.SendAsync(HttpMethod.Get, _agenda, TokenForge.Valid(mine.Sub(Actor.Chosen)), null);
        using var other = await host.SendAsync(HttpMethod.Get, _agenda, TokenForge.Valid(theirs.Sub(Actor.Chosen)), null);

        await SecureHttp.AssertProblemAsync(anonymous, HttpStatusCode.Unauthorized);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain(mine.VisitId.ToString()).And.NotContain(theirs.VisitId.ToString());
        (await other.Content.ReadAsStringAsync()).Should().Contain(theirs.VisitId.ToString(), "la visita ajena existe y su técnico la ve");
    }
}
