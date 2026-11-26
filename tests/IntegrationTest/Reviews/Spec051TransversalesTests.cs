using System.Net;
using AwesomeAssertions;
using Knock.IntegrationTest.Requests;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Reviews;

/// <summary>Spec 051, T-06 (R-18 a R-21): la persona es la del <c>sub</c>, registros sin datos personales, 429 y 413, y sin <c>Idempotency-Key</c>. Solo datos ficticios; corre en CI.</summary>
public sealed class Spec051TransversalesTests
{
    private const string _commentMarker = "Comentario-privado-del-cliente-051";

    private static async Task<ConfirmedVisit> FinishedVisitAsync(Spec020Host host)
    {
        var visit = await host.ConfirmedVisitAsync();
        await host.SetVisitAsync(visit.VisitId, Builders<BsonDocument>.Update.Set("estado", "terminada").Set("terminadaEn", DateTime.UtcNow.AddDays(-1)));
        await host.Database.GetCollection<BsonDocument>("solicitudes").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", visit.RequestId), Builders<BsonDocument>.Update.Set("estado", "terminada"));
        return visit;
    }

    private static string ReviewPath(ObjectId visitId) => $"/api/v1/visitas/{visitId}/opinion";

    private static Dictionary<string, object?> Foreign(string otherPerson, string key, object? value) => new()
    {
        [key] = value,
        ["sub"] = otherPerson,
        ["clienteId"] = otherPerson,
        ["tecnicoId"] = otherPerson,
        ["usuarioId"] = otherPerson,
    };

    [Fact]
    public async Task IdentificadorAjeno_En_El_Cuerpo_Se_Ignora_CA_051_25()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await FinishedVisitAsync(host);
        var stranger = Guid.NewGuid().ToString("D");

        // Un extraño que nombra al cliente verdadero en el cuerpo no actúa en su nombre: la persona es la del token.
        using var impersonated = await host.SendAsync(HttpMethod.Post, ReviewPath(visit.VisitId), stranger, Foreign(host.ClientSub, "calificacion", 5));
        // El dueño con identificadores ajenos en el cuerpo opera igual que sin ellos.
        using var created = await host.SendAsync(HttpMethod.Post, ReviewPath(visit.VisitId), host.ClientSub, Foreign(stranger, "calificacion", 5));
        var reviewId = (await Spec020Host.ReadAsync(created)).GetProperty("id").GetString();
        var path = $"/api/v1/opiniones/{reviewId}/respuesta";
        using var impersonatedAnswer = await host.SendAsync(HttpMethod.Post, path, stranger, Foreign(visit.Technician.Sub, "texto", "Gracias"));
        using var answered = await host.SendAsync(HttpMethod.Post, path, visit.Technician.Sub, Foreign(stranger, "texto", "Gracias por confiar"));

        impersonated.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        impersonatedAnswer.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        answered.StatusCode.Should().Be(HttpStatusCode.OK);
        (await host.CountAsync("opiniones")).Should().Be(1);
    }

    [Fact]
    public async Task Registros_Y_Errores_Sin_Datos_Personales_CA_051_26()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await FinishedVisitAsync(host);
        var errorBody = string.Empty;

        var output = await ConsoleCapture.RunAsync(async () =>
        {
            using var invalid = await host.SendAsync(HttpMethod.Post, ReviewPath(visit.VisitId), host.ClientSub, rawJson: "{\"calificacion\":0,\"comentario\":\"" + _commentMarker + "\"}");
            errorBody = await invalid.Content.ReadAsStringAsync();
            using var created = await host.SendAsync(HttpMethod.Post, ReviewPath(visit.VisitId), host.ClientSub, new { calificacion = 5, comentario = _commentMarker + " muy buen trabajo" });
            var reviewId = (await Spec020Host.ReadAsync(created)).GetProperty("id").GetString();
            using var answered = await host.SendAsync(HttpMethod.Post, $"/api/v1/opiniones/{reviewId}/respuesta", visit.Technician.Sub, new { texto = _commentMarker + " respuesta" });
            using var list = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{visit.Technician.Id}/opiniones", host.ClientSub);
        });

        output.Should().NotContain(_commentMarker, "los textos escritos por la persona no se registran")
            .And.NotContain(host.ClientSub, "el cliente no se registra")
            .And.NotContain(visit.Technician.Sub, "el técnico no se registra")
            .And.NotContain(Spec021Host.TechnicianPhone);
        errorBody.Should().NotContain(_commentMarker, "el error nunca repite lo enviado");
        errorBody.Should().Contain("correlationId").And.Contain("traceId");
    }

    [Fact]
    public async Task Supera_El_Limite_Responde_429_Con_RetryAfter_CA_051_27()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await FinishedVisitAsync(host);
        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? last = null;

        for (var i = 0; i < 31; i++)
        {
            last?.Dispose();
            last = await host.SendAsync(HttpMethod.Post, ReviewPath(visit.VisitId), host.ClientSub, new { calificacion = 5 });
            statuses.Add(last.StatusCode);
        }

        using (last)
        {
            statuses[0].Should().Be(HttpStatusCode.Created);
            statuses[30].Should().Be(HttpStatusCode.TooManyRequests);
            last!.Headers.RetryAfter.Should().NotBeNull();
        }
    }

    [Theory]
    [InlineData("visit", 4200)]
    [InlineData("answer", 2200)]
    public async Task Cuerpo_Mayor_Al_Tope_Responde_413_CA_051_27(string route, int size)
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await FinishedVisitAsync(host);
        using var created = await host.SendAsync(HttpMethod.Post, ReviewPath(visit.VisitId), host.ClientSub, new { calificacion = 5 });
        var reviewId = (await Spec020Host.ReadAsync(created)).GetProperty("id").GetString();
        var path = route == "visit" ? ReviewPath(visit.VisitId) : $"/api/v1/opiniones/{reviewId}/respuesta";
        var sender = route == "visit" ? host.ClientSub : visit.Technician.Sub;

        using var response = await host.SendAsync(HttpMethod.Post, path, sender, rawJson: "{\"texto\":\"" + new string('a', size) + "\"}");

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("payload_too_large");
    }

    [Fact]
    public async Task Repetir_La_Opinion_Y_La_Respuesta_No_Duplica_Ni_Pide_IdempotencyKey_CA_051_28()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await FinishedVisitAsync(host);

        using var first = await host.SendAsync(HttpMethod.Post, ReviewPath(visit.VisitId), host.ClientSub, new { calificacion = 4 });
        using var second = await host.SendAsync(HttpMethod.Post, ReviewPath(visit.VisitId), host.ClientSub, new { calificacion = 4 });
        var reviewId = (await Spec020Host.ReadAsync(first)).GetProperty("id").GetString();
        var path = $"/api/v1/opiniones/{reviewId}/respuesta";
        using var answer = await host.SendAsync(HttpMethod.Post, path, visit.Technician.Sub, new { texto = "Gracias por confiar" });
        using var answerAgain = await host.SendAsync(HttpMethod.Post, path, visit.Technician.Sub, new { texto = "Gracias por confiar" });

        first.StatusCode.Should().Be(HttpStatusCode.Created, "no exige Idempotency-Key");
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        answer.StatusCode.Should().Be(HttpStatusCode.OK);
        answerAgain.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await host.CountAsync("opiniones")).Should().Be(1);
        (await host.CountAsync("outbox", Builders<BsonDocument>.Filter.Eq("tipo", "OpinionCreada"))).Should().Be(1);
    }
}
