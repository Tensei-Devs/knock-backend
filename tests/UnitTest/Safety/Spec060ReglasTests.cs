using AwesomeAssertions;
using Knock.Identity.Api.Modules.Safety.Domain;
using Knock.Identity.Api.Modules.Safety.Dtos;
using Knock.Identity.Api.Modules.Safety.Validators;
using Knock.Identity.Api.Modules.Sesiones.Services;
using Xunit;

namespace Knock.UnitTest.Safety;

/// <summary>Spec 060: reglas puras de la apelación de una persona suspendida, su vigencia y la frescura del inicio de sesión por SMS.</summary>
public sealed class Spec060ReglasTests
{
    private static readonly DateTime _now = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    private static ReportForAppeal Report(string state, StoredAppeal? appeal = null) => new("reportado", state, "moderador", appeal);

    private static StoredAppeal Appeal(string text = "texto de prueba", string origin = AppealOrigins.Suspension, string? decision = null) => new(text, _now, origin, decision, null);

    [Theory]
    [InlineData("1234567", false)]
    [InlineData("12345678", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void El_Texto_Mide_De_8_A_500_CA_060_04(string? text, bool valid) =>
        new SuspensionAppealBodyValidator().Validate(new SuspensionAppealBody { Texto = text }).IsValid.Should().Be(valid);

    [Fact]
    public void Un_Texto_De_501_Caracteres_Se_Rechaza_Sin_Repetirlo_CA_060_04()
    {
        var result = new SuspensionAppealBodyValidator().Validate(new SuspensionAppealBody { Texto = new string('a', 501) });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().OnlyContain(e => !e.ErrorMessage.Contains("aaaa", StringComparison.Ordinal));
    }

    [Fact]
    public void Sin_Apelacion_Y_Con_Decision_Se_Abre_CA_060_01() =>
        SuspensionAppealRules.Evaluate(Report("decidido"), "texto de prueba").Should().Be(AppealDecision.Open);

    [Fact]
    public void Sin_Decision_No_Hay_Nada_Que_Apelar_CA_060_03() =>
        SuspensionAppealRules.Evaluate(Report("abierto"), "texto de prueba").Should().Be(AppealDecision.NotFound);

    [Fact]
    public void El_Mismo_Texto_Pendiente_Es_Idempotente_Y_Otro_Texto_No_Se_Permite_CA_060_05()
    {
        var pending = Report("en_apelacion", Appeal());

        SuspensionAppealRules.Evaluate(pending, "texto de prueba").Should().Be(AppealDecision.Repeat);
        SuspensionAppealRules.Evaluate(pending, "otro texto distinto").Should().Be(AppealDecision.NotAllowed);
    }

    [Fact]
    public void Tras_Un_Rechazo_No_Se_Abre_Otra_Ni_Se_Repite_CA_060_08a() =>
        SuspensionAppealRules.Evaluate(Report("decidido", Appeal(decision: AppealDecisions.Reject)), "texto de prueba").Should().Be(AppealDecision.NotAllowed);

    [Fact]
    public void Una_Apelacion_De_Otro_Origen_No_Cuenta_Como_Repeticion_CA_060_05() =>
        SuspensionAppealRules.Evaluate(Report("en_apelacion", Appeal(origin: AppealOrigins.Session)), "texto de prueba").Should().Be(AppealDecision.NotAllowed);

    [Fact]
    public void El_Estado_Sale_De_La_Decision_CA_060_13()
    {
        Appeal().State.Should().Be("pendiente");
        Appeal(decision: AppealDecisions.Accept).State.Should().Be("aceptada");
        Appeal(decision: AppealDecisions.Reject).State.Should().Be("rechazada");
        Appeal().ToString().Should().NotContain("texto de prueba");
    }

    [Fact]
    public void La_Gradual_Vence_A_Los_7_Dias_Y_La_Critica_Nunca_CA_060_08b()
    {
        AccountSuspensionRules.ExpiresAt(SuspensionTypes.Gradual, _now, 7).Should().Be(_now.AddDays(7));
        AccountSuspensionRules.ExpiresAt(SuspensionTypes.Preventive, _now, 7).Should().BeNull();
        AccountSuspensionRules.HasExpired(SuspensionTypes.Gradual, _now.AddDays(7), _now.AddDays(7)).Should().BeTrue();
        AccountSuspensionRules.HasExpired(SuspensionTypes.Gradual, _now.AddDays(7), _now.AddDays(6)).Should().BeFalse();
        AccountSuspensionRules.HasExpired(SuspensionTypes.Preventive, _now.AddDays(-30), _now).Should().BeFalse();
        AccountSuspensionRules.HasExpired(SuspensionTypes.Gradual, null, _now.AddDays(100)).Should().BeFalse();
    }

    [Theory]
    [InlineData(-9, true)]
    [InlineData(-11, false)]
    [InlineData(10, false)]
    public void El_Codigo_SMS_Es_Reciente_Hasta_10_Minutos_CA_060_02(int minutes, bool recent) =>
        new FirebaseIdentity("uid", "+525555550000", new DateTimeOffset(_now).AddMinutes(minutes)).IsRecent(new DateTimeOffset(_now), FirebaseIdentity.RecentSignInWindow).Should().Be(recent);

    [Fact]
    public void Sin_Auth_Time_No_Es_Reciente_CA_060_02() =>
        new FirebaseIdentity("uid", "+525555550000").IsRecent(new DateTimeOffset(_now), FirebaseIdentity.RecentSignInWindow).Should().BeFalse();
}
