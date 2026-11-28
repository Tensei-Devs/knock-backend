extern alias MarketApi;

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Requests;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Catalog;

/// <summary>Puerta de permisos de mentira: decide lo que la prueba le diga y recuerda qué permiso se le pidió.</summary>
internal sealed class FakePanelGate : IPanelPermissionGate
{
    public PanelDecision Decision { get; set; } = PanelDecision.Allowed;

    public string? LastPermission { get; private set; }

    public Task<PanelDecision> DecideAsync(string subject, string sessionId, string permissionsVersion, string permission, CancellationToken cancellationToken)
    {
        LastPermission = permission;
        return Task.FromResult(Decision);
    }
}

/// <summary>
/// Spec 045 (OFP-02 y LIS-02, HU-070): consultas del panel sobre oficios pedidos y lista de espera, con MongoDB real y la puerta de permisos reemplazada. El token del panel lleva otra llave y otro
/// destinatario que el de la app. Solo datos ficticios.
/// </summary>
public sealed class Spec045AdminHttpTests
{
    private const string _pedidos = "/api/v1/admin/oficios-pedidos";
    private const string _espera = "/api/v1/admin/lista-espera";
    private static readonly byte[] _panelKey = RandomNumberGenerator.GetBytes(32);

    private static Task<(Spec020Host Host, FakePanelGate Gate)> StartAsync() => PanelTestHost.StartAsync(_panelKey);

    private static string PanelToken(string? audience = PanelAuthentication.DefaultAudience, byte[]? key = null) => PanelTestHost.Token(key ?? _panelKey, audience);

    private static async Task<HttpResponseMessage> GetAsync(Spec020Host host, string path, string? bearer)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return await host.Client.SendAsync(request);
    }

    private static Task SeedTradeRequestsAsync(Spec020Host host, params (string Normalized, string Original)[] rows) =>
        host.Database.GetCollection<BsonDocument>("oficios_pedidos").InsertManyAsync(rows.Select(row => new BsonDocument
        {
            { "texto", row.Original }, { "textoNormalizado", row.Normalized }, { "usuarioId", Guid.NewGuid().ToString("D") }, { "creadoEn", DateTime.UtcNow },
        }));

    private static Task SeedWaitlistAsync(Spec020Host host, params string[] postalCodes) =>
        host.Database.GetCollection<BsonDocument>("listas_espera").InsertManyAsync(postalCodes.Select(cp => new BsonDocument
        {
            { "usuarioId", Guid.NewGuid().ToString("D") }, { "cp", cp }, { "municipio", "Piloto" }, { "creadoEn", DateTime.UtcNow },
        }));

    [Fact]
    public async Task Sin_Token_Del_Panel_Las_Dos_Rutas_Responden_401_CA_045_26_CA_045_30()
    {
        var (host, _) = await StartAsync();
        await using var disposable = host;

        using var sin = await GetAsync(host, _pedidos, null);
        using var app = await GetAsync(host, _pedidos, Spec020Host.Token(Guid.NewGuid().ToString("D")));
        using var otraLlave = await GetAsync(host, _espera, PanelToken(key: RandomNumberGenerator.GetBytes(32)));
        using var otroDestinatario = await GetAsync(host, _espera, PanelToken(audience: "knock-app"));

        sin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        app.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "el token de la app no sirve en el panel");
        otraLlave.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        otroDestinatario.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sin_El_Permiso_Responde_403_Y_No_Lee_Nada_CA_045_27_CA_045_31_CA_045_35()
    {
        var (host, gate) = await StartAsync();
        await using var disposable = host;
        gate.Decision = PanelDecision.Denied;

        using var pedidos = await GetAsync(host, _pedidos, PanelToken());
        using var espera = await GetAsync(host, _espera, PanelToken());

        pedidos.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        espera.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Spec020Host.ReadAsync(pedidos)).GetProperty("code").GetString().Should().Be(PanelAuthentication.InsufficientPermissionCode);
        gate.LastPermission.Should().Be(PanelAuthentication.MetricsReadPermission);
    }

    [Fact]
    public async Task Oficios_Pedidos_Se_Agrupan_Por_Texto_Normalizado_Y_Se_Cuentan_CA_045_25_CA_045_36()
    {
        var (host, _) = await StartAsync();
        await using var disposable = host;
        await SeedTradeRequestsAsync(host, ("plomero", "Plomero"), ("plomero", "plomero "), ("herrero", "Herrero"), ("plomero", "PLOMERO"), ("albanil", "Albañil"), ("herrero", "herrero"));

        using var response = await GetAsync(host, _pedidos, PanelToken());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var items = (await Spec020Host.ReadAsync(response)).GetProperty("items").EnumerateArray().ToList();
        items.Select(i => (i.GetProperty("texto").GetString(), i.GetProperty("cantidad").GetInt64())).Should().Equal(("plomero", 3L), ("herrero", 2L), ("albanil", 1L));
        items[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["texto", "cantidad"], "sin datos de quien lo pidió");
    }

    [Fact]
    public async Task Pagina_De_20_Con_Cursor_Y_Rechaza_Limites_Invalidos_CA_045_28_CA_045_32()
    {
        var (host, _) = await StartAsync();
        await using var disposable = host;
        await SeedTradeRequestsAsync(host, [.. Enumerable.Range(0, 25).Select(i => ($"oficio{i:00}", $"Oficio {i:00}"))]);
        await SeedWaitlistAsync(host, [.. Enumerable.Range(0, 25).Select(i => $"4{i:0000}")]);

        foreach (var route in new[] { _pedidos, _espera })
        {
            using var firstPage = await GetAsync(host, route, PanelToken());
            var first = await Spec020Host.ReadAsync(firstPage);
            var cursor = first.GetProperty("nextCursor").GetString()!;
            using var secondPage = await GetAsync(host, $"{route}?cursor={Uri.EscapeDataString(cursor)}", PanelToken());
            var second = await Spec020Host.ReadAsync(secondPage);
            using var tooMany = await GetAsync(host, $"{route}?limite=51", PanelToken());
            using var badCursor = await GetAsync(host, $"{route}?cursor=esto-no-es-un-cursor", PanelToken());

            first.GetProperty("items").GetArrayLength().Should().Be(20, route);
            second.GetProperty("items").GetArrayLength().Should().Be(5, route);
            second.TryGetProperty("nextCursor", out _).Should().BeFalse(route);
            tooMany.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, route);
            badCursor.StatusCode.Should().Be(HttpStatusCode.BadRequest, route);
        }
    }

    [Fact]
    public async Task Lista_De_Espera_Da_Conteos_Por_Codigo_Postal_Sin_Celulares_CA_045_29_CA_045_37()
    {
        var (host, _) = await StartAsync();
        await using var disposable = host;
        await host.Database.GetCollection<BsonDocument>("listas_espera").InsertOneAsync(new BsonDocument
        {
            { "usuarioId", "demo" }, { "cp", "42800" }, { "celular", "5512345678" }, { "creadoEn", DateTime.UtcNow },
        });
        await SeedWaitlistAsync(host, "42800", "42801", "42800");

        using var response = await GetAsync(host, _espera, PanelToken());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = await response.Content.ReadAsStringAsync();
        var items = JsonElement.Parse(text).GetProperty("items").EnumerateArray().ToList();
        items.Select(i => (i.GetProperty("codigoPostal").GetString(), i.GetProperty("cantidad").GetInt64())).Should().Equal(("42800", 3L), ("42801", 1L));
        text.Should().NotContain("celular").And.NotContain("5512345678");
    }
}
