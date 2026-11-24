extern alias MarketApi;

using System.Globalization;
using MarketApi::Knock.Market.Api.Modules.Requests.Services;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.IntegrationTest.Requests;

/// <summary>Fotos de trabajo de mentira: todas existen, son del técnico y están listas (el módulo de archivos real no entra en estas pruebas).</summary>
internal sealed class FakeVisitPhotos : IVisitPhotos
{
    public Task<bool> AreReadyForAsync(string ownerId, IReadOnlyList<string> photoIds, CancellationToken cancellationToken) => Task.FromResult(true);
}

/// <summary>Apoyo de las pruebas de integración de la spec 050 (agenda, cambios y cierre de la visita) sobre el host de la 020: MongoDB real y solo datos ficticios.</summary>
internal static class Spec050Host
{
    public const string Reason = "Cambio de planes del cliente";
    public const string Work = "Se cambio la llave y se sello la conexion";

    public static Task<Spec020Host> StartAsync() =>
        Spec020Host.StartAsync(configure: services => services.AddSingleton<IVisitPhotos>(new FakeVisitPhotos()));

    /// <summary>Pone la cita en el instante UTC dado (se guarda como día y hora de la Ciudad de México).</summary>
    public static Task SetAppointmentAsync(this Spec020Host host, ObjectId visitId, DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City"));
        return host.SetVisitAsync(visitId, Builders<BsonDocument>.Update
            .Set("dia", local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Set("hora", local.ToString("HH:mm", CultureInfo.InvariantCulture)));
    }

    public static Task<HttpResponseMessage> PostAsync(this Spec020Host host, ObjectId visitId, string route, string? sub, object? body = null, string? rawJson = null) =>
        host.SendAsync(HttpMethod.Post, $"/api/v1/visitas/{visitId}/{route}", sub, rawJson is null ? body : null, rawJson);

    public static Task<HttpResponseMessage> PutPermissionAsync(this Spec020Host host, ObjectId visitId, string? sub, object? body = null, string? rawJson = null) =>
        host.SendAsync(HttpMethod.Put, $"/api/v1/visitas/{visitId}/permiso-fotos", sub, rawJson is null ? body : null, rawJson);

    public static async Task<string> CodeOfAsync(HttpResponseMessage response) => (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString()!;

    public static object FinishBody() => new { descripcion = Work, recomendacion = "Revisar la llave en seis meses", fotos = new[] { Guid.NewGuid().ToString("N") } };

    /// <summary>Visita <c>en_curso</c> lista para terminarla.</summary>
    public static async Task<ConfirmedVisit> InProgressVisitAsync(this Spec020Host host)
    {
        var visit = await host.ConfirmedVisitAsync();
        await host.SetVisitAsync(visit.VisitId, Builders<BsonDocument>.Update.Set("estado", "en_curso").Set("llegadaEn", DateTime.UtcNow));
        return visit;
    }

    /// <summary>Visita <c>terminada</c> hace el tiempo dado.</summary>
    public static async Task<ConfirmedVisit> FinishedVisitAsync(this Spec020Host host, TimeSpan ago)
    {
        var visit = await host.ConfirmedVisitAsync();
        await host.SetVisitAsync(visit.VisitId, Builders<BsonDocument>.Update.Set("estado", "terminada").Set("terminadaEn", DateTime.UtcNow - ago));
        return visit;
    }

    /// <summary>Visita confirmada con la cita a la distancia dada de ahora (negativa: ya pasó).</summary>
    public static async Task<ConfirmedVisit> VisitAtAsync(this Spec020Host host, TimeSpan fromNow)
    {
        var visit = await host.ConfirmedVisitAsync();
        await host.SetAppointmentAsync(visit.VisitId, DateTime.UtcNow + fromNow);
        return visit;
    }

    /// <summary>Cuántos eventos del tipo dado hay en el outbox para el agregado.</summary>
    public static Task<long> OutboxCountOfAsync(this Spec020Host host, string type, string aggregateId) =>
        host.CountAsync("outbox", Builders<BsonDocument>.Filter.Eq("tipo", type) & Builders<BsonDocument>.Filter.Eq("agregadoId", aggregateId));

    /// <summary>Los eventos del outbox del tipo dado.</summary>
    public static Task<List<BsonDocument>> OutboxEventsOfTypeAsync(this Spec020Host host, string type) =>
        host.Database.GetCollection<BsonDocument>("outbox").Find(Builders<BsonDocument>.Filter.Eq("tipo", type)).ToListAsync();
}
