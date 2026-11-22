extern alias messaging;

using System.Reflection;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Controllers = messaging::Knock.Messaging.Api.Modules.Notices.Controllers.V1;
using Dtos = messaging::Knock.Messaging.Api.Modules.Notices.Dtos;

namespace Knock.UnitTest.Messaging;

/// <summary>Spec 044, T-08: reglas transversales (R-15 a R-18), criterios CA-21 a CA-24. 401, 422, 413 y 429 sobre HTTP real están en <c>tests/SecurityTest/Messaging/Spec044AvisosSeguridadTests.cs</c> (CI).</summary>
public sealed partial class Spec044AvisosTests
{
    private static DefaultHttpContext WithProblemServices(ControllerBase controller)
    {
        var context = controller.ControllerContext.HttpContext as DefaultHttpContext ?? throw new InvalidOperationException("sin contexto");
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        return context;
    }

    [Fact]
    public void NoInputOfTheModuleAcceptsAPersonIdentifier_CA_044_21()
    {
        var inputs = new[] { typeof(Dtos.ListNoticesQuery), typeof(Dtos.MarkNoticeReadRequest) };

        var names = inputs.SelectMany(t => t.GetProperties()).Select(p => p.Name).ToList();

        names.Should().NotContain(["UsuarioId", "Sub", "PersonaId", "UserId", "ClienteId", "TecnicoId"]);
        var actions = new[] { typeof(Controllers.AvisosController), typeof(Controllers.MePreferenciasAvisosController) }
            .SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        actions.SelectMany(m => m.GetParameters()).Where(p => p.GetCustomAttributes<FromBodyAttribute>().Any()).Should().BeEmpty("ninguna ruta recibe cuerpo con identificadores");
    }

    [Fact]
    public async Task EveryOperation_UsesTheSubOfTheToken_AndNeverTheOneOfAnotherPerson_CA_044_21()
    {
        var store = StoreWith(2, others: 2);
        var controller = Avisos(store, sub: _other);

        await controller.List(new Dtos.ListNoticesQuery(), CancellationToken.None);
        store.LastUser.Should().Be(_other);
        await controller.CountUnread(CancellationToken.None);
        store.LastUser.Should().Be(_other);
        await controller.MarkAllRead(CancellationToken.None);
        store.Notices.Where(n => n.User == _me).Should().OnlyContain(n => !n.Read, "el sub del token es el de la otra persona");
        await Preferencias(store, sub: _other).Get(CancellationToken.None);
        store.LastUser.Should().Be(_other);
    }

    [Fact]
    public async Task TheNotFoundBody_DoesNotRepeatTheNoticeIdOrAnyPersonalData_CA_044_22()
    {
        var store = StoreWith(0);
        var controller = Avisos(store);
        var context = WithProblemServices(controller);

        await controller.MarkRead(new Dtos.MarkNoticeReadRequest { Id = Id(424242) }, CancellationToken.None);

        context.Response.Body.Position = 0;
        var body = await ReadAsync(context.Response.Body);
        body.Should().NotContain(Id(424242)).And.NotContain(_me);
    }

    [Fact]
    public void TheModuleDoesNotWriteLogsWithNoticeContent_CA_044_22()
    {
        // Ninguna clase del módulo depende de ILogger: no hay registros que puedan llevar títulos, textos ni destinos. Si una tarea futura agrega registros, esta prueba obliga a revisarlos con la spec 011.
        var types = typeof(Controllers.AvisosController).Assembly.GetTypes().Where(t => t.Namespace?.Contains("Modules.Notices", StringComparison.Ordinal) == true);

        var withLogger = types.Where(t => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Any(c => c.GetParameters().Any(p => p.ParameterType.Name.StartsWith("ILogger", StringComparison.Ordinal)))).Select(t => t.Name);

        withLogger.Should().BeEmpty();
    }

    [Fact]
    public async Task APostWithABodyOverOneKiB_IsRefusedWith413WithoutRunningTheOperation_CA_044_23()
    {
        var store = StoreWith(2);
        var controller = Avisos(store);
        var context = WithProblemServices(controller);
        context.Request.ContentLength = 2048;

        var one = await controller.MarkRead(new Dtos.MarkNoticeReadRequest { Id = Id(1) }, CancellationToken.None);
        var all = await controller.MarkAllRead(CancellationToken.None);

        one.Should().BeOfType<EmptyResult>();
        all.Should().BeOfType<EmptyResult>();
        context.Response.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        store.Notices.Should().OnlyContain(n => !n.Read);
        var limit = typeof(Controllers.AvisosController).GetMethod(nameof(Controllers.AvisosController.MarkRead))!.GetCustomAttributes<RequestSizeLimitAttribute>().Should().ContainSingle().Subject;
        ((IRequestSizeLimitMetadata)limit).MaxRequestBodySize.Should().Be(Controllers.AvisosController.MaxBodyBytes);
    }

    [Fact]
    public async Task RepeatingAnOperation_LeavesNoDuplicatesBecauseTheStateRuleSettlesIt_CA_044_24()
    {
        var store = StoreWith(3);
        var controller = Avisos(store);

        for (var i = 0; i < 3; i++)
        {
            await controller.MarkRead(new Dtos.MarkNoticeReadRequest { Id = Id(1) }, CancellationToken.None);
            await controller.MarkAllRead(CancellationToken.None);
        }

        store.Notices.Should().HaveCount(3).And.OnlyContain(n => n.Read);
    }

    [Fact]
    public void NoRouteAsksForAnIdempotencyKey_CA_044_24()
    {
        var controllers = new[] { typeof(Controllers.AvisosController), typeof(Controllers.MePreferenciasAvisosController) };

        var headers = controllers.SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(m => m.GetParameters()).SelectMany(p => p.GetCustomAttributes<FromHeaderAttribute>()).Select(a => a.Name);

        headers.Should().NotContain(name => string.Equals(name, "Idempotency-Key", StringComparison.OrdinalIgnoreCase));
    }
}
