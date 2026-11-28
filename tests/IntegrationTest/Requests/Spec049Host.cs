extern alias MarketApi;

using System.Net;
using AwesomeAssertions;
using MarketApi::Knock.Market.Api.Common.TareasProgramadas;
using MarketApi::Knock.Market.Api.Modules.Requests.Services;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.IntegrationTest.Requests;

/// <summary>Una visita en curso con la cotización elegida: la visita, la cotización, el técnico y el tope aprobado.</summary>
internal sealed record InProgressVisit(ObjectId VisitId, ObjectId QuoteId, SeededTechnician Technician, int Cap);

/// <summary>Apoyo de las pruebas de la spec 049 (cotización final y ajustes de precio) sobre el host de la 020: MongoDB real, moderación que aprueba y solo datos ficticios.</summary>
internal static class Spec049Host
{
    public const string Description = "Cambio de la llave completa y sellado de la conexion";
    public const string Reason = "Hubo que reemplazar tambien la manguera";
    public const int Price = 850;
    public const int Cap = 1000;

    /// <summary>Host con un moderador de notas que aprueba (el real falla cerrado sin el servicio de moderación).</summary>
    public static Task<Spec020Host> StartAsync() =>
        Spec020Host.StartAsync(configure: services => services.AddSingleton<INoteModerator>(new ApprovingNoteModerator()));

    /// <summary>Crea una solicitud, elige una cotización del tipo dado por la API y deja la visita <c>en_curso</c>.</summary>
    public static async Task<InProgressVisit> InProgressAsync(this Spec020Host host, string quoteType = "visita", int cap = Cap)
    {
        var requestId = await host.CreateRequestAsync();
        await host.SetRequestAsync(requestId, "cotizando");
        var technician = await host.SeedPublicTechnicianAsync();
        var quote = await host.SeedQuoteAsync(requestId, technician, type: quoteType, price: cap);
        using var chosen = await host.ChooseAsync(requestId, quote);
        chosen.EnsureSuccessStatusCode();
        var visit = await host.Database.GetCollection<BsonDocument>("visitas").Find(Builders<BsonDocument>.Filter.Eq("solicitudId", requestId)).SingleAsync();
        var visitId = visit["_id"].AsObjectId;
        await host.SetVisitAsync(visitId, Builders<BsonDocument>.Update.Set("estado", "en_curso").Set("llegadaEn", DateTime.UtcNow));
        return new InProgressVisit(visitId, quote, technician, cap);
    }

    public static Task<HttpResponseMessage> SendFinalQuoteAsync(this Spec020Host host, ObjectId visitId, string? sub, object? body = null, string? rawJson = null) =>
        host.SendAsync(HttpMethod.Post, $"/api/v1/visitas/{visitId}/cotizacion-final", sub, rawJson is null ? body ?? new { precio = Price, descripcion = Description } : null, rawJson);

    public static Task<HttpResponseMessage> DecideFinalQuoteAsync(this Spec020Host host, ObjectId visitId, string? sub, object? body = null, string? rawJson = null) =>
        host.SendAsync(HttpMethod.Post, $"/api/v1/visitas/{visitId}/cotizacion-final/decision", sub, rawJson is null ? body ?? new { decision = "aprueba" } : null, rawJson);

    public static Task<HttpResponseMessage> ProposeAdjustmentAsync(this Spec020Host host, ObjectId quoteId, string? sub, object? body = null, string? rawJson = null) =>
        host.SendAsync(HttpMethod.Post, $"/api/v1/cotizaciones/{quoteId}/ajuste-precio", sub, rawJson is null ? body ?? new { montoNuevo = Cap + 500, motivo = Reason } : null, rawJson);

    public static Task<HttpResponseMessage> DecideAdjustmentAsync(this Spec020Host host, ObjectId adjustmentId, string? sub, object? body = null, string? rawJson = null) =>
        host.SendAsync(HttpMethod.Post, $"/api/v1/ajustes/{adjustmentId}/decision", sub, rawJson is null ? body ?? new { decision = "aprueba" } : null, rawJson);

    /// <summary>Envía el ajuste como el técnico de la visita y devuelve el id del ajuste creado.</summary>
    public static async Task<ObjectId> ProposeAsync(this Spec020Host host, InProgressVisit visit, int newAmount = Cap + 500)
    {
        using var response = await host.ProposeAdjustmentAsync(visit.QuoteId, visit.Technician.Sub, new { montoNuevo = newAmount, motivo = Reason });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return ObjectId.Parse((await Spec020Host.ReadAsync(response)).GetProperty("id").GetString()!);
    }

    public static Task<BsonDocument> AdjustmentDocAsync(this Spec020Host host, ObjectId id) => host.OneAsync("ajustes_precio", id);

    public static Task<BsonDocument> QuoteDocAsync(this Spec020Host host, ObjectId id) => host.OneAsync("cotizaciones", id);

    public static Task<long> OutboxOfAsync(this Spec020Host host, string type, string aggregateId) =>
        host.CountAsync("outbox", Builders<BsonDocument>.Filter.Eq("tipo", type) & Builders<BsonDocument>.Filter.Eq("agregadoId", aggregateId));

    public static Task<List<BsonDocument>> OutboxEventsAsync(this Spec020Host host, string type) =>
        host.Database.GetCollection<BsonDocument>("outbox").Find(Builders<BsonDocument>.Filter.Eq("tipo", type)).ToListAsync();

    /// <summary>Corre el manejador de la tarea de vencimiento (<c>VencerCotizacionFinal</c> o <c>VencerAjuste</c>) para la clave dada.</summary>
    public static async Task RunExpiryAsync(this Spec020Host host, string type, ObjectId key)
    {
        using var scope = host.Services.CreateScope();
        var handler = scope.ServiceProvider.GetServices<IManejadorDeTarea>().Single(h => h.Tipo == type);
        await handler.HandleAsync(new TareaReservada(ObjectId.GenerateNewId(), type, key.ToString(), "x", 0), CancellationToken.None);
    }
}
