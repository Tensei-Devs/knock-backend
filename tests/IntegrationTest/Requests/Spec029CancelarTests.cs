using System.Net;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>Spec 029, T-03 y T-05: <c>POST /api/v1/solicitudes/{id}/cancelar</c> y el detalle de solicitudes cerradas (MongoDB real; solo corren en CI).</summary>
public sealed class Spec029CancelarTests
{
    private static async Task<ObjectId> CreateRequestAsync(Spec020Host host)
    {
        using var created = await host.CreateAsync(host.ValidBody());
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        return ObjectId.Parse((await Spec020Host.ReadAsync(created)).GetProperty("id").GetString());
    }

    private static Task<HttpResponseMessage> CancelAsync(Spec020Host host, string id, string? sub, object? body = null) =>
        host.SendAsync(HttpMethod.Post, $"/api/v1/solicitudes/{id}/cancelar", sub, body);

    private static Task<UpdateResult> SetStateAsync(Spec020Host host, ObjectId id, string state) =>
        host.Database.GetCollection<BsonDocument>("solicitudes").UpdateOneAsync(new BsonDocument("_id", id), new BsonDocument("$set", new BsonDocument("estado", state)));

    private static async Task<string> StateOfAsync(Spec020Host host, ObjectId id) =>
        (await host.Database.GetCollection<BsonDocument>("solicitudes").Find(new BsonDocument("_id", id)).SingleAsync())["estado"].AsString;

    private static Task<long> CancelledEventsAsync(Spec020Host host, ObjectId id) =>
        host.Database.GetCollection<BsonDocument>("outbox").CountDocumentsAsync(new BsonDocument("_id", "SolicitudCancelada:" + id));

    [Fact]
    public async Task Cancela_En_Cascada_CA_029_01()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await CreateRequestAsync(host);
        await SetStateAsync(host, id, "cotizando");
        var quotes = host.Database.GetCollection<BsonDocument>("cotizaciones");
        var deliveries = host.Database.GetCollection<BsonDocument>("envios");
        var technicians = new[] { ObjectId.GenerateNewId(), ObjectId.GenerateNewId() };
        await quotes.InsertManyAsync(technicians.Select(t => new BsonDocument { { "solicitudId", id }, { "tecnicoId", t }, { "estado", "enviada" } }));
        await deliveries.InsertManyAsync(Enumerable.Range(0, 3).Select(_ => new BsonDocument { { "solicitudId", id }, { "estado", "activo" } }));

        using var response = await CancelAsync(host, id.ToString(), host.ClientSub, new { motivo = "other" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var body = await Spec020Host.ReadAsync(response);
        body.GetProperty("estado").GetString().Should().Be("cancelada");
        body.GetProperty("motivo").GetString().Should().Be("other");
        (await StateOfAsync(host, id)).Should().Be("cancelada");
        (await quotes.Find(new BsonDocument("solicitudId", id)).ToListAsync()).Should().OnlyContain(q => q["estado"] == "rechazada" && q["motivoRechazo"] == "request_cancelled");
        (await deliveries.Find(new BsonDocument("solicitudId", id)).ToListAsync()).Should().OnlyContain(d => d["estado"] == "cerrado" && d["cerradoMotivo"] == "request_cancelled");
        var message = await host.Database.GetCollection<BsonDocument>("outbox").Find(new BsonDocument("_id", "SolicitudCancelada:" + id)).SingleAsync();
        message["datos"]["tecnicosAvisar"].AsBsonArray.Select(x => x.AsString).Should().BeEquivalentTo(technicians.Select(t => t.ToString()));
    }

    [Theory]
    [InlineData("cita_confirmada")]
    [InlineData("terminada")]
    [InlineData("calificada")]
    public async Task Rechaza_Con_Cita_CA_029_02(string state)
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await CreateRequestAsync(host);
        await SetStateAsync(host, id, state);

        using var response = await CancelAsync(host, id.ToString(), host.ClientSub);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("request_with_appointment");
        (await StateOfAsync(host, id)).Should().Be(state);
        (await CancelledEventsAsync(host, id)).Should().Be(0);
    }

    [Fact]
    public async Task Oculta_Solicitud_Ajena_CA_029_03()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await CreateRequestAsync(host);

        using var other = await CancelAsync(host, id.ToString(), Guid.NewGuid().ToString("D"));
        using var missing = await CancelAsync(host, ObjectId.GenerateNewId().ToString(), host.ClientSub);

        other.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await StateOfAsync(host, id)).Should().Be("enviada");
    }

    [Fact]
    public async Task Cancelar_Es_Idempotente_CA_029_04()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await CreateRequestAsync(host);

        using var first = await CancelAsync(host, id.ToString(), host.ClientSub, new { motivo = "solved_elsewhere" });
        using var second = await CancelAsync(host, id.ToString(), host.ClientSub, new { motivo = "other" });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Spec020Host.ReadAsync(second)).GetRawText().Should().Be((await Spec020Host.ReadAsync(first)).GetRawText(), "devuelve el cuerpo de la primera cancelación");
        (await CancelledEventsAsync(host, id)).Should().Be(1);
    }

    [Fact]
    public async Task Cancelaciones_Simultaneas_Emiten_Un_Solo_Evento_CA_029_05()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await CreateRequestAsync(host);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => CancelAsync(host, id.ToString(), host.ClientSub)));

        try
        {
            responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
            (await CancelledEventsAsync(host, id)).Should().Be(1);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Estados_Finales_CA_029_11()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await CreateRequestAsync(host);
        await SetStateAsync(host, id, "vencida");

        using var response = await CancelAsync(host, id.ToString(), host.ClientSub);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("request_expired");
    }

    [Fact]
    public async Task Rechaza_Cuerpo_Invalido_CA_029_12()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await CreateRequestAsync(host);

        using var badReason = await CancelAsync(host, id.ToString(), host.ClientSub, new { motivo = "inventado" });
        using var badId = await CancelAsync(host, "no-es-un-id", host.ClientSub);

        badReason.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Spec020Host.ReadAsync(badReason)).GetProperty("errors").GetProperty("motivo").GetArrayLength().Should().BeGreaterThan(0);
        badId.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await StateOfAsync(host, id)).Should().Be("enviada");
    }

    [Fact]
    public async Task Exige_Sesion_Y_Limite_CA_029_13()
    {
        await using var host = await Spec020Host.StartAsync();
        var ids = new List<ObjectId>();
        for (var i = 0; i < 10; i++)
        {
            ids.Add(await CreateRequestAsync(host));
        }

        using var anonymous = await CancelAsync(host, ids[0].ToString(), sub: null);
        foreach (var id in ids)
        {
            using var ok = await CancelAsync(host, id.ToString(), host.ClientSub);
            ok.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var limited = await CancelAsync(host, ids[0].ToString(), host.ClientSub);

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.Contains("Retry-After").Should().BeTrue();
    }

    [Theory]
    [InlineData("cancelada")]
    [InlineData("vencida")]
    public async Task Detalle_De_Solicitud_Cerrada_CA_029_10(string state)
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await CreateRequestAsync(host);
        await SetStateAsync(host, id, state);

        using var detail = await host.SendAsync(HttpMethod.Get, $"/api/v1/solicitudes/{id}", host.ClientSub);
        using var list = await host.SendAsync(HttpMethod.Get, $"/api/v1/solicitudes?estado={state}", host.ClientSub);

        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await Spec020Host.ReadAsync(detail);
        body.GetProperty("puedeVolverAPedir").GetBoolean().Should().BeTrue();
        body.GetProperty("direccion").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("descripcion").GetString().Should().NotBeNullOrEmpty();
        (await Spec020Host.ReadAsync(list)).GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()).Should().Contain(id.ToString());
    }

    [Fact]
    public async Task Puede_Volver_A_Pedir_Es_Falso_En_Solicitud_Abierta_CA_029_10()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await CreateRequestAsync(host);

        using var detail = await host.SendAsync(HttpMethod.Get, $"/api/v1/solicitudes/{id}", host.ClientSub);

        (await Spec020Host.ReadAsync(detail)).GetProperty("puedeVolverAPedir").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Tecnico_Que_Cotizo_No_Ve_Solicitud_Cerrada_CA_029_10()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await CreateRequestAsync(host);
        var technician = Guid.NewGuid().ToString("D");
        await host.SeedTechnicianAsync(technician, "activo", hidden: false, [Spec020Host.Plumbing], host.ColoniaId);
        await host.Database.GetCollection<BsonDocument>("cotizaciones").InsertOneAsync(
            new BsonDocument { { "solicitudId", id }, { "tecnicoId", ObjectId.GenerateNewId() }, { "tecnicoUsuarioId", technician }, { "estado", "enviada" } });
        using var before = await host.SendAsync(HttpMethod.Get, $"/api/v1/solicitudes/{id}", technician);
        await SetStateAsync(host, id, "cancelada");

        using var after = await host.SendAsync(HttpMethod.Get, $"/api/v1/solicitudes/{id}", technician);

        before.StatusCode.Should().Be(HttpStatusCode.OK);
        after.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
