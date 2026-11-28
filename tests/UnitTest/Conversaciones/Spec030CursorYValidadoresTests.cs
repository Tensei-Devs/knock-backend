extern alias messaging;

using System.Text;
using AwesomeAssertions;
using messaging::Knock.Messaging.Api.Modules.Conversaciones.Domain;
using messaging::Knock.Messaging.Api.Modules.Conversaciones.Dtos;
using messaging::Knock.Messaging.Api.Modules.Conversaciones.Services;
using messaging::Knock.Messaging.Api.Modules.Conversaciones.Validators;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Conversaciones;

/// <summary>Spec 030: el cursor de las listas (R-16 y R-17), los identificadores de la ruta y el formato de los cuerpos y consultas. Solo datos ficticios.</summary>
public sealed class Spec030CursorYValidadoresTests
{
    private static readonly DateTime _date = new(2026, 11, 1, 12, 30, 15, 123, DateTimeKind.Utc);

    private static string Encode(string text) => Convert.ToBase64String(Encoding.ASCII.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Fact]
    public void ElCursor_VuelveIgual_CA_030_11()
    {
        var id = ObjectId.GenerateNewId();

        var parsed = PageCursor.Parse(new PageCursor(_date, id).Format());

        parsed.Should().Be(new PageCursor(_date, id));
        parsed.Value.Date.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SinCursor_EsLaPrimeraPagina_CA_030_11(string? cursor) =>
        PageCursor.Parse(cursor).Should().BeNull();

    [Theory]
    [InlineData("no-es-un-cursor!!")]
    [InlineData("%%%")]
    [InlineData("YWJj")]
    public void UnCursorIlegible_Es400_Sin_Repetir_El_Valor_CA_030_16(string cursor)
    {
        var read = () => PageCursor.Parse(cursor);

        var error = read.Should().Throw<DomainException>().Which;
        error.Kind.Should().Be(DomainErrorKind.BadRequest);
        error.Code.Should().Be("invalid_cursor");
        error.Message.Should().NotContain(cursor);
    }

    [Theory]
    [InlineData("abc.507f1f77bcf86cd799439011")]
    [InlineData("1700000000000.zzzzzzzzzzzzzzzzzzzzzzzz")]
    [InlineData("1700000000000.507f1f77bcf86cd79943901")]
    [InlineData("-5.507f1f77bcf86cd799439011")]
    [InlineData("99999999999999999.507f1f77bcf86cd799439011")]
    [InlineData("1700000000000")]
    [InlineData("1700000000000.507f1f77bcf86cd799439011.extra")]
    public void UnCursorBienCodificadoPeroMalFormado_Es400_CA_030_16(string payload)
    {
        var read = () => PageCursor.Parse(Encode(payload));

        read.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_cursor");
    }

    [Fact]
    public void UnCursorLargo_Es400_CA_030_16()
    {
        var read = () => PageCursor.Parse(new string('A', 200));

        read.Should().Throw<DomainException>().Which.Code.Should().Be("invalid_cursor");
    }

    [Theory]
    [InlineData("507f1f77bcf86cd799439011", true)]
    [InlineData("507F1F77BCF86CD799439011", true)]
    [InlineData("507f1f77bcf86cd79943901", false)]
    [InlineData("507f1f77bcf86cd7994390111", false)]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzz", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ElIdDeLaRuta_Se_Valida_Como_400_CA_030_03(string? id, bool valid)
    {
        var parse = () => ConversacionErrors.ParseId(id);

        if (valid)
        {
            parse().Should().Be(ObjectId.Parse(id));
            return;
        }

        var error = parse.Should().Throw<DomainException>().Which;
        error.Kind.Should().Be(DomainErrorKind.BadRequest);
        error.Code.Should().Be("invalid_id");
    }

    [Fact]
    public void LosCodigosDeError_Son_Los_De_La_Spec_CA_030_06()
    {
        ConversacionErrors.NotFound().Kind.Should().Be(DomainErrorKind.NotFound);
        ConversacionErrors.NotFound().Code.Should().Be("not_found");
        ConversacionErrors.Closed().Kind.Should().Be(DomainErrorKind.Conflict);
        ConversacionErrors.Closed().Code.Should().Be("conversation_closed");
    }

    [Theory]
    [InlineData("507f1f77bcf86cd799439011", null, true)]
    [InlineData("507f1f77bcf86cd799439011", "507f1f77bcf86cd799439012", true)]
    [InlineData(null, null, false)]
    [InlineData("", null, false)]
    [InlineData("no-es-id", null, false)]
    [InlineData("507f1f77bcf86cd799439011", "no-es-id", false)]
    [InlineData("507f1f77bcf86cd799439011", "", false)]
    public void CrearConversacion_Valida_El_Formato_CA_030_15(string? request, string? technician, bool valid) =>
        new CrearConversacionBodyValidator().Validate(new CrearConversacionBody(request, technician)).IsValid.Should().Be(valid);

    [Fact]
    public void CrearConversacion_Nombra_El_Campo_Y_No_Repite_El_Valor_CA_030_15()
    {
        const string wrong = "valor-de-prueba-ajeno";

        var result = new CrearConversacionBodyValidator().Validate(new CrearConversacionBody(wrong, wrong));

        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo("SolicitudId", "TecnicoId");
        result.Errors.Select(e => e.ErrorMessage).Should().OnlyContain(message => !message.Contains(wrong, StringComparison.Ordinal));
    }

    [Fact]
    public void Leido_Exige_Hasta_CA_030_12()
    {
        new LeidoBodyValidator().Validate(new LeidoBody(null)).Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Hasta");
        new LeidoBodyValidator().Validate(new LeidoBody(DateTimeOffset.UtcNow.AddYears(1))).IsValid.Should().BeTrue("una fecha futura se acota en el servicio, no se rechaza");
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(20, true)]
    [InlineData(0, false)]
    [InlineData(21, false)]
    [InlineData(-1, false)]
    public void ListaDeConversaciones_Acepta_De_1_A_20_CA_030_11(int? limit, bool valid) =>
        new ConversacionesQueryValidator().Validate(new ConversacionesQuery { Limit = limit }).IsValid.Should().Be(valid);

    [Theory]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(50, true)]
    [InlineData(0, false)]
    [InlineData(51, false)]
    public void ListaDeMensajes_Acepta_De_1_A_50_CA_030_16(int? limit, bool valid) =>
        new MensajesQueryValidator().Validate(new MensajesQuery { Limit = limit }).IsValid.Should().Be(valid);

    [Fact]
    public void LosLimitesPorOmision_Son_Los_De_La_Spec_CA_030_16()
    {
        ConversacionesQuery.DefaultLimit.Should().Be(20);
        ConversacionesQuery.MaxLimit.Should().Be(20);
        MensajesQuery.DefaultLimit.Should().Be(30);
        MensajesQuery.MaxLimit.Should().Be(50);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("Por tu seguridad.", true)]
    public void ElAvisoDeSeguridad_Debe_Estar_Configurado_CA_030_14(string? warning, bool valid) =>
        new ConversacionesOptionsValidator().Validate(null, new ConversacionesOptions { AvisoSeguridad = warning }).Succeeded.Should().Be(valid);
}
