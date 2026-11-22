extern alias messaging;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;
using AvisoRules = messaging::Knock.Messaging.Api.Modules.Avisos.Domain.AvisoRules;

namespace Knock.UnitTest.Messaging;

/// <summary>Spec 037 T-06: la carga v1 que recibe el teléfono (frontend, spec 024) tiene exactamente estos campos de texto, sin datos personales ni texto libre (CA-04, CA-10).</summary>
public sealed class Spec037CargaV1ContratoTests
{
    private const string _resource = "507f1f77bcf86cd799439011";
    private const string _conversation = "507f1f77bcf86cd799439012";

    [Theory]
    [InlineData("solicitud_nueva", "cotizaciones")]
    [InlineData("solicitud_sin_tecnicos", "cotizaciones")]
    [InlineData("cotizacion_nueva", "cotizaciones")]
    [InlineData("cotizacion_aceptada", "visitas")]
    [InlineData("solicitud_cancelada", "cotizaciones")]
    [InlineData("solicitud_vencida", "cotizaciones")]
    [InlineData("cotizacion_vencida", "cotizaciones")]
    [InlineData("mensaje_nuevo", "mensajes")]
    public void Payload_SerializesToTheV1ContractShape_CA_037_04_CA_037_10(string evento, string tipo)
    {
        var aviso = Guid.NewGuid();
        var isMessage = evento == "mensaje_nuevo";

        var payload = AvisoRules.BuildPayload(evento, _resource, isMessage ? _conversation : null, aviso)!;

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        var fields = json.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value);
        fields.Keys.Should().BeEquivalentTo(isMessage ? ["v", "tipo", "evento", "recurso", "aviso", "conversacion"] : ["v", "tipo", "evento", "recurso", "aviso"]);
        fields.Values.Should().OnlyContain(value => value.ValueKind == JsonValueKind.String, "FCM solo transporta datos de texto (R-05)");
        fields["v"].GetString().Should().Be("1");
        fields["tipo"].GetString().Should().Be(tipo);
        fields["evento"].GetString().Should().Be(evento);
        fields["recurso"].GetString().Should().Be(_resource);
        fields["aviso"].GetString().Should().Be(aviso.ToString("D"));
    }
}
