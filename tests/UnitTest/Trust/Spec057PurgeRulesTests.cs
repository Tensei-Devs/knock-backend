using AwesomeAssertions;
using Knock.Identity.Api.Modules.Trust.Domain;
using Xunit;

namespace Knock.UnitTest.Trust;

/// <summary>Spec 057 (KNOCK-87): plazos y estado final de la purga de fotos de verificación, con tiempo explícito.</summary>
public class Spec057PurgeRulesTests
{
    private static readonly DateTime _sent = new(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void La_Alerta_Sale_A_Las_24_Horas_Y_Una_Sola_Vez_CA_057_02()
    {
        PurgeRules.NeedsAlert("en_revision", null, _sent, _sent.AddHours(23)).Should().BeFalse();
        PurgeRules.NeedsAlert("en_revision", null, _sent, _sent.AddHours(24)).Should().BeTrue();
        PurgeRules.NeedsAlert("en_revision", _sent.AddHours(24), _sent, _sent.AddHours(30)).Should().BeFalse("ya se alertó");
        PurgeRules.NeedsAlert("verificada", null, _sent, _sent.AddHours(30)).Should().BeFalse("ya se decidió");
    }

    [Fact]
    public void Vence_A_Los_7_Dias_Solo_En_Revision_Y_Sin_Purgar_CA_057_03()
    {
        PurgeRules.IsExpired("en_revision", null, _sent, _sent.AddDays(7).AddTicks(-1)).Should().BeFalse();
        PurgeRules.IsExpired("en_revision", null, _sent, _sent.AddDays(7)).Should().BeTrue();
        PurgeRules.IsExpired("en_revision", _sent.AddDays(8), _sent, _sent.AddDays(9)).Should().BeFalse("la purga es idempotente");
        PurgeRules.IsExpired("no_verificada", null, _sent, _sent.AddDays(9)).Should().BeFalse();
    }

    [Theory]
    [InlineData("decidida", true, "verificada")]
    [InlineData("decidida", false, "no_verificada")]
    [InlineData("vencida", false, "sin_verificar")]
    [InlineData("baja_de_cuenta", true, "sin_verificar")]
    [InlineData("otra", true, null)]
    public void El_Estado_Final_Depende_De_La_Razon_CA_057_01(string reason, bool approved, string? expected)
    {
        PurgeRules.FinalState(reason, approved).Should().Be(expected);
    }

    [Fact]
    public void Solo_El_Vencimiento_Pide_Reenviar_Las_Fotos_CA_057_03()
    {
        PurgeRules.VisibleReason("vencida").Should().Be("fotos_borradas");
        PurgeRules.VisibleReason("decidida").Should().BeNull();
        PurgeReasons.IsKnown("baja_de_cuenta").Should().BeTrue();
        PurgeReasons.IsKnown("x").Should().BeFalse();
    }
}
