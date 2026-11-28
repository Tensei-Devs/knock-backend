using System.Net;
using AwesomeAssertions;
using Knock.SecurityTest.Support;

namespace Knock.SecurityTest.Market;

/// <summary>
/// Spec 052 (CA-20, CA-34): solo el técnico que participa en la solicitud guarda su bitácora y nadie más la lee. Mercado real en Production con MongoDB y Redis reales y el puerto real de
/// participación; quien ataca es un técnico con un envío activo en <b>otra</b> solicitud. Solo datos ficticios.
/// </summary>
public sealed class AccesoBitacoraSecurityTests
{
    private const string _marker = "Revisar la llave de paso zq7";

    [Fact]
    public async Task OnlyTheParticipatingTechnician_WritesAndReadsTheLog_CA_052_20_CA_052_34()
    {
        var host = await MarketAccessHost.SharedAsync();
        var scene = await host.SeedRequestAsync();
        var other = await host.SeedRequestAsync();
        await host.SeedSendAsync(scene);
        await host.SeedSendAsync(other);
        var path = $"/api/v1/asistente/bitacora/{scene.RequestId}";
        var body = new { sabe = new[] { _marker }, porConfirmar = new[] { "Horario" }, queLlevar = new[] { "Empaque" } };
        var attacker = TokenForge.Valid(other.Sub(Actor.Invited));
        var owner = TokenForge.Valid(scene.Sub(Actor.Invited));

        using var anonymousWrite = await host.SendAsync(HttpMethod.Put, path, null, body);
        using var foreignWrite = await host.SendAsync(HttpMethod.Put, path, attacker, body);
        using var clientWrite = await host.SendAsync(HttpMethod.Put, path, TokenForge.Valid(scene.Sub(Actor.Client)), body);

        await SecureHttp.AssertProblemAsync(anonymousWrite, HttpStatusCode.Unauthorized);
        await SecureHttp.AssertProblemAsync(foreignWrite, HttpStatusCode.Forbidden, MarketAccessHost.Address);
        await SecureHttp.AssertProblemAsync(clientWrite, HttpStatusCode.Forbidden, MarketAccessHost.Address);

        // Control: quien participa sí la guarda, y responde 201 porque lo rechazado no dejó nada.
        using var ownWrite = await host.SendAsync(HttpMethod.Put, path, owner, body);
        ownWrite.StatusCode.Should().Be(HttpStatusCode.Created, await ownWrite.Content.ReadAsStringAsync());

        using var anonymousRead = await host.SendAsync(HttpMethod.Get, path, null, null);
        using var foreignRead = await host.SendAsync(HttpMethod.Get, path, attacker, null);
        using var ownRead = await host.SendAsync(HttpMethod.Get, path, owner, null);

        await SecureHttp.AssertProblemAsync(anonymousRead, HttpStatusCode.Unauthorized, _marker);
        await SecureHttp.AssertProblemAsync(foreignRead, HttpStatusCode.NotFound, _marker, scene.Sub(Actor.Invited));
        ownRead.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ownRead.Content.ReadAsStringAsync()).Should().Contain(_marker);
    }
}
