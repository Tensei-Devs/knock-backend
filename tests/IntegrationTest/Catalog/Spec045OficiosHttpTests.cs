extern alias MarketApi;

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
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

/// <summary>
/// Spec 045 sobre HTTP real con MongoDB (y la puerta de permisos y la auditoría del panel reemplazadas): detalle público de un oficio (OFI-02), administración del catálogo (OFI-03 a OFI-05) y «Otro oficio»
/// (OFP-01). Corre solo en CI (Docker). Solo datos ficticios.
/// </summary>
public sealed class Spec045OficiosHttpTests
{
    private const string _public = "/api/v1/oficios";
    private const string _admin = "/api/v1/admin/oficios";
    private const string _requests = "/api/v1/oficios-pedidos";
    private static readonly byte[] _panelKey = RandomNumberGenerator.GetBytes(32);

    private sealed class RecordingAudit(bool available) : IPanelAuditTrail
    {
        public List<PanelAuditEntry> Entries { get; } = [];

        public Task<bool> RecordAsync(PanelAuditEntry entry, CancellationToken cancellationToken)
        {
            if (available)
            {
                Entries.Add(entry);
            }

            return Task.FromResult(available);
        }
    }

    private static async Task<(Spec020Host Host, FakePanelGate Gate, RecordingAudit Audit)> StartAsync(bool auditAvailable = true)
    {
        var gate = new FakePanelGate();
        var audit = new RecordingAudit(auditAvailable);
        var host = await Spec020Host.StartAsync(
            new Dictionary<string, string?> { [PanelAuthentication.KeyConfigurationKey] = Convert.ToBase64String(_panelKey) },
            services =>
            {
                services.AddSingleton<IPanelPermissionGate>(gate);
                services.AddSingleton<IPanelAuditTrail>(audit);
            });
        return (host, gate, audit);
    }

    private static string PanelToken(string? audience = PanelAuthentication.DefaultAudience) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = GatewayTestSupport.Issuer,
            Audience = audience,
            Claims = new Dictionary<string, object> { ["sub"] = "panel-de-prueba", ["sid"] = "sesion-de-prueba", ["pv"] = "1" },
            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_panelKey), SecurityAlgorithms.HmacSha256),
        });

    private static async Task<HttpResponseMessage> PanelAsync(Spec020Host host, HttpMethod method, string path, string? bearer = null, object? body = null, string? raw = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        if (body is not null || raw is not null)
        {
            request.Content = new StringContent(raw ?? JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        return await host.Client.SendAsync(request);
    }

    private static object NewTrade(string key = "gas") => new
    {
        clave = key, nombre = "Instalaciones de gas", modalidad = "ambos", frasesSugeridas = new[] { "Huele a gas" },
        topePorcentaje = 25, topeFijoPorcentaje = 10, pisoAprobacion = 200, visitaMaxima = 500,
    };

    [Fact]
    public async Task Detail_IsPublic_ReturnsTheTrade_AndRevalidatesWithETag_CA_045_01()
    {
        var (host, _, _) = await StartAsync();
        await using var disposable = host;

        using var response = await host.SendAsync(HttpMethod.Get, $"{_public}/{Spec020Host.Plumbing}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await Spec020Host.ReadAsync(response);
        body.GetProperty("clave").GetString().Should().Be(Spec020Host.Plumbing);
        body.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("clave", "nombre", "modalidad", "frasesSugeridas");
        using var again = new HttpRequestMessage(HttpMethod.Get, $"{_public}/{Spec020Host.Plumbing}");
        again.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(response.Headers.ETag!.ToString()));
        using var notModified = await host.Client.SendAsync(again);
        notModified.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task Detail_WithAnInvalidKey_Answers422_AndAnUnknownOrInactiveOne_404_CA_045_02_CA_045_03_CA_045_34()
    {
        var (host, _, _) = await StartAsync();
        await using var disposable = host;
        await host.SeedTradeAsync("pausa", "En pausa", "taller", active: false);

        using var invalid = await host.SendAsync(HttpMethod.Get, $"{_public}/Con%20Espacios");
        using var unknown = await host.SendAsync(HttpMethod.Get, $"{_public}/no-existe");
        using var inactive = await host.SendAsync(HttpMethod.Get, $"{_public}/pausa");

        invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await invalid.Content.ReadAsStringAsync()).Should().NotContain("Con Espacios");
        foreach (var response in new[] { unknown, inactive })
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("trade_not_found");
        }
    }

    [Fact]
    public async Task AdminRoutes_WithoutThePanelToken_Answer401_AndWithoutThePermission_403_CA_045_06_CA_045_07_CA_045_09_CA_045_10_CA_045_15_CA_045_16()
    {
        var (host, gate, _) = await StartAsync();
        await using var disposable = host;
        var routes = new[] { (HttpMethod.Get, _admin), (HttpMethod.Post, _admin), (HttpMethod.Put, $"{_admin}/gas") };

        foreach (var (method, path) in routes)
        {
            using var none = await PanelAsync(host, method, path, null, NewTrade());
            using var app = await PanelAsync(host, method, path, Spec020Host.Token(Guid.NewGuid().ToString("D")), NewTrade());
            none.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"{method} {path}");
            app.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "el token de la app no sirve en el panel");
        }

        gate.Decision = PanelDecision.Denied;
        foreach (var (method, path) in routes)
        {
            using var denied = await PanelAsync(host, method, path, PanelToken(), NewTrade());
            denied.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {path}");
        }

        gate.LastPermission.Should().Be(PanelAuthentication.CatalogManagePermission);
    }

    [Fact]
    public async Task AdminList_ShowsInactiveTradesToo_CA_045_05()
    {
        var (host, _, _) = await StartAsync();
        await using var disposable = host;
        await host.SeedTradeAsync("pausa", "En pausa", "taller", active: false);

        using var response = await PanelAsync(host, HttpMethod.Get, _admin, PanelToken());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var items = (await Spec020Host.ReadAsync(response)).GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("clave").GetString()).Should().Contain(["plo", "mec", "pausa"]);
        items.Single(i => i.GetProperty("clave").GetString() == "pausa").GetProperty("activo").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Create_Returns201_Audits_AndTheRepeatedKeyIs409_CA_045_08_CA_045_13_CA_045_41()
    {
        var (host, _, audit) = await StartAsync();
        await using var disposable = host;

        using var created = await PanelAsync(host, HttpMethod.Post, _admin, PanelToken(), NewTrade());
        using var repeated = await PanelAsync(host, HttpMethod.Post, _admin, PanelToken(), NewTrade());

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location!.ToString().Should().Be($"{_public}/gas");
        repeated.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Spec020Host.ReadAsync(repeated)).GetProperty("code").GetString().Should().Be("trade_key_taken");
        (await host.Database.GetCollection<BsonDocument>("oficios").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("clave", "gas"))).Should().Be(1);
        audit.Entries.Should().NotBeEmpty().And.OnlyContain(e => e.Subject == "panel-de-prueba" && e.Action == "trade.create");
    }

    [Fact]
    public async Task Create_WithoutTheAuditTrail_Answers503AndWritesNothing_CA_045_35()
    {
        var (host, _, _) = await StartAsync(auditAvailable: false);
        await using var disposable = host;

        using var response = await PanelAsync(host, HttpMethod.Post, _admin, PanelToken(), NewTrade());

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("panel_audit_unavailable");
        (await host.Database.GetCollection<BsonDocument>("oficios").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("clave", "gas"))).Should().Be(0);
    }

    [Fact]
    public async Task CreateAndUpdate_WithAnInvalidValueOrAnUnreadableBody_Answer422Or400AndSaveNothing_CA_045_11_CA_045_12_CA_045_17_CA_045_18()
    {
        var (host, _, _) = await StartAsync();
        await using var disposable = host;

        using var invalidCreate = await PanelAsync(host, HttpMethod.Post, _admin, PanelToken(), new { clave = "Con Espacios", nombre = new string('x', 41), modalidad = "remoto", topePorcentaje = 120, topeFijoPorcentaje = 1, pisoAprobacion = 1, visitaMaxima = 1 });
        using var invalidUpdate = await PanelAsync(host, HttpMethod.Put, $"{_admin}/plo", PanelToken(), new { topePorcentaje = 101 });
        using var unreadableCreate = await PanelAsync(host, HttpMethod.Post, _admin, PanelToken(), raw: "{no es json");
        using var unreadableUpdate = await PanelAsync(host, HttpMethod.Put, $"{_admin}/plo", PanelToken(), raw: "{no es json");

        invalidCreate.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var errors = (await Spec020Host.ReadAsync(invalidCreate)).GetProperty("errors").EnumerateObject().Select(p => p.Name).ToList();
        errors.Should().Contain(["clave", "nombre", "modalidad", "topePorcentaje"]);
        invalidUpdate.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        unreadableCreate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        unreadableUpdate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await host.Database.GetCollection<BsonDocument>("oficios").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(2);
    }

    [Fact]
    public async Task Update_ChangesTheTrade_NeverTheKey_AndTheNextPublicReadSeesIt_CA_045_14_CA_045_19_CA_045_33_CA_045_34()
    {
        var (host, _, _) = await StartAsync();
        await using var disposable = host;
        using var before = await host.SendAsync(HttpMethod.Get, $"{_public}/plo");

        using var updated = await PanelAsync(host, HttpMethod.Put, $"{_admin}/plo", PanelToken(), new { nombre = "Plomería y gas", topePorcentaje = 40 });
        using var keyChange = await PanelAsync(host, HttpMethod.Put, $"{_admin}/plo", PanelToken(), new { clave = "otra", nombre = "X" });
        using var unknown = await PanelAsync(host, HttpMethod.Put, $"{_admin}/no-existe", PanelToken(), new { nombre = "X" });
        using var after = await host.SendAsync(HttpMethod.Get, $"{_public}/plo");

        before.StatusCode.Should().Be(HttpStatusCode.OK);
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Spec020Host.ReadAsync(updated)).GetProperty("clave").GetString().Should().Be("plo");
        keyChange.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Spec020Host.ReadAsync(unknown)).GetProperty("code").GetString().Should().Be("trade_not_found");
        (await Spec020Host.ReadAsync(after)).GetProperty("nombre").GetString().Should().Be("Plomería y gas");
    }

    [Fact]
    public async Task TradeRequest_OfATechnician_Returns201_RepeatingItDoesNotDuplicate_AndANonTechnicianGets404_CA_045_20_CA_045_22_CA_045_36_CA_045_38_CA_045_41()
    {
        var (host, _, _) = await StartAsync();
        await using var disposable = host;
        var technician = Guid.NewGuid().ToString("D");
        await host.SeedTechnicianAsync(technician, "activo", hidden: false, [Spec020Host.Plumbing], host.ColoniaId);

        using var first = await host.SendAsync(HttpMethod.Post, _requests, technician, new { texto = "Plomero" });
        using var repeated = await host.SendAsync(HttpMethod.Post, _requests, technician, new { texto = "plomero ", usuarioId = "otra-persona" });
        using var notTechnician = await host.SendAsync(HttpMethod.Post, _requests, host.ClientSub, new { texto = "Plomero" });

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        repeated.StatusCode.Should().Be(HttpStatusCode.Created);
        (await Spec020Host.ReadAsync(repeated)).GetProperty("id").GetString().Should().Be((await Spec020Host.ReadAsync(first)).GetProperty("id").GetString());
        notTechnician.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var saved = await host.Database.GetCollection<BsonDocument>("oficios_pedidos").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        saved.Should().ContainSingle().Which["usuarioId"].AsString.Should().Be(technician, "la persona es la del sub del token, no la del cuerpo");
        saved[0]["textoNormalizado"].AsString.Should().Be("plomero");
    }

    [Fact]
    public async Task TradeRequest_WithoutToken_Answers401_AnInvalidTextOrUnreadableBody_422Or400_CA_045_21_CA_045_23_CA_045_24()
    {
        var (host, _, _) = await StartAsync();
        await using var disposable = host;
        var technician = Guid.NewGuid().ToString("D");
        await host.SeedTechnicianAsync(technician, "activo", hidden: false, [Spec020Host.Plumbing], host.ColoniaId);

        using var none = await host.SendAsync(HttpMethod.Post, _requests, null, new { texto = "Plomero" });
        using var tooShort = await host.SendAsync(HttpMethod.Post, _requests, technician, new { texto = "a" });
        using var tooLong = await host.SendAsync(HttpMethod.Post, _requests, technician, new { texto = new string('a', 61) });
        using var contact = await host.SendAsync(HttpMethod.Post, _requests, technician, new { texto = "llama al 7711234567" });
        using var unreadable = await host.SendAsync(HttpMethod.Post, _requests, technician, rawJson: "{no es json");

        none.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        foreach (var response in new[] { tooShort, tooLong, contact })
        {
            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await response.Content.ReadAsStringAsync()).Should().NotContain("7711234567");
        }

        unreadable.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await host.Database.GetCollection<BsonDocument>("oficios_pedidos").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(0);
    }

    [Fact]
    public async Task TradeRequest_OverTheLimitAnswers429WithRetryAfter_AndAnOversizedBody413_CA_045_40()
    {
        var (host, _, _) = await StartAsync();
        await using var disposable = host;
        var technician = Guid.NewGuid().ToString("D");
        await host.SeedTechnicianAsync(technician, "activo", hidden: false, [Spec020Host.Plumbing], host.ColoniaId);

        using var oversized = await host.SendAsync(HttpMethod.Post, _requests, technician, new { texto = new string('a', 4096) });
        HttpResponseMessage? limited = null;
        for (var i = 0; i < 8 && limited is null; i++)
        {
            var response = await host.SendAsync(HttpMethod.Post, _requests, technician, new { texto = $"Oficio {i}" });
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                limited = response;
            }
            else
            {
                response.Dispose();
            }
        }

        oversized.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        limited.Should().NotBeNull("la sexta petición por hora de la misma persona recibe 429");
        limited.Headers.GetValues("Retry-After").Should().ContainSingle();
        limited.Dispose();
    }
}
