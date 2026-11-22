using System.Net;
using AwesomeAssertions;
using Knock.Contracts.Conversations;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Conversaciones;

/// <summary>Crear una conversación (spec 030, R-04): idempotente por el par, con el permiso de Mercado y sin crear las cerradas.</summary>
public sealed class Spec030CreacionTests
{
    [Fact]
    public async Task Creacion_Idempotente_Y_Tecnico_Propio_CA_030_15()
    {
        await using var host = await ConversacionesHost.StartAsync();

        using var first = await host.CreateAsync(host.Technician);
        var created = await ConversacionesHost.ReadAsync(first);
        using var second = await host.CreateAsync(host.Technician, ObjectId.GenerateNewId().ToString());
        var repeated = await ConversacionesHost.ReadAsync(second);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        first.Headers.Location!.ToString().Should().Be($"/api/v1/conversaciones/{created.GetProperty("conversacionId").GetString()}");
        second.StatusCode.Should().Be(HttpStatusCode.OK, "el técnico que manda el tecnicoId de otro usa el suyo y la conversación ya existe");
        repeated.GetProperty("conversacionId").GetString().Should().Be(created.GetProperty("conversacionId").GetString());
        (await host.Conversations.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1);
        var stored = await host.Conversations.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        stored["tecnicoId"].AsString.Should().Be(host.TechnicianId);
        stored["clienteId"].AsString.Should().Be(host.Client);
        stored["tecnicoUsuarioId"].AsString.Should().Be(host.Technician);
        stored["estado"].AsString.Should().Be("abierta");
        stored["noLeidosCliente"].ToInt32().Should().Be(0);
        stored["noLeidosTecnico"].ToInt32().Should().Be(0);
    }

    [Fact]
    public async Task Dos_Creaciones_Simultaneas_Dejan_Un_Solo_Documento_CA_030_15()
    {
        await using var host = await ConversacionesHost.StartAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => host.CreateAsync(host.Client, host.TechnicianId)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1, "solo una creación gana el índice único");
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(7);
        (await host.Conversations.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1);
        var ids = new HashSet<string?>();
        foreach (var response in responses)
        {
            ids.Add((await ConversacionesHost.ReadAsync(response)).GetProperty("conversacionId").GetString());
            response.Dispose();
        }

        ids.Should().HaveCount(1);
    }

    [Fact]
    public async Task Solicitud_Cerrada_No_Se_Crea_Pero_Una_Existente_Se_Lee_CA_030_06()
    {
        await using var host = await ConversacionesHost.StartAsync();
        host.Permissions.Answer = (_, _, _) => new ResultadoPermisoConversacion(
            EstadoPermisoConversacion.Concedido, host.Permission(open: false, closeReason: "request_cancelled"));

        using var closed = await host.CreateAsync(host.Client, host.TechnicianId);

        closed.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ConversacionesHost.ReadAsync(closed)).GetProperty("code").GetString().Should().Be("conversation_closed");
        (await host.Conversations.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(0);

        // Con la conversación ya creada, que se cierre después no impide verla: el permiso actualiza su estado.
        host.Permissions.Answer = null;
        using var opened = await host.CreateAsync(host.Client, host.TechnicianId);
        host.Permissions.Answer = (_, _, _) => new ResultadoPermisoConversacion(
            EstadoPermisoConversacion.Concedido, host.Permission(open: false, closeReason: "other_quote_chosen"));
        using var again = await host.CreateAsync(host.Client, host.TechnicianId);
        var view = await ConversacionesHost.ReadAsync(again);

        opened.StatusCode.Should().Be(HttpStatusCode.Created);
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        view.GetProperty("estado").GetString().Should().Be("cerrada");
        view.GetProperty("motivoCierre").GetString().Should().Be("other_quote_chosen");
        var stored = await host.Conversations.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        stored["estado"].AsString.Should().Be("cerrada");
        stored["motivoCierre"].AsString.Should().Be("other_quote_chosen");
    }

    [Fact]
    public async Task Elegido_Y_Visita_Se_Guardan_Con_El_Permiso_R_030_09()
    {
        await using var host = await ConversacionesHost.StartAsync();
        using var before = await host.CreateAsync(host.Client, host.TechnicianId);
        var chosen = host.Permission(chosen: true);
        host.Permissions.Answer = (_, _, _) => new ResultadoPermisoConversacion(EstadoPermisoConversacion.Concedido, chosen);

        using var after = await host.CreateAsync(host.Client, host.TechnicianId);
        var view = await ConversacionesHost.ReadAsync(after);

        before.StatusCode.Should().Be(HttpStatusCode.Created);
        view.GetProperty("contactoVisible").GetBoolean().Should().BeTrue();
        view.GetProperty("visitaId").GetString().Should().Be(chosen.VisitaId);
        (await host.Conversations.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync())["elegido"].AsBoolean.Should().BeTrue();
    }

    [Fact]
    public async Task Persona_Ajena_Recibe_404_Y_No_Se_Crea_Nada_CA_030_03()
    {
        await using var host = await ConversacionesHost.StartAsync();
        var outsider = Guid.NewGuid().ToString("D");

        using var asClient = await host.CreateAsync(outsider, host.TechnicianId);
        using var asTechnician = await host.CreateAsync(outsider);
        using var wrongTechnician = await host.CreateAsync(host.Client, ObjectId.GenerateNewId().ToString());

        foreach (var response in new[] { asClient, asTechnician, wrongTechnician })
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            var reasons = ProblemBody.Reasons(await response.Content.ReadAsStringAsync());
            reasons.Should().NotContain(host.Client).And.NotContain(host.Technician).And.NotContain(outsider);
            (await ConversacionesHost.ReadAsync(response)).GetProperty("code").GetString().Should().Be("not_found");
        }

        (await host.Conversations.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(0);
    }

    [Fact]
    public async Task Mercado_Sin_Respuesta_Responde_503_Y_No_Guarda_CA_030_09()
    {
        await using var host = await ConversacionesHost.StartAsync();
        host.Permissions.Answer = (_, _, _) => new ResultadoPermisoConversacion(EstadoPermisoConversacion.NoDisponible);

        using var response = await host.CreateAsync(host.Client, host.TechnicianId);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await ConversacionesHost.ReadAsync(response)).GetProperty("code").GetString().Should().Be("market_unavailable");
        (await host.Conversations.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(0);
    }

    [Fact]
    public async Task Crear_Pasa_La_Solicitud_Y_El_Tecnico_Tal_Cual_Al_Permiso_CA_030_15()
    {
        await using var host = await ConversacionesHost.StartAsync();
        var seen = new List<(string Request, string? Technician, string Subject)>();
        host.Permissions.Answer = (request, technician, subject) =>
        {
            seen.Add((request, technician, subject));
            return new ResultadoPermisoConversacion(EstadoPermisoConversacion.NoEncontrado);
        };

        using var response = await host.CreateAsync(host.Client, host.TechnicianId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        seen.Should().Equal((host.RequestId, host.TechnicianId, host.Client));
    }
}
