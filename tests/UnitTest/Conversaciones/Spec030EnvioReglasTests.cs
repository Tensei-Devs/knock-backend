extern alias messaging;

using System.Net;
using System.Text;
using AwesomeAssertions;
using Knock.Contracts.Conversations;
using Knock.Contracts.Moderation;
using messaging::Knock.Messaging.Api.Modules.Conversaciones.Data;
using messaging::Knock.Messaging.Api.Modules.Conversaciones.Domain;
using messaging::Knock.Messaging.Api.Modules.Conversaciones.Dtos;
using messaging::Knock.Messaging.Api.Modules.Conversaciones.Services;
using messaging::Knock.Messaging.Api.Modules.Conversaciones.Validators;
using Xunit;

namespace Knock.UnitTest.Conversaciones;

/// <summary>Spec 030, T-06 (KNOCK-73): las reglas puras del envío de mensajes, su validador, el cliente de permiso de Mercado y la moderación por omisión. Solo datos ficticios.</summary>
public sealed class Spec030EnvioReglasTests
{
    private static readonly EnviarMensajeRequestValidator _validator = new();

    [Theory]
    [InlineData("a", "a")]
    [InlineData("  hola  ", "hola")]
    [InlineData("línea uno\nlínea dos", "línea uno\nlínea dos")]
    [InlineData("emoji 🔧 incluido", "emoji 🔧 incluido")]
    public void Limpiar_Acepta_Texto_Valido_CA_030_02(string input, string expected) =>
        ReglasDeMensaje.Limpiar(input).Should().Be(expected);

    [Fact]
    public void Limpiar_Acepta_Exactamente_1000_Y_Rechaza_1001_CA_030_02()
    {
        ReglasDeMensaje.Limpiar(new string('x', 1_000)).Should().HaveLength(1_000);
        ReglasDeMensaje.Limpiar(new string('x', 1_001)).Should().BeNull();
        ReglasDeMensaje.Limpiar(" " + new string('x', 1_000) + " ").Should().HaveLength(1_000, "el recorte va antes de contar");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    [InlineData("hola\u0007")]
    [InlineData("hola\u0000mundo")]
    [InlineData("tab\taqui")]
    [InlineData("retorno\rde carro")]
    public void Limpiar_Rechaza_Vacio_O_Con_Caracteres_De_Control_CA_030_02(string? input) =>
        ReglasDeMensaje.Limpiar(input).Should().BeNull();

    [Fact]
    public void Limpiar_No_Deja_Pasar_Texto_Que_Excede_Los_Bytes_Del_Cuerpo_CA_030_02()
    {
        var huge = new string('é', 3_000);
        Encoding.UTF8.GetByteCount(huge).Should().BeGreaterThan(ReglasDeMensaje.MaxBytesDelTexto);
        ReglasDeMensaje.Limpiar(huge).Should().BeNull();
    }

    [Fact]
    public void Resumir_Recorta_A_80_Y_No_Parte_Un_Par_Sustituto_CA_030_01()
    {
        ReglasDeMensaje.Resumir("corto").Should().Be("corto");
        ReglasDeMensaje.Resumir(new string('a', 80)).Should().HaveLength(80);
        ReglasDeMensaje.Resumir(new string('a', 81)).Should().HaveLength(80);
        var split = new string('a', 79) + "🔧🔧";
        var summary = ReglasDeMensaje.Resumir(split);
        summary.Should().Be(new string('a', 79), "el emoji que cae en el corte se descarta completo");
        char.IsHighSurrogate(summary[^1]).Should().BeFalse();
    }

    [Fact]
    public void RolDe_Distingue_Cliente_Tecnico_Y_Ajeno_CA_030_03()
    {
        var conversation = new ConversacionParaEnvio("id", "sol", "tec", "cliente-sub", "tecnico-sub");

        conversation.RolDe("cliente-sub").Should().Be("cliente");
        conversation.RolDe("tecnico-sub").Should().Be("tecnico");
        conversation.RolDe("otro").Should().BeNull();
        conversation.RolDe(null).Should().BeNull();
        conversation.RolDe(string.Empty).Should().BeNull();
        conversation.RolDe("CLIENTE-SUB").Should().BeNull("el sub se compara tal cual");
        conversation.DestinatarioDe("cliente").Should().Be("tecnico-sub");
        conversation.DestinatarioDe("tecnico").Should().Be("cliente-sub");
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("hola", true)]
    [InlineData("hola\u0007", false)]
    public void El_Validador_Usa_La_Misma_Regla_CA_030_02(string? text, bool valid) =>
        _validator.Validate(new EnviarMensajeRequest(text)).IsValid.Should().Be(valid);

    [Fact]
    public void El_Validador_No_Repite_El_Texto_En_El_Motivo_CA_030_02()
    {
        var text = new string('z', 1_001);

        var result = _validator.Validate(new EnviarMensajeRequest(text));

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Texto");
        result.Errors[0].ErrorMessage.Should().NotContain(text);
    }

    [Fact]
    public async Task La_Moderacion_Por_Omision_Falla_Cerrado_CA_030_07()
    {
        var moderator = new ModeradorNoDisponible();

        (await moderator.ReviewTextAsync("hola", CancellationToken.None)).Outcome.Should().Be(ModerationOutcome.Unavailable);
        (await moderator.ReviewImageAsync(new byte[] { 1 }, CancellationToken.None)).Outcome.Should().Be(ModerationOutcome.Unavailable);
    }
}
