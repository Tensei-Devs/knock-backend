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
    [InlineData("reporte_decidido", "sistema")]
    [InlineData("opinion_oculta", "sistema")]
    [InlineData("exportacion_lista", "sistema")]
    [InlineData("reprogramacion_propuesta", "visitas")]
    [InlineData("visita_reprogramada", "visitas")]
    [InlineData("reprogramacion_sin_cambio", "visitas")]
    [InlineData("visita_cancelada", "visitas")]
    [InlineData("cliente_ausente", "visitas")]
    [InlineData("visita_terminada", "visitas")]
    [InlineData("cotizacion_final_nueva", "visitas")]
    [InlineData("cotizacion_final_respondida", "visitas")]
    [InlineData("ajuste_precio_nuevo", "visitas")]
    [InlineData("ajuste_precio_respondido", "visitas")]
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

    [Theory]
    [InlineData("reporte_decidido", null, true)]
    [InlineData("reporte_decidido", "cliente", true)]
    [InlineData("reporte_decidido", "tecnico", true)]
    [InlineData("exportacion_lista", null, true)]
    [InlineData("exportacion_lista", "cliente", true)]
    [InlineData("exportacion_lista", "tecnico", true)]
    [InlineData("opinion_oculta", "tecnico", true)]
    [InlineData("opinion_oculta", "cliente", false)]
    [InlineData("opinion_oculta", null, false)]
    [InlineData("solicitud_nueva", null, false)]
    [InlineData("reprogramacion_propuesta", "cliente", true)]
    [InlineData("reprogramacion_propuesta", "tecnico", true)]
    [InlineData("reprogramacion_propuesta", null, false)]
    [InlineData("visita_reprogramada", "tecnico", true)]
    [InlineData("reprogramacion_sin_cambio", "tecnico", true)]
    [InlineData("visita_cancelada", "cliente", true)]
    [InlineData("cliente_ausente", "tecnico", false)]
    [InlineData("visita_terminada", "cliente", true)]
    [InlineData("cotizacion_final_nueva", "tecnico", false)]
    [InlineData("cotizacion_final_respondida", "cliente", false)]
    [InlineData("ajuste_precio_nuevo", "cliente", true)]
    [InlineData("ajuste_precio_respondido", "cliente", false)]
    public void RoleRulesOfTheFixedNotices_CA_037_20(string tipo, string? rol, bool expected) =>
        AvisoRules.RoleMatches(tipo, rol).Should().Be(expected);
}
