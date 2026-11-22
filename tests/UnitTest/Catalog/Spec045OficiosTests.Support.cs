using AwesomeAssertions;
using Knock.Market.Api.Modules.Catalog.Data;
using Knock.Market.Api.Modules.Catalog.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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

        public Task<IReadOnlyList<AdminTrade>> ListAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AdminTrade>>([.. Trades.OrderBy(t => t.Nombre, StringComparer.Ordinal).ThenBy(t => t.Clave, StringComparer.Ordinal)]);
    }

    private static AdminTrade Trade(string key = "plomeria", string name = "Plomería", bool active = true) =>
        new(key, name, "domicilio", ["Fuga"], 30m, 10m, 150, 450, active);

    private static OficioListing Plomeria() => new("plomeria", "Plomería", "domicilio", ["Fuga en el baño", "Se tapó el drenaje"]);

    private static OficioListing Mecanica() => new("mecanica", "Mecánica automotriz", "taller");

    private static ControllerContext Context(HttpContext? http = null) => new() { HttpContext = http ?? new DefaultHttpContext() };

    private static T Ok<T>(IActionResult result) => (T)result.Should().BeOfType<OkObjectResult>().Which.Value!;
}
