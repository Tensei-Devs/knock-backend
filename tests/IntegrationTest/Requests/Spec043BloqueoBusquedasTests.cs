extern alias MarketApi;

using System.Net;
using AwesomeAssertions;
using MongoDB.Bson;
using Xunit;
using BlockStore = MarketApi::Knock.Market.Api.Modules.Requests.Data.BlockProjectionStore;

namespace Knock.IntegrationTest.Requests;

/// <summary>
/// Spec 043, R-16 y R-18 (T-08): con un bloqueo en cualquier sentido, las búsquedas con sesión (lista de técnicos y Explorar) no muestran al técnico y la lista de cotizaciones del cliente
/// no trae sus cotizaciones; al desbloquear, se restablece. Sin sesión nada cambia y la caché compartida no se contamina. MongoDB real; solo identificadores ficticios.
/// </summary>
public sealed class Spec043BloqueoBusquedasTests
{
    private static Task BlockAsync(Spec020Host host, string blocker, string blocked, bool active = true) =>
        new BlockStore(host.Database).ApplyAsync(blocker, blocked, active, DateTime.UtcNow, "evento-" + Guid.NewGuid().ToString("N"), CancellationToken.None);

    private static async Task<List<string>> IdsAsync(HttpResponseMessage response, params string[] path)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var node = await Spec020Host.ReadAsync(response);
        foreach (var name in path)
        {
            node = node.GetProperty(name);
        }

        return [.. node.EnumerateArray().Select(x => x.GetProperty("id").GetString()!)];
    }

    [Fact]
    public async Task ListaDeTecnicos_ConSesion_OcultaAlBloqueadoEnAmbosSentidos_YDesbloquearRestablece_CA_043_26()
    {
        await using var host = await Spec020Host.StartAsync();
        var (blockedByClient, blockedClient, free) = (await host.SeedPublicTechnicianAsync(), await host.SeedPublicTechnicianAsync(), await host.SeedPublicTechnicianAsync());
        var all = new[] { blockedByClient.Id, blockedClient.Id, free.Id }.Select(x => x.ToString()).ToList();
        await BlockAsync(host, host.ClientSub, blockedByClient.Sub);
        await BlockAsync(host, blockedClient.Sub, host.ClientSub);

        var signedIn = await IdsAsync(await host.SendAsync(HttpMethod.Get, "/api/v1/tecnicos", host.ClientSub), "tecnicos");
        var anonymous = await IdsAsync(await host.SendAsync(HttpMethod.Get, "/api/v1/tecnicos"), "tecnicos");
        var other = await IdsAsync(await host.SendAsync(HttpMethod.Get, "/api/v1/tecnicos", Guid.NewGuid().ToString("D")), "tecnicos");

        signedIn.Should().Equal(free.Id.ToString());
        anonymous.Should().BeEquivalentTo(all, "sin sesión no hay a quién cruzar y la caché compartida no guarda el filtro de nadie");
        other.Should().BeEquivalentTo(all, "el bloqueo de una persona no afecta a otra");

        await BlockAsync(host, host.ClientSub, blockedByClient.Sub, active: false);
        await BlockAsync(host, blockedClient.Sub, host.ClientSub, active: false);
        (await IdsAsync(await host.SendAsync(HttpMethod.Get, "/api/v1/tecnicos", host.ClientSub), "tecnicos")).Should().BeEquivalentTo(all, "desbloquear restablece");
    }

    [Fact]
    public async Task Explorar_ConSesion_OcultaAlBloqueadoEnMejorCalificados_CA_043_26()
    {
        await using var host = await Spec020Host.StartAsync();
        var (blocked, free) = (await host.SeedPublicTechnicianAsync(rating: 4.9), await host.SeedPublicTechnicianAsync(rating: 4.1));
        await BlockAsync(host, blocked.Sub, host.ClientSub);

        var signedIn = await IdsAsync(await host.SendAsync(HttpMethod.Get, "/api/v1/explorar?filtro=todo", host.ClientSub), "mejorCalificados");
        var anonymous = await IdsAsync(await host.SendAsync(HttpMethod.Get, "/api/v1/explorar?filtro=todo"), "mejorCalificados");

        signedIn.Should().Equal(free.Id.ToString());
        anonymous.Should().BeEquivalentTo(blocked.Id.ToString(), free.Id.ToString());

        await BlockAsync(host, blocked.Sub, host.ClientSub, active: false);
        (await IdsAsync(await host.SendAsync(HttpMethod.Get, "/api/v1/explorar?filtro=todo", host.ClientSub), "mejorCalificados")).Should().Contain(blocked.Id.ToString());
    }

    [Fact]
    public async Task ListaDeCotizacionesDelCliente_NoMuestraLasDeUnTecnicoBloqueado_CA_043_28()
    {
        await using var host = await Spec020Host.StartAsync();
        var (requestId, _, technicians) = await host.QuotedRequestAsync(3);
        await BlockAsync(host, host.ClientSub, technicians[0].Sub);
        await BlockAsync(host, technicians[1].Sub, host.ClientSub);

        using var response = await host.CompareAsync(requestId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await Spec020Host.ReadAsync(response);
        json.GetProperty("totalCotizaciones").GetInt32().Should().Be(1);
        json.GetProperty("cotizaciones").EnumerateArray().Select(q => q.GetProperty("tecnicoUsuarioId").GetString()).Should().Equal(technicians[2].Sub);
        (await host.CountAsync("cotizaciones", MongoDB.Driver.Builders<BsonDocument>.Filter.Eq("solicitudId", requestId))).Should().Be(3, "las cotizaciones no se borran: solo no se muestran");

        await BlockAsync(host, host.ClientSub, technicians[0].Sub, active: false);
        await BlockAsync(host, technicians[1].Sub, host.ClientSub, active: false);
        using var restored = await host.CompareAsync(requestId);
        (await Spec020Host.ReadAsync(restored)).GetProperty("totalCotizaciones").GetInt32().Should().Be(3, "desbloquear restablece");
    }

    [Fact]
    public async Task ListaDeCotizaciones_SinBloqueos_NoCambia_CA_043_28()
    {
        await using var host = await Spec020Host.StartAsync();
        var (requestId, _, _) = await host.QuotedRequestAsync(2);
        await BlockAsync(host, Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"));

        using var response = await host.CompareAsync(requestId);

        (await Spec020Host.ReadAsync(response)).GetProperty("totalCotizaciones").GetInt32().Should().Be(2);
    }
}
