extern alias messaging;

using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Controllers = messaging::Knock.Messaging.Api.Modules.Notices.Controllers.V1;
using Dtos = messaging::Knock.Messaging.Api.Modules.Notices.Dtos;

namespace Knock.UnitTest.Messaging;

/// <summary>Spec 044, T-03: <c>GET /api/v1/avisos/no-leidos/contador</c> (AVI-02), criterios CA-05 y CA-06.</summary>
public sealed partial class Spec044AvisosTests
{
    private static async Task<Dtos.UnreadCountResponse> CountOf(Controllers.AvisosController controller) =>
        (Dtos.UnreadCountResponse)(await controller.CountUnread(CancellationToken.None)).Should().BeOfType<OkObjectResult>().Which.Value!;

    [Fact]
    public async Task CountUnread_ReturnsTheExactCountOfTheCallerOnly_CA_044_05()
    {
        var store = StoreWith(3, others: 4);

        var count = await CountOf(Avisos(store));

        count.Should().Be(new Dtos.UnreadCountResponse(3, false));
        store.LastUser.Should().Be(_me);
    }

    [Fact]
    public async Task CountUnread_CapsAtNinetyNineAndFlagsMoreThanNinetyNine_CA_044_05()
    {
        var exactly = await CountOf(Avisos(StoreWith(99)));
        var many = await CountOf(Avisos(StoreWith(120)));

        exactly.Should().Be(new Dtos.UnreadCountResponse(99, false));
        many.Should().Be(new Dtos.UnreadCountResponse(99, true));
    }

    [Fact]
    public async Task CountUnread_WithoutSub_ReturnsUnauthorizedAndTouchesNothing_CA_044_06()
    {
        var store = StoreWith(2);

        var result = await Avisos(store, sub: null).CountUnread(CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        store.LastUser.Should().BeNull();
    }
}
