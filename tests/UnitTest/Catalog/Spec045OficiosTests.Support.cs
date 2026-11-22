using AwesomeAssertions;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Catalog.Controllers.V1;
using Knock.Market.Api.Modules.Catalog.Data;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Knock.Market.Api.Modules.Catalog.Domain;
using Knock.Market.Api.Modules.Catalog.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Knock.UnitTest.Catalog;

/// <summary>
/// Spec 045 (catálogo de oficios, oficios pedidos y lista de espera): validadores, reglas puras, servicios y controladores con almacenes de mentira. Lo que necesita MongoDB, Redis, el JWT real o el
/// limitador corre en <c>tests/DatabaseTest</c>, <c>tests/IntegrationTest</c> y <c>tests/SecurityTest</c> (CI). Solo datos ficticios. La clase es parcial: cada tarea de la spec agrega su archivo.
/// </summary>
public sealed partial class Spec045OficiosTests
{
    private sealed class FakeActiveOficios(params OficioListing[] items) : IActiveOficios
    {
        public int Reads { get; private set; }

        public int Invalidations { get; private set; }

        public Task InvalidateActiveOficiosAsync(CancellationToken cancellationToken)
        {
            Invalidations++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OficioListing>> FindActiveOficiosAsync(CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult<IReadOnlyList<OficioListing>>(items);
        }
    }

    private sealed class FakeTradeAdminStore : ITradeAdminStore
    {
        public List<AdminTrade> Trades { get; } = [];

        public int Inserts { get; private set; }

        public Task<AdminTrade?> UpdateAsync(string clave, TradeChanges changes, CancellationToken cancellationToken)
        {
            var index = Trades.FindIndex(t => t.Clave == clave);
            if (index < 0)
            {
                return Task.FromResult<AdminTrade?>(null);
            }

            var current = Trades[index];
            Trades[index] = current with
            {
                Nombre = changes.Nombre ?? current.Nombre,
                Modalidad = changes.Modalidad ?? current.Modalidad,
                FrasesSugeridas = changes.FrasesSugeridas ?? current.FrasesSugeridas,
                TopePorcentaje = changes.TopePorcentaje ?? current.TopePorcentaje,
                TopeFijoPorcentaje = changes.TopeFijoPorcentaje ?? current.TopeFijoPorcentaje,
                PisoAprobacion = changes.PisoAprobacion ?? current.PisoAprobacion,
                VisitaMaxima = changes.VisitaMaxima ?? current.VisitaMaxima,
                Activo = changes.Activo ?? current.Activo,
            };
            return Task.FromResult<AdminTrade?>(Trades[index]);
        }

        public Task<bool> InsertAsync(NewTrade trade, CancellationToken cancellationToken)
        {
            if (Trades.Any(t => t.Clave == trade.Clave))
            {
                return Task.FromResult(false);
            }

            Inserts++;
            Trades.Add(new AdminTrade(trade.Clave, trade.Nombre, trade.Modalidad, trade.FrasesSugeridas, trade.TopePorcentaje, trade.TopeFijoPorcentaje, trade.PisoAprobacion, trade.VisitaMaxima, trade.Activo));
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<AdminTrade>> ListAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AdminTrade>>([.. Trades.OrderBy(t => t.Nombre, StringComparer.Ordinal).ThenBy(t => t.Clave, StringComparer.Ordinal)]);
    }

    private sealed class FakeAudit(bool available = true) : IPanelAuditTrail
    {
        public List<PanelAuditEntry> Entries { get; } = [];

        public Task<bool> RecordAsync(PanelAuditEntry entry, CancellationToken cancellationToken)
        {
            if (!available)
            {
                return Task.FromResult(false);
            }

            Entries.Add(entry);
            return Task.FromResult(true);
        }
    }

    private sealed class TestProblemFactory : ProblemDetailsFactory
    {
        public override ProblemDetails CreateProblemDetails(HttpContext httpContext, int? statusCode = null, string? title = null, string? type = null, string? detail = null, string? instance = null) =>
            new() { Status = statusCode, Title = title, Type = type, Detail = detail, Instance = instance };

        public override ValidationProblemDetails CreateValidationProblemDetails(
            HttpContext httpContext, Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelStateDictionary, int? statusCode = null, string? title = null, string? type = null, string? detail = null, string? instance = null) =>
            new(modelStateDictionary) { Status = statusCode, Title = title, Type = type, Detail = detail, Instance = instance };
    }

    private static readonly DateTimeOffset _now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private static TradeAdminService AdminService(FakeTradeAdminStore store, FakeActiveOficios? catalog = null, FakeAudit? audit = null) =>
        new(store, catalog ?? new FakeActiveOficios(), audit ?? new FakeAudit(), new FixedClock());

    private static AdminTradesController Admin(TradeAdminService service, string? sub = "cuenta-de-prueba")
    {
        var identity = sub is null ? new System.Security.Claims.ClaimsIdentity() : new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("sub", sub)], "test");
        var http = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(identity) };
        return new AdminTradesController(service) { ControllerContext = Context(http), ProblemDetailsFactory = new TestProblemFactory() };
    }

    private static CreateTradeBody NewBody(string key = "gas") =>
        new(key, "Instalaciones de gas", "ambos", ["Huele a gas"], 25m, 10m, 200, 500, null);

    private static AdminTrade Trade(string key = "plomeria", string name = "Plomería", bool active = true) =>
        new(key, name, "domicilio", ["Fuga"], 30m, 10m, 150, 450, active);

    private static OficioListing Plomeria() => new("plomeria", "Plomería", "domicilio", ["Fuga en el baño", "Se tapó el drenaje"]);

    private static OficioListing Mecanica() => new("mecanica", "Mecánica automotriz", "taller");

    private static ControllerContext Context(HttpContext? http = null) => new() { HttpContext = http ?? new DefaultHttpContext() };

    private static T Ok<T>(IActionResult result) => (T)result.Should().BeOfType<OkObjectResult>().Which.Value!;
}
