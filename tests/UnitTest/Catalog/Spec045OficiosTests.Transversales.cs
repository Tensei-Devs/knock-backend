using System.Reflection;
using AwesomeAssertions;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Catalog.Controllers.V1;
using Knock.Market.Api.Modules.Catalog.Data;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Knock.Market.Api.Modules.Catalog.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Xunit;

namespace Knock.UnitTest.Catalog;

/// <summary>Spec 045, T-10: reglas transversales (R-23 a R-26), criterios CA-38 a CA-41. 413 y 429 sobre HTTP real están en <c>Spec045OficiosHttpTests</c> (CI).</summary>
public sealed partial class Spec045OficiosTests
{
    private static readonly Type[] _writeBodies = [typeof(CreateTradeBody), typeof(UpdateTradeBody), typeof(TradeRequestBody), typeof(TradeKeyRoute), typeof(AdminPageQuery)];

    private static readonly Type[] _controllers = [typeof(OficiosController), typeof(AdminTradesController), typeof(TradeRequestsController), typeof(AdminCatalogController)];

    [Fact]
    public void NoInputOfTheSpecAcceptsAPersonIdentifier_CA_045_38()
    {
        var names = _writeBodies.SelectMany(t => t.GetProperties()).Select(p => p.Name).ToList();

        names.Should().NotContain(["UsuarioId", "Sub", "PersonaId", "UserId", "TecnicoId", "ClienteId"]);
    }

    [Fact]
    public async Task TheTradeRequestIsSavedForTheSubOfTheToken_NeverForAnotherPerson_CA_045_38()
    {
        var store = new FakeRequestStore(_technician, "otra-persona");

        await Requests(store, sub: _technician).Create(new TradeRequestBody("Plomero"), CancellationToken.None);

        store.Saved.Should().ContainSingle().Which.User.Should().Be(_technician);
    }

    [Fact]
    public async Task TheAuditEventUsesTheSubOfThePanelToken_CA_045_38()
    {
        var store = new FakeTradeAdminStore();

        await Admin(AdminService(store), sub: "panel-9").Create(NewBody(), CancellationToken.None);

        store.Audits.Should().ContainSingle().Which.PanelSubject.Should().Be("panel-9");
    }

    [Fact]
    public void NoClassOfTheTradeFlowWritesLogs_AndTheAuditCarriesNoPersonalData_CA_045_39()
    {
        // Ninguna clase nueva depende de ILogger: no hay registros que puedan llevar textos escritos por la persona. Si una tarea futura agrega registros, esta prueba obliga a revisarlos con la spec 011.
        var types = new[] { typeof(TradeAdminService), typeof(Knock.Market.Api.Modules.Catalog.Services.TradeRequestService), typeof(Knock.Market.Api.Modules.Catalog.Data.TradeAdminStore), typeof(Knock.Market.Api.Modules.Catalog.Data.TradeRequestStore) }
            .Concat(_controllers);

        var withLogger = types.Where(t => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Any(c => c.GetParameters().Any(p => p.ParameterType.Name.StartsWith("ILogger", StringComparison.Ordinal)))).Select(t => t.Name);

        withLogger.Should().BeEmpty();
        typeof(TradeAuditEvent).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("PanelSubject", "Created", "AtUtc");
    }

    [Fact]
    public async Task TheErrorsOfTheTradeFlow_DoNotRepeatTheValuesSent_CA_045_39()
    {
        var store = new FakeRequestStore(_technician);
        var act = async () => await Requests(store, sub: "cliente-secreto-1").Create(new TradeRequestBody("Plomero secreto"), CancellationToken.None);

        var thrown = (await act.Should().ThrowAsync<Knock.Market.Api.Common.Errors.DomainException>()).Which;

        thrown.Message.Should().NotContain("cliente-secreto-1").And.NotContain("secreto");
    }

    [Fact]
    public void EveryRouteOfTheSpecDeclaresItsRateLimitOrRidesTheGlobalOne_AndEveryWriteHasABodyLimit_CA_045_40()
    {
        typeof(AdminTradesController).GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName.Should().Be(MarketRateLimits.AdminPolicy);
        typeof(TradeRequestsController).GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName.Should().Be(MarketRateLimits.RegisterTechnicianPolicy);
        typeof(OficiosController).GetCustomAttribute<EnableRateLimitingAttribute>().Should().BeNull("OFI-02 es pública y rige el límite global por IP");
        foreach (var write in new[] { typeof(AdminTradesController).GetMethod("Create")!, typeof(AdminTradesController).GetMethod("Update")!, typeof(TradeRequestsController).GetMethod("Create")! })
        {
            write.GetCustomAttributes<RequestSizeLimitAttribute>().Should().ContainSingle($"{write.DeclaringType!.Name}.{write.Name} limita su cuerpo (413)");
        }
    }

    [Fact]
    public async Task TheCreationRoutes_NeverAskForAnIdempotencyKey_AndRepeatingThemLeavesNoDuplicates_CA_045_41()
    {
        var headers = _controllers.SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(m => m.GetParameters()).SelectMany(p => p.GetCustomAttributes<FromHeaderAttribute>()).Select(a => a.Name);
        var store = new FakeTradeAdminStore();
        var controller = Admin(AdminService(store));
        var requests = new FakeRequestStore(_technician);

        await controller.Create(NewBody(), CancellationToken.None);
        var repeat = async () => await controller.Create(NewBody(), CancellationToken.None);
        await repeat.Should().ThrowAsync<Knock.Market.Api.Common.Errors.DomainException>();
        await Requests(requests).Create(new TradeRequestBody("Plomero"), CancellationToken.None);
        await Requests(requests).Create(new TradeRequestBody("Plomero"), CancellationToken.None);

        headers.Should().NotContain(name => string.Equals(name, "Idempotency-Key", StringComparison.OrdinalIgnoreCase));
        store.Trades.Should().ContainSingle();
        requests.Saved.Should().ContainSingle();
    }
}
