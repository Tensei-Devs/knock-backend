using System.Net;
using AwesomeAssertions;
using Knock.IntegrationTest.Requests;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Reviews;

/// <summary>Spec 051 (OPI-01 a OPI-03): opiniones y reputación con MongoDB real. Solo datos ficticios; corre en CI.</summary>
public sealed class Spec051OpinionesHttpTests
{
    private static async Task<ConfirmedVisit> FinishedVisitAsync(Spec020Host host, int daysAgo = 1)
    {
        var visit = await host.ConfirmedVisitAsync();
        await host.SetVisitAsync(visit.VisitId, Builders<BsonDocument>.Update.Set("estado", "terminada").Set("terminadaEn", DateTime.UtcNow.AddDays(-daysAgo)));
        await host.Database.GetCollection<BsonDocument>("solicitudes").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", visit.RequestId), Builders<BsonDocument>.Update.Set("estado", "terminada"));
        return visit;
    }

    private static string ReviewPath(ObjectId visitId) => $"/api/v1/visitas/{visitId}/opinion";

    [Fact]
    public async Task Cliente_Califica_Y_La_Reputacion_Y_La_Solicitud_Se_Actualizan_CA_051_07_CA_051_21()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await FinishedVisitAsync(host);

        using var created = await host.SendAsync(HttpMethod.Post, ReviewPath(visit.VisitId), host.ClientSub, new { calificacion = 4, partes = new { calidad = 5 }, guardarTecnicoConfianza = true });
        using var list = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{visit.Technician.Id}/opiniones", Guid.NewGuid().ToString("D"));

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var page = await Spec020Host.ReadAsync(list);
        page.GetProperty("items").GetArrayLength().Should().Be(1);
        page.GetProperty("resumen").GetProperty("total").GetInt32().Should().Be(1);
        page.GetProperty("resumen").GetProperty("promedio").GetDouble().Should().Be(4.0);
        var technician = await host.Database.GetCollection<BsonDocument>("tecnicos").Find(Builders<BsonDocument>.Filter.Eq("_id", visit.Technician.Id)).FirstAsync();
        technician["reputacion"]["total"].ToInt32().Should().Be(1);
        technician["reputacion"]["nivel"].AsString.Should().Be("nuevo");
        technician["calificacion"].ToDouble().Should().Be(4.0, "la búsqueda, la lista y las oleadas ordenan por este campo");
        technician["totalResenas"].ToInt32().Should().Be(1);
        (await host.Database.GetCollection<BsonDocument>("solicitudes").Find(Builders<BsonDocument>.Filter.Eq("_id", visit.RequestId)).FirstAsync())["estado"].AsString.Should().Be("calificada");
        (await host.CountAsync("tecnicos_confianza")).Should().Be(1);
    }

    [Fact]
    public async Task Segunda_Opinion_Visita_No_Terminada_Y_Plazo_Vencido_Responden_409_CA_051_12_CA_051_28()
    {
        await using var host = await Spec020Host.StartAsync();
        var finished = await FinishedVisitAsync(host);
        var open = await host.ConfirmedVisitAsync();
        var late = await FinishedVisitAsync(host, daysAgo: 31);

        using var first = await host.SendAsync(HttpMethod.Post, ReviewPath(finished.VisitId), host.ClientSub, new { calificacion = 5 });
        using var second = await host.SendAsync(HttpMethod.Post, ReviewPath(finished.VisitId), host.ClientSub, new { calificacion = 5 });
        using var notFinished = await host.SendAsync(HttpMethod.Post, ReviewPath(open.VisitId), host.ClientSub, new { calificacion = 5 });
        using var expired = await host.SendAsync(HttpMethod.Post, ReviewPath(late.VisitId), host.ClientSub, new { calificacion = 5 });

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        foreach (var response in new[] { second, notFinished, expired })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("review_not_allowed");
        }

        (await host.CountAsync("opiniones")).Should().Be(1);
    }

    [Fact]
    public async Task Sin_Sesion_Ajeno_Y_Valores_Invalidos_Responden_401_403_422_CA_051_08_CA_051_09_CA_051_10()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await FinishedVisitAsync(host);

        using var anonymous = await host.SendAsync(HttpMethod.Post, ReviewPath(visit.VisitId), body: new { calificacion = 5 });
        using var stranger = await host.SendAsync(HttpMethod.Post, ReviewPath(visit.VisitId), Guid.NewGuid().ToString("D"), new { calificacion = 5 });
        using var zero = await host.SendAsync(HttpMethod.Post, ReviewPath(visit.VisitId), host.ClientSub, new { calificacion = 0 });
        using var unreadable = await host.SendAsync(HttpMethod.Post, ReviewPath(visit.VisitId), host.ClientSub, rawJson: "{no es json");

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        stranger.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        zero.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        unreadable.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await host.CountAsync("opiniones")).Should().Be(0);
    }

    [Fact]
    public async Task Tecnico_Responde_Una_Sola_Vez_Y_Otro_Tecnico_Recibe_403_CA_051_13_CA_051_15_CA_051_18_CA_051_22()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await FinishedVisitAsync(host);
        var other = await host.SeedPublicTechnicianAsync();
        using var created = await host.SendAsync(HttpMethod.Post, ReviewPath(visit.VisitId), host.ClientSub, new { calificacion = 5 });
        var reviewId = (await Spec020Host.ReadAsync(created)).GetProperty("id").GetString();
        var path = $"/api/v1/opiniones/{reviewId}/respuesta";

        using var foreign = await host.SendAsync(HttpMethod.Post, path, other.Sub, new { texto = "Gracias" });
        using var answered = await host.SendAsync(HttpMethod.Post, path, visit.Technician.Sub, new { texto = "Gracias por confiar" });
        using var again = await host.SendAsync(HttpMethod.Post, path, visit.Technician.Sub, new { texto = "Otra vez" });

        foreign.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        answered.StatusCode.Should().Be(HttpStatusCode.OK);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Spec020Host.ReadAsync(again)).GetProperty("code").GetString().Should().Be("review_already_answered");
    }

    [Fact]
    public async Task Opinion_Con_Comentario_Pendiente_No_Sale_En_La_Lista_Y_Al_Aprobarse_Si_CA_051_24()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await FinishedVisitAsync(host);
        using var created = await host.SendAsync(HttpMethod.Post, ReviewPath(visit.VisitId), host.ClientSub, new { calificacion = 5, comentario = "Muy buen trabajo" });
        var id = ObjectId.Parse((await Spec020Host.ReadAsync(created)).GetProperty("id").GetString()!);
        var reviews = host.Database.GetCollection<BsonDocument>("opiniones");
        await reviews.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), Builders<BsonDocument>.Update.Set("moderacion", "pendiente"));

        using var pending = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{visit.Technician.Id}/opiniones?limite=20", Guid.NewGuid().ToString("D"));
        await reviews.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), Builders<BsonDocument>.Update.Set("moderacion", "publicada"));
        using var approved = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{visit.Technician.Id}/opiniones?limite=21", Guid.NewGuid().ToString("D"));

        (await Spec020Host.ReadAsync(pending)).GetProperty("items").GetArrayLength().Should().Be(0);
        (await Spec020Host.ReadAsync(approved)).GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Lista_Valida_Limite_Y_Tecnico_Y_Exige_Sesion_CA_051_02_CA_051_03_CA_051_04_CA_051_05()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await FinishedVisitAsync(host);
        var reader = Guid.NewGuid().ToString("D");

        using var anonymous = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{visit.Technician.Id}/opiniones");
        using var tooMany = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{visit.Technician.Id}/opiniones?limite=51", reader);
        using var missing = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{ObjectId.GenerateNewId()}/opiniones", reader);

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        tooMany.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Spec020Host.ReadAsync(missing)).GetProperty("code").GetString().Should().Be("technician_not_found");
    }

    [Fact]
    public async Task Cuerpo_Grande_Y_Limite_De_Peticiones_Responden_413_Y_429_CA_051_27()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await FinishedVisitAsync(host);

        using var large = await host.SendAsync(HttpMethod.Post, ReviewPath(visit.VisitId), host.ClientSub, rawJson: "{\"comentario\":\"" + new string('a', 4200) + "\"}");
        var last = HttpStatusCode.OK;
        for (var i = 0; i < 31; i++)
        {
            using var response = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{visit.Technician.Id}/opiniones", host.ClientSub);
            last = response.StatusCode;
        }

        large.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        last.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
