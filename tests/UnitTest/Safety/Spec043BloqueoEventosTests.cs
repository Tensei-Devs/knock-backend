extern alias messaging;

using System.Text.Json;
using AwesomeAssertions;
using Knock.Contracts.Safety;
using Xunit;
using MarketDto = Knock.Market.Api.Modules.Requests.Dtos.BlockEventBody;
using MarketValidator = Knock.Market.Api.Modules.Requests.Validators.BlockEventBodyValidator;
using MessagingDto = messaging::Knock.Messaging.Api.Modules.Bloqueos.Dtos.EventoDeBloqueo;
using MessagingValidator = messaging::Knock.Messaging.Api.Modules.Bloqueos.Validators.EventoDeBloqueoValidator;

namespace Knock.UnitTest.Safety;

/// <summary>Spec 043, R-23 y CA-33: los validadores de la entrega interna de eventos de bloqueo en Mercado y Mensajería.</summary>
public sealed class Spec043BloqueoEventosTests
{
    private static readonly DateTime _at = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("BloqueoCreado", true)]
    [InlineData("BloqueoEliminado", true)]
    [InlineData("CuentaSuspendida", false)]
    [InlineData(null, false)]
    public void Messaging_AcceptsOnlyBlockEventTypes_CA_043_33(string? type, bool valid) =>
        new MessagingValidator().Validate(new MessagingDto("e1", type, "a", "b", _at)).IsValid.Should().Be(valid);

    [Theory]
    [InlineData("BloqueoCreado", true)]
    [InlineData("BloqueoEliminado", true)]
    [InlineData("otro", false)]
    public void Market_AcceptsOnlyBlockEventTypes_CA_043_33(string? type, bool valid) =>
        new MarketValidator().Validate(new MarketDto("e1", type, "a", "b", _at)).IsValid.Should().Be(valid);

    [Fact]
    public void Validators_RejectMissingIdsDateAndExtraFields_R_22()
    {
        var extra = new Dictionary<string, JsonElement> { ["nombre"] = JsonSerializer.SerializeToElement("x") };
        new MessagingValidator().Validate(new MessagingDto(null, BlockCreated.EventType, "a", "b", _at)).IsValid.Should().BeFalse();
        new MessagingValidator().Validate(new MessagingDto("e", BlockCreated.EventType, "a", "b", null)).IsValid.Should().BeFalse();
        new MessagingValidator().Validate(new MessagingDto("e", BlockCreated.EventType, "a", "b", _at) { Extra = extra }).IsValid.Should().BeFalse();
        new MarketValidator().Validate(new MarketDto("e", BlockRemoved.EventType, null, "b", _at)).IsValid.Should().BeFalse();
        new MarketValidator().Validate(new MarketDto("e", BlockRemoved.EventType, "a", "b", _at) { Extra = extra }).IsValid.Should().BeFalse();
        new MarketValidator().Validate(new MarketDto(new string('x', 129), BlockRemoved.EventType, "a", "b", _at)).IsValid.Should().BeFalse();
    }
}
