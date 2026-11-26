extern alias MarketApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.IntegrationTest.Catalog;
using Knock.IntegrationTest.Requests;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Reviews;

/// <summary>
/// Spec 061: ocultar y mostrar una opinión desde el panel con MongoDB real y la puerta de permisos reemplazada. La reputación, la caché de lectura y el evento <c>OpinionModerada</c> salen de la misma
/// operación; nada se borra y el evento no lleva texto ni motivo. Solo datos ficticios; corre en CI.
/// </summary>
public sealed class Spec061OcultarOpinionTests
{
    private const string _reason = "Lenguaje ofensivo hacia el cliente";
    private static readonly byte[] _panelKey = RandomNumberGenerator.GetBytes(32);

    private static Task<(Spec020Host Host, FakePanelGate Gate)> StartAsync() => PanelTestHost.StartAsync(_panelKey);

    private static string PanelToken() => PanelTestHost.Token(_panelKey);

    private static async Task<HttpResponseMessage> PostAsync(Spec020Host host, string path, object? body = null, string? bearer = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative)) { Content = JsonContent.Create(body ?? new { }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer ?? PanelToken());
        return await host.Client.SendAsync(request);
    }

    private static async Task<(ConfirmedVisit Visit, ObjectId ReviewId)> ReviewedAsync(Spec020Host host, int stars = 2)
    {
        var visit = await host.ConfirmedVisitAsync();
        await host.SetVisitAsync(visit.VisitId, Builders<BsonDocument>.Update.Set("estado", "terminada").Set("terminadaEn", DateTime.UtcNow.AddDays(-1)));
        await host.Database.GetCollection<BsonDocument>("solicitudes").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", visit.RequestId), Builders<BsonDocument>.Update.Set("estado", "terminada"));
        using var created = await host.SendAsync(HttpMethod.Post, $"/api/v1/visitas/{visit.VisitId}/opinion", host.ClientSub, new { calificacion = stars, comentario = "Texto de la opinion ficticia" });
        return (visit, ObjectId.Parse((await Spec020Host.ReadAsync(created)).GetProperty("id").GetString()!));
    }

    private static async Task<System.Text.Json.JsonElement> ListAsync(Spec020Host host, ObjectId technician)
    {
        using var list = await host.SendAsync(HttpMethod.Get, $"/api/v1/tecnicos/{technician}/opiniones", Guid.NewGuid().ToString("D"));
        return await Spec020Host.ReadAsync(list);
    }

    private static Task<long> EventsAsync(Spec020Host host) =>
        host.CountAsync("outbox", Builders<BsonDocument>.Filter.Eq("tipo", "OpinionModerada"));

    [Fact]
    public async Task Ocultar_Saca_La_Opinion_Y_Recalcula_La_Reputacion_Sin_Borrar_CA_061_01_CA_061_02_CA_061_10()
    {
        var (host, _) = await StartAsync();
        await using var scope = host;
        var (visit, reviewId) = await ReviewedAsync(host);
        (await ListAsync(host, visit.Technician.Id)).GetProperty("items").GetArrayLength().Should().Be(1);

        using var hidden = await PostAsync(host, $"/api/v1/admin/opiniones/{reviewId}/ocultar", new { motivo = _reason });

        hidden.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await ListAsync(host, visit.Technician.Id);
        after.GetProperty("items").GetArrayLength().Should().Be(0);
        after.GetProperty("resumen").GetProperty("total").GetInt32().Should().Be(0);
        var technician = await host.Database.GetCollection<BsonDocument>("tecnicos").Find(Builders<BsonDocument>.Filter.Eq("_id", visit.Technician.Id)).FirstAsync();
        technician["reputacion"]["total"].ToInt32().Should().Be(0);
        technician["totalResenas"].ToInt32().Should().Be(0);
        technician.Contains("calificacion").Should().BeFalse("sin opiniones visibles no ordena como si tuviera calificación");
        var review = await host.Database.GetCollection<BsonDocument>("opiniones").Find(Builders<BsonDocument>.Filter.Eq("_id", reviewId)).FirstAsync();
        review["oculta"].AsBoolean.Should().BeTrue();
        review["calificacion"].ToInt32().Should().Be(2);
        review["comentario"].AsString.Should().Be("Texto de la opinion ficticia");
        review["ocultaMotivo"].AsString.Should().Be(_reason);
        review.Contains("ocultaPor").Should().BeTrue();
        var message = await host.Database.GetCollection<BsonDocument>("outbox").Find(Builders<BsonDocument>.Filter.Eq("tipo", "OpinionModerada")).SingleAsync();
        message["datos"]["accion"].AsString.Should().Be("ocultada");
        message["datos"]["opinionId"].AsString.Should().Be(reviewId.ToString());
        message.ToJson().Should().NotContain("Texto de la opinion").And.NotContain(_reason);
    }

    [Fact]
    public async Task Repetir_Ocultar_Es_200_Sin_Evento_Nuevo_Y_Mostrar_La_Devuelve_CA_061_03_CA_061_04()
    {
        var (host, _) = await StartAsync();
        await using var scope = host;
        var (visit, reviewId) = await ReviewedAsync(host);
        var path = $"/api/v1/admin/opiniones/{reviewId}";

        using var first = await PostAsync(host, path + "/ocultar", new { motivo = _reason });
        using var again = await PostAsync(host, path + "/ocultar", new { motivo = _reason });
        using var shown = await PostAsync(host, path + "/mostrar");
        using var shownAgain = await PostAsync(host, path + "/mostrar");

        new[] { first, again, shown, shownAgain }.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        (await EventsAsync(host)).Should().Be(2);
        (await ListAsync(host, visit.Technician.Id)).GetProperty("resumen").GetProperty("total").GetInt32().Should().Be(1);
        var review = await host.Database.GetCollection<BsonDocument>("opiniones").Find(Builders<BsonDocument>.Filter.Eq("_id", reviewId)).FirstAsync();
        review["oculta"].AsBoolean.Should().BeFalse();
        review.Contains("ocultaPor").Should().BeFalse();
    }

    [Fact]
    public async Task Motivo_Invalido_Id_Mal_Formado_E_Inexistente_Responden_422_400_404_CA_061_06_CA_061_07()
    {
        var (host, _) = await StartAsync();
        await using var scope = host;
        var (_, reviewId) = await ReviewedAsync(host);

        using var shortReason = await PostAsync(host, $"/api/v1/admin/opiniones/{reviewId}/ocultar", new { motivo = "corto" });
        using var longReason = await PostAsync(host, $"/api/v1/admin/opiniones/{reviewId}/ocultar", new { motivo = new string('x', 301) });
        using var malformed = await PostAsync(host, "/api/v1/admin/opiniones/no-es-un-id/ocultar", new { motivo = _reason });
        using var missing = await PostAsync(host, $"/api/v1/admin/opiniones/{ObjectId.GenerateNewId()}/ocultar", new { motivo = _reason });

        shortReason.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        longReason.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Spec020Host.ReadAsync(shortReason)).GetProperty("errors").TryGetProperty("motivo", out _).Should().BeTrue();
        (await shortReason.Content.ReadAsStringAsync()).Should().NotContain("corto");
        malformed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Spec020Host.ReadAsync(missing)).GetProperty("code").GetString().Should().Be("review_not_found");
        (await EventsAsync(host)).Should().Be(0);
    }

    [Fact]
    public async Task Dos_Peticiones_Simultaneas_Responden_200_Y_Dejan_Un_Solo_Evento_CA_061_08()
    {
        var (host, _) = await StartAsync();
        await using var scope = host;
        var (_, reviewId) = await ReviewedAsync(host);

        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => PostAsync(host, $"/api/v1/admin/opiniones/{reviewId}/ocultar", new { motivo = _reason })));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        (await EventsAsync(host)).Should().Be(1);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }
}
