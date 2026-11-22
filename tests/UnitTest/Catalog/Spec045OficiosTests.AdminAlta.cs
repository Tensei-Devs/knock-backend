using AwesomeAssertions;
using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Catalog.Domain;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Knock.Market.Api.Modules.Catalog.Validators;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Knock.UnitTest.Catalog;

/// <summary>Spec 045, T-04: <c>POST /api/v1/admin/oficios</c> (OFI-04), criterios CA-08 a CA-13.</summary>
public sealed partial class Spec045OficiosTests
{
    [Fact]
    public async Task Create_WithValidData_Returns201WithTheTrade_ActiveByDefault_CA_045_08()
    {
        var store = new FakeTradeAdminStore();
        var catalog = new FakeActiveOficios();

        var result = await Admin(AdminService(store, catalog)).Create(NewBody(), CancellationToken.None);

        var created = result.Should().BeOfType<CreatedResult>().Which;
        created.StatusCode.Should().Be(201);
        var trade = (AdminTradeResponse)created.Value!;
        trade.Should().BeEquivalentTo(new AdminTradeResponse("gas", "Instalaciones de gas", "ambos", ["Huele a gas"], 25m, 10m, 200, 500, true));
        store.Trades.Should().ContainSingle();
        catalog.Invalidations.Should().Be(1, "el alta invalida la caché de oficios activos");
    }

    [Fact]
    public async Task Create_LeavesTheAuditEntryWithWhoWhenAndWhat_BeforeWriting_CA_045_08_CA_045_35()
    {
        var audit = new FakeAudit();

        await Admin(AdminService(new FakeTradeAdminStore(), audit: audit), sub: "panel-1").Create(NewBody(), CancellationToken.None);

        var entry = audit.Entries.Should().ContainSingle().Subject;
        entry.Subject.Should().Be("panel-1");
        entry.Action.Should().Be("trade.create");
        entry.Resource.Should().Be("gas");
        entry.At.Should().Be(_now);
        entry.Changes.Should().ContainKey("topePorcentaje").WhoseValue.Should().Be("25");
    }

    [Fact]
    public async Task Create_WithoutTheAuditTrail_Answers503AndWritesNothing_CA_045_35()
    {
        var store = new FakeTradeAdminStore();
        var controller = Admin(AdminService(store, audit: new FakeAudit(available: false)));

        var result = await controller.Create(NewBody(), CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(503);
        store.Trades.Should().BeEmpty();
        store.Inserts.Should().Be(0);
    }

    [Fact]
    public async Task TheDefaultAuditTrail_FailsClosed_CA_045_35()
    {
        var recorded = await new UnavailablePanelAuditTrail().RecordAsync(new PanelAuditEntry("x", "trade.create", "gas", _now, new Dictionary<string, string>()), CancellationToken.None);

        recorded.Should().BeFalse();
    }

    [Fact]
    public async Task Create_WithoutSub_Returns401AndWritesNothing_CA_045_09()
    {
        var store = new FakeTradeAdminStore();

        var result = await Admin(AdminService(store), sub: null).Create(NewBody(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        store.Trades.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_AllowsOnlyWhenThePanelGateGrantsCatalogManage_CA_045_10()
    {
        (await CatalogManageAllowedAsync(new StubGate(PanelDecision.Denied))).Should().BeFalse();
        (await CatalogManageAllowedAsync(new DenyAllPanelPermissionGate())).Should().BeFalse();
    }

    private static readonly (string Field, CreateTradeBody Body)[] _invalidCreates =
    [
        ("clave", NewBody("Con Espacios")),
        ("clave", NewBody("")),
        ("clave", NewBody("clave-demasiado-larga")),
        ("nombre", NewBody() with { Nombre = new string('x', 41) }),
        ("nombre", NewBody() with { Nombre = "   " }),
        ("modalidad", NewBody() with { Modalidad = "remoto" }),
        ("topePorcentaje", NewBody() with { TopePorcentaje = 100.5m }),
        ("topePorcentaje", NewBody() with { TopePorcentaje = -1m }),
        ("topePorcentaje", NewBody() with { TopePorcentaje = null }),
        ("topeFijoPorcentaje", NewBody() with { TopeFijoPorcentaje = 101m }),
        ("pisoAprobacion", NewBody() with { PisoAprobacion = 0 }),
        ("visitaMaxima", NewBody() with { VisitaMaxima = -5 }),
        ("visitaMaxima", NewBody() with { VisitaMaxima = null }),
        ("frasesSugeridas", NewBody() with { FrasesSugeridas = [.. Enumerable.Repeat("frase", 11)] }),
        ("frasesSugeridas", NewBody() with { FrasesSugeridas = [new string('x', 61)] }),
    ];

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    public async Task CreateBody_WithAnInvalidValue_FailsOnThatFieldOnly_CA_045_11(int index)
    {
        var (field, body) = _invalidCreates[index];
        var result = await new CreateTradeBodyValidator().ValidateAsync(body);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.PropertyName).Distinct().Should().ContainSingle().Which.Should().BeEquivalentTo(field);
        result.Errors.Should().OnlyContain(e => e.ErrorMessage == CreateTradeBodyValidator.Reason, "el motivo es fijo y nunca repite el valor enviado");
    }

    [Theory]
    [InlineData("domicilio")]
    [InlineData("taller")]
    [InlineData("ambos")]
    public async Task CreateBody_AcceptsTheThreeModalities_CA_045_11(string modality) =>
        (await new CreateTradeBodyValidator().ValidateAsync(NewBody() with { Modalidad = modality })).IsValid.Should().BeTrue();

    [Fact]
    public async Task CreateBody_AcceptsTheLimitValues_CA_045_11()
    {
        var body = new CreateTradeBody("a", new string('n', 40), "taller", null, 0m, 100m, 1, 1, false);

        (await new CreateTradeBodyValidator().ValidateAsync(body)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void TheCreateRoute_DeclaresABodyLimit_AndTheUnreadableBodyIs400ByTheFramework_CA_045_12()
    {
        // Un cuerpo que no se puede leer lo rechaza el enlace de datos de [ApiController] con 400 antes de llegar a la acción; la prueba HTTP real está en Spec045OficiosAdminHttpTests.
        var create = typeof(Knock.Market.Api.Modules.Catalog.Controllers.V1.AdminTradesController).GetMethod("Create")!;

        create.GetCustomAttributes(typeof(RequestSizeLimitAttribute), inherit: false).Should().ContainSingle();
        TradeBodies.MaxBodyBytes.Should().Be(4096);
    }

    [Fact]
    public async Task Create_WithARepeatedKey_Throws409AndChangesNothing_CA_045_13()
    {
        var store = new FakeTradeAdminStore();
        store.Trades.Add(Trade("gas", "Gas"));
        var catalog = new FakeActiveOficios();

        var act = async () => await Admin(AdminService(store, catalog)).Create(NewBody("gas"), CancellationToken.None);

        var thrown = (await act.Should().ThrowAsync<DomainException>()).Which;
        thrown.Kind.Should().Be(DomainErrorKind.Conflict);
        thrown.Code.Should().Be(TradeErrorCodes.TradeKeyTakenCode);
        store.Trades.Should().ContainSingle().Which.Nombre.Should().Be("Gas");
        catalog.Invalidations.Should().Be(0);
    }
}
