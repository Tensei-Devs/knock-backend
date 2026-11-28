extern alias messaging;

using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Controllers = messaging::Knock.Messaging.Api.Modules.Notices.Controllers.V1;
using Dtos = messaging::Knock.Messaging.Api.Modules.Notices.Dtos;
using Validators = messaging::Knock.Messaging.Api.Modules.Notices.Validators;

namespace Knock.UnitTest.Messaging;

/// <summary>Spec 044, T-04: <c>POST /api/v1/avisos/{id}/leido</c> (AVI-03), criterios CA-07 a CA-11.</summary>
public sealed partial class Spec044AvisosTests
{
    [Fact]
    public async Task MarkRead_OnAnOwnNoticeIsIdempotent_CA_044_07()
    {
        var store = StoreWith(1);
        var controller = Avisos(store);

        var first = await controller.MarkRead(new Dtos.MarkNoticeReadRequest { Id = Id(1) }, CancellationToken.None);
        var again = await controller.MarkRead(new Dtos.MarkNoticeReadRequest { Id = Id(1) }, CancellationToken.None);

        first.Should().BeOfType<NoContentResult>();
        again.Should().BeOfType<NoContentResult>();
        store.Notices.Single().Read.Should().BeTrue();
    }

    [Fact]
    public async Task MarkRead_DropsTheUnreadCountByOne_CA_044_17()
    {
        var store = StoreWith(3);

        var before = await CountOf(Avisos(store));
        await Avisos(store).MarkRead(new Dtos.MarkNoticeReadRequest { Id = Id(1) }, CancellationToken.None);
        var after = await CountOf(Avisos(store));

        before.Cantidad.Should().Be(3);
        after.Cantidad.Should().Be(2);
    }

    [Fact]
    public async Task MarkRead_WithoutSub_ReturnsUnauthorizedAndChangesNothing_CA_044_08()
    {
        var store = StoreWith(1);

        var result = await Avisos(store, sub: null).MarkRead(new Dtos.MarkNoticeReadRequest { Id = Id(1) }, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        store.LastUser.Should().BeNull();
        store.Notices.Single().Read.Should().BeFalse();
    }

    [Fact]
    public async Task MarkRead_OnANoticeOfAnotherPerson_IsRefusedAsNotFoundAndChangesNothing_CA_044_09_CA_044_18()
    {
        // La spec pide 403 (CA-09) y también 404 sin revelar que existe (R-12, CA-11, CA-18). Se aplica el 404 uniforme: cualquier token válido es una persona, y un 403 distinto del 404 diría que el aviso existe.
        var store = StoreWith(0, others: 1);
        var controller = Avisos(store);
        var context = controller.ControllerContext.HttpContext;
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        context.Response.Body = new MemoryStream();

        var result = await controller.MarkRead(new Dtos.MarkNoticeReadRequest { Id = Id(1000) }, CancellationToken.None);

        result.Should().BeOfType<EmptyResult>();
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        store.Notices.Single().Read.Should().BeFalse("el aviso es de otra persona");
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzz")]
    [InlineData("507f1f77bcf86cd79943901")]
    public async Task MarkRead_WithAnInvalidId_FailsValidationWithoutRepeatingIt_CA_044_10(string id)
    {
        var result = await new Validators.MarkNoticeReadRequestValidator().ValidateAsync(new Dtos.MarkNoticeReadRequest { Id = id });

        result.IsValid.Should().BeFalse();
        result.Errors[0].ErrorMessage.Should().NotContain(id.Length == 0 ? "\0" : id);
    }

    [Fact]
    public async Task MarkRead_OnAnUnknownNotice_ReturnsNotFoundWithTheBusinessCode_CA_044_11()
    {
        var store = StoreWith(1);
        var controller = Avisos(store);
        var context = controller.ControllerContext.HttpContext;
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        context.Response.Body = new MemoryStream();

        var result = await controller.MarkRead(new Dtos.MarkNoticeReadRequest { Id = Id(777) }, CancellationToken.None);

        result.Should().BeOfType<EmptyResult>();
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        context.Response.Body.Position = 0;
        (await ReadAsync(context.Response.Body)).Should().Contain(Controllers.AvisosController.NoticeNotFoundCode);
        store.Notices.Single().Read.Should().BeFalse();
    }
}
