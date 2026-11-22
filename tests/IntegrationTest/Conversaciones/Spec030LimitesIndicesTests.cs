using System.Net;
using AwesomeAssertions;
using Knock.Contracts.Conversations;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Conversaciones;

/// <summary>El límite de creación (spec 030, R-11) y los índices que dejan listos las pruebas (R-01 y R-21).</summary>
public sealed class Spec030LimitesIndicesTests
{
    [Fact]
    public async Task Exige_Sesion_Y_Limites_CA_030_20()
    {
        await using var host = await ConversacionesHost.StartAsync();
        var person = Guid.NewGuid().ToString("D");
        host.Permissions.Answer = (_, _, _) => new ResultadoPermisoConversacion(EstadoPermisoConversacion.NoEncontrado);

        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? rejected = null;
        for (var i = 0; i < 21; i++)
        {
            var response = await host.CreateAsync(person, host.TechnicianId);
            statuses.Add(response.StatusCode);
            if (i == 20)
            {
                rejected = response;
            }
            else
            {
                response.Dispose();
            }
        }

        using (rejected)
        {
            statuses.Take(20).Should().OnlyContain(status => status == HttpStatusCode.NotFound, "las primeras 20 llegan al permiso");
            statuses[20].Should().Be(HttpStatusCode.TooManyRequests, "la 21 de la misma hora se corta");
            rejected!.Headers.RetryAfter.Should().NotBeNull();
            (await ConversacionesHost.ReadAsync(rejected)).GetProperty("code").GetString().Should().Be("rate_limited");
        }

        host.Permissions.Calls.Should().Be(20, "la 21 no llega a consultar a Mercado");
        using var other = await host.CreateAsync(Guid.NewGuid().ToString("D"), host.TechnicianId);
        other.StatusCode.Should().Be(HttpStatusCode.NotFound, "el límite es por persona");
    }

    [Fact]
    public async Task Los_Indices_Llevan_Los_Nombres_Del_Script_Y_El_Ttl_De_180_Dias_CA_030_19()
    {
        await using var host = await ConversacionesHost.StartAsync();

        var conversations = await (await host.Conversations.Indexes.ListAsync()).ToListAsync();
        var messages = await (await host.Messages.Indexes.ListAsync()).ToListAsync();

        conversations.Select(i => i["name"].AsString).Should().Contain(["solicitudId_1_tecnicoId_1", "clienteId_1_ultimoMensajeEn_-1__id_-1", "tecnicoUsuarioId_1_ultimoMensajeEn_-1__id_-1"]);
        conversations.Single(i => i["name"].AsString == "solicitudId_1_tecnicoId_1")["unique"].AsBoolean.Should().BeTrue();
        messages.Select(i => i["name"].AsString).Should().Contain(["conversacionId_1_creadoEn_-1__id_-1", "creadoEn_1"]);
        messages.Single(i => i["name"].AsString == "creadoEn_1")["expireAfterSeconds"].ToInt64().Should().Be(180L * 24 * 60 * 60);
    }

    [Fact]
    public async Task El_Par_Es_Unico_En_La_Base_CA_030_15()
    {
        await using var host = await ConversacionesHost.StartAsync();
        await host.SeedConversationAsync(request: host.RequestId);

        var duplicate = async () => await host.SeedConversationAsync(request: host.RequestId);

        (await duplicate.Should().ThrowAsync<MongoWriteException>()).Which.WriteError.Category.Should().Be(ServerErrorCategory.DuplicateKey);
        (await host.Conversations.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1);
    }
}
