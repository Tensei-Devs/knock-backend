using System.Reflection;
using AwesomeAssertions;
using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Modules.Catalog.Domain;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Knock.Market.Api.Modules.Catalog.Validators;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Knock.UnitTest.Catalog;

/// <summary>Spec 045, T-05: <c>PUT /api/v1/admin/oficios/{clave}</c> (OFI-05), criterios CA-14 a CA-19.</summary>
public sealed partial class Spec045OficiosTests
{
    private static UpdateTradeBody Edit() => new(null, null, null, null, null, null, null, null, null);

    private static async Task<IActionResult> PutAsync(AdminTradesControllerFixture fixture, string key, UpdateTradeBody body) =>
        await fixture.Controller.Update(new TradeKeyRoute { Clave = key }, body, CancellationToken.None);

    private sealed class AdminTradesControllerFixture
    {
        public AdminTradesControllerFixture(string? sub = "cuenta-de-prueba", FakeAudit? audit = null)
        {
            Store.Trades.Add(Trade("plomeria", "Plomería"));
            Audit = audit ?? new FakeAudit();
            Controller = Admin(AdminService(Store, Catalog, Audit), sub);
        }

        public FakeTradeAdminStore Store { get; } = new();

        public FakeActiveOficios Catalog { get; } = new();

        public FakeAudit Audit { get; }

        public Knock.Market.Api.Modules.Catalog.Controllers.V1.AdminTradesController Controller { get; }
    }

    [Fact]
    public async Task Update_ChangesOnlyTheSentFields_KeepsTheKey_AndInvalidatesTheCache_CA_045_14_CA_045_33()
    {
        var fixture = new AdminTradesControllerFixture();

        var result = await PutAsync(fixture, "plomeria", Edit() with { Nombre = "Plomería y gas", TopePorcentaje = 40m, Clave = "plomeria" });

        var trade = (AdminTradeResponse)result.Should().BeOfType<OkObjectResult>().Which.Value!;
        trade.Clave.Should().Be("plomeria");
        trade.Nombre.Should().Be("Plomería y gas");
        trade.TopePorcentaje.Should().Be(40m);
        trade.TopeFijoPorcentaje.Should().Be(10m, "lo que no se envía no cambia");
        trade.Modalidad.Should().Be("domicilio");
        fixture.Catalog.Invalidations.Should().Be(1, "la siguiente consulta pública debe ver el cambio");
    }

    [Fact]
    public async Task Update_LeavesTheAuditEntryWithTheChangedFields_CA_045_14_CA_045_35()
    {
        var fixture = new AdminTradesControllerFixture(sub: "panel-7");

        await PutAsync(fixture, "plomeria", Edit() with { VisitaMaxima = 600, Activo = false });

        var entry = fixture.Audit.Entries.Should().ContainSingle().Subject;
        entry.Subject.Should().Be("panel-7");
        entry.Action.Should().Be("trade.update");
        entry.Resource.Should().Be("plomeria");
        entry.Changes.Keys.Should().BeEquivalentTo("visitaMaxima", "activo");
        entry.Changes["activo"].Should().Be("false");
    }

    [Fact]
    public async Task Update_WithoutSub_Returns401AndChangesNothing_CA_045_15()
    {
        var fixture = new AdminTradesControllerFixture(sub: null);

        var result = await PutAsync(fixture, "plomeria", Edit() with { Nombre = "Otro" });

        result.Should().BeOfType<UnauthorizedResult>();
        fixture.Store.Trades.Single().Nombre.Should().Be("Plomería");
    }

    [Fact]
    public async Task Update_AllowsOnlyWhenThePanelGateGrantsCatalogManage_CA_045_16()
    {
        (await CatalogManageAllowedAsync(new StubGate(Knock.Market.Api.Hosting.PanelDecision.Denied))).Should().BeFalse();
        typeof(Knock.Market.Api.Modules.Catalog.Controllers.V1.AdminTradesController).GetMethod("Update")!
            .GetCustomAttributes<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>().Should().BeEmpty();
    }

    [Theory]
    [InlineData("nombre")]
    [InlineData("modalidad")]
    [InlineData("topePorcentaje")]
    [InlineData("topeFijoPorcentaje")]
    [InlineData("pisoAprobacion")]
    [InlineData("visitaMaxima")]
    [InlineData("frasesSugeridas")]
    public async Task UpdateBody_WithAnInvalidValue_FailsOnThatField_CA_045_17(string field)
    {
        var body = field switch
        {
            "nombre" => Edit() with { Nombre = new string('x', 41) },
            "modalidad" => Edit() with { Modalidad = "remoto" },
            "topePorcentaje" => Edit() with { TopePorcentaje = 101m },
            "topeFijoPorcentaje" => Edit() with { TopeFijoPorcentaje = -0.1m },
            "pisoAprobacion" => Edit() with { PisoAprobacion = 0 },
            "visitaMaxima" => Edit() with { VisitaMaxima = 0 },
            _ => Edit() with { FrasesSugeridas = [new string('x', 61)] },
        };

        var result = await new UpdateTradeBodyValidator().ValidateAsync(body);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.PropertyName).Distinct().Should().ContainSingle().Which.Should().BeEquivalentTo(field);
        result.Errors.Should().OnlyContain(e => e.ErrorMessage == UpdateTradeBodyValidator.Reason);
    }

    [Fact]
    public async Task UpdateBody_WithNoFieldAtAll_IsRejected_CA_045_17()
    {
        var result = await new UpdateTradeBodyValidator().ValidateAsync(Edit());

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("campos");
    }

    [Fact]
    public async Task Update_WithAnotherKeyInTheBody_Answers422OnTheKeyFieldAndChangesNothing_CA_045_33()
    {
        var fixture = new AdminTradesControllerFixture();

        var result = await PutAsync(fixture, "plomeria", Edit() with { Clave = "gas", Nombre = "Otro" });

        var problem = result.Should().BeOfType<ObjectResult>().Which;
        problem.StatusCode.Should().Be(422);
        ((ProblemDetails)problem.Value!).Extensions["errors"].Should().BeOfType<Dictionary<string, string[]>>().Which.Should().ContainKey("clave");
        fixture.Store.Trades.Single().Clave.Should().Be("plomeria");
        fixture.Store.Trades.Single().Nombre.Should().Be("Plomería");
        fixture.Audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_OfAnUnknownTrade_Throws404WithTheBusinessCode_CA_045_19()
    {
        var fixture = new AdminTradesControllerFixture();

        var act = async () => await PutAsync(fixture, "electricidad", Edit() with { Nombre = "Otro" });

        var thrown = (await act.Should().ThrowAsync<DomainException>()).Which;
        thrown.Kind.Should().Be(DomainErrorKind.NotFound);
        thrown.Code.Should().Be(TradeErrorCodes.TradeNotFoundCode);
        fixture.Catalog.Invalidations.Should().Be(0);
    }

    [Fact]
    public async Task Update_WithoutTheAuditTrail_Answers503AndChangesNothing_CA_045_35()
    {
        var fixture = new AdminTradesControllerFixture(audit: new FakeAudit(available: false));

        var result = await PutAsync(fixture, "plomeria", Edit() with { Nombre = "Otro" });

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(503);
        fixture.Store.Trades.Single().Nombre.Should().Be("Plomería");
    }

    [Fact]
    public void TheUpdateRoute_DeclaresABodyLimit_AndTheUnreadableBodyIs400ByTheFramework_CA_045_18()
    {
        // Un cuerpo ilegible lo rechaza el enlace de datos de [ApiController] con 400; la prueba HTTP real está en Spec045OficiosAdminHttpTests.
        typeof(Knock.Market.Api.Modules.Catalog.Controllers.V1.AdminTradesController).GetMethod("Update")!
            .GetCustomAttributes<RequestSizeLimitAttribute>().Should().ContainSingle();
    }
}
