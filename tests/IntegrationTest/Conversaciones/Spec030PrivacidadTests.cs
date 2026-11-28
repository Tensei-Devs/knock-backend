using System.Net;
using AwesomeAssertions;
using Xunit;

namespace Knock.IntegrationTest.Conversaciones;

/// <summary>Lo que ve cada parte de la otra (spec 030, R-06 y R-07): nunca contacto, <c>sub</c> ni dirección.</summary>
public sealed class Spec030PrivacidadTests
{
    private const string _warning = "Por tu seguridad, no hagas pagos por adelantado ni compartas tu número antes de elegir. Paga solo cuando el trabajo esté hecho.";

    [Fact]
    public async Task Contraparte_Sin_Datos_Personales_CA_030_14()
    {
        await using var host = await ConversacionesHost.StartAsync();
        var id = await host.SeedConversationAsync(unreadTechnician: 3);
        await host.SeedMessageAsync(id, "cliente", DateTime.UtcNow.AddMinutes(-4), "hola, ¿puede venir hoy?");

        using var asTechnician = await host.SendAsync(HttpMethod.Get, $"/api/v1/conversaciones/{id}", host.Technician);
        using var asClient = await host.SendAsync(HttpMethod.Get, $"/api/v1/conversaciones/{id}", host.Client);
        using var history = await host.SendAsync(HttpMethod.Get, $"/api/v1/conversaciones/{id}/mensajes", host.Technician);
        using var list = await host.SendAsync(HttpMethod.Get, "/api/v1/conversaciones", host.Technician);

        asTechnician.StatusCode.Should().Be(HttpStatusCode.OK);
        var technicianView = await ConversacionesHost.ReadAsync(asTechnician);
        var counterpart = technicianView.GetProperty("contraparte");
        counterpart.GetProperty("nombre").GetString().Should().Be($"Cliente de {ConversacionesHost.Neighborhood}");
        counterpart.GetProperty("fotoUrl").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        technicianView.GetProperty("avisoSeguridad").GetString().Should().Be(_warning);
        technicianView.GetProperty("rol").GetString().Should().Be("tecnico");
        technicianView.GetProperty("noLeidos").GetInt32().Should().Be(3);

        var clientView = await ConversacionesHost.ReadAsync(asClient);
        clientView.GetProperty("contraparte").GetProperty("nombre").GetString().Should().Be(ConversacionesHost.TechnicianName);
        clientView.GetProperty("contraparte").GetProperty("fotoUrl").GetString().Should().Be(ConversacionesHost.TechnicianPhoto);
        clientView.GetProperty("contactoVisible").GetBoolean().Should().BeFalse();
        clientView.GetProperty("avisoSeguridad").GetString().Should().Be(_warning);

        foreach (var response in new[] { asTechnician, asClient, history, list })
        {
            var body = await response.Content.ReadAsStringAsync();
            body.Should().NotContain(host.Client, "el sub del cliente nunca sale").And.NotContain(host.Technician, "el sub del técnico nunca sale");
            body.Should().NotContain("clienteId").And.NotContain("tecnicoUsuarioId").And.NotContain("autorId");
        }
    }
}
