extern alias messaging;

using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Knock.UnitTest.Messaging;

/// <summary>Spec 044, T-05: <c>POST /api/v1/avisos/leidos</c> (AVI-04), criterios CA-12 y CA-13.</summary>
public sealed partial class Spec044AvisosTests
{
    [Fact]
    public async Task MarkAllRead_AffectsOnlyTheCaller_CA_044_12()
    {
        var store = StoreWith(3, others: 2);

        var result = await Avisos(store).MarkAllRead(CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        store.Notices.Where(n => n.User == _me).Should().OnlyContain(n => n.Read);
        store.Notices.Where(n => n.User == _other).Should().OnlyContain(n => !n.Read);
    }

    [Fact]
    public async Task MarkAllRead_LeavesTheCounterAtZero_AndRepeatingItIsHarmless_CA_044_12()
    {
        var store = StoreWith(5);
        var controller = Avisos(store);

        await controller.MarkAllRead(CancellationToken.None);
        var again = await controller.MarkAllRead(CancellationToken.None);

        again.Should().BeOfType<NoContentResult>();
        (await CountOf(controller)).Cantidad.Should().Be(0);
    }

    [Fact]
    public async Task MarkAllRead_WithoutSub_ReturnsUnauthorizedAndChangesNothing_CA_044_13()
    {
        var store = StoreWith(2);

        var result = await Avisos(store, sub: null).MarkAllRead(CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        store.LastUser.Should().BeNull();
        store.Notices.Should().OnlyContain(n => !n.Read);
    }
}
