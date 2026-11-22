using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Requests.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Requests.Data;

/// <summary>
/// Cierre de la visita (spec 050, VIS-09 a VIS-11): terminar con fotos, reporte de «no terminado» y permiso de fotos. Cada cambio es una actualización condicionada por el estado
/// y se escribe junto con su evento del buzón (y las tareas de cierre, si corresponde) en la misma transacción; los eventos solo llevan identificadores (R-32). Dos peticiones
/// simultáneas no pueden ganar las dos. Único punto del módulo con acceso a MongoDB para estas operaciones.
/// </summary>
internal sealed class VisitCloseStore(
    [FromKeyedServices(RequestsModuleExtensions.ModuleKey)] IMongoDatabase database,
    IOutbox outbox)
{
    private const string _stateField = "estado";
    private const string _updatedField = "actualizadoEn";
    private const string _permissionField = "permisoPublicarFotos";

    private IMongoCollection<BsonDocument> Visits => database.GetCollection<BsonDocument>(ChoiceStore.VisitsCollection);

    /// <summary>
    /// Termina la visita <c>en_curso</c> con lo que hizo y sus fotos (privadas), escribe <c>VisitaTerminada</c> y programa <c>CerrarPeriodoDeOpinion</c> (30 días) y
    /// <c>CerrarReporteDeNoTerminado</c> (7 días) en la misma transacción. No hay confirmación del cliente. <see langword="false"/> si la visita ya no estaba <c>en_curso</c>.
    /// </summary>
    public Task<bool> FinishAsync(VisitRecord visit, string description, string? recommendation, IReadOnlyList<string> photoIds, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(visit);
        return MarketTransactions.RunAsync(database, async (transaction, token) =>
        {
            var work = new BsonDocument { { "descripcion", description }, { "fotos", new BsonArray(photoIds) } };
            if (recommendation is not null)
            {
                work.Add("recomendacion", recommendation);
            }

            var changed = await Visits.UpdateOneAsync(
                transaction,
                Builders<BsonDocument>.Filter.Eq("_id", visit.Id) & Builders<BsonDocument>.Filter.Eq(_stateField, VisitRules.InProgress),
                Builders<BsonDocument>.Update.Set(_stateField, VisitCloseRules.Finished).Set("terminadaEn", nowUtc).Set("trabajo", work).Set(_updatedField, nowUtc),
                cancellationToken: token);
            if (changed.MatchedCount != 1)
            {
                return false;
            }

            await outbox.AddAsync(transaction, OutboxMessage.Create(VisitCloseRules.FinishedEvent, visit.Id.ToString(), VisitStore.Identifiers(visit), nowUtc), token);
            var tasks = new TareasProgramadasStore(database);
            await tasks.ScheduleByKeyAsync(transaction, VisitCloseRules.CloseOpinionTask, visit.Id.ToString(), nowUtc + VisitCloseRules.OpinionPeriod, nowUtc, token);
            await tasks.ScheduleByKeyAsync(transaction, VisitCloseRules.CloseReportTask, visit.Id.ToString(), nowUtc + VisitCloseRules.ReportWindow, nowUtc, token);
            return true;
        }, false, cancellationToken);
    }

    /// <summary>
    /// Guarda el reporte de «no terminado» de una visita <c>terminada</c> dentro de los 7 días y sin reporte previo (una sola vez), en revisión, y escribe
    /// <c>ReporteNoTerminadoCreado</c> con identificadores, sin el texto (R-32). <see langword="false"/> si el plazo pasó, ya se reportó o la ganó otra petición.
    /// </summary>
    public Task<bool> ReportNotFinishedAsync(VisitRecord visit, string text, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(visit);
        return MarketTransactions.RunAsync(database, async (transaction, token) =>
        {
            var filter = Builders<BsonDocument>.Filter;
            var changed = await Visits.UpdateOneAsync(
                transaction,
                filter.Eq("_id", visit.Id) & filter.Eq(_stateField, VisitCloseRules.Finished) & filter.Exists("reporteNoTerminado", false) & filter.Gte("terminadaEn", nowUtc - VisitCloseRules.ReportWindow),
                Builders<BsonDocument>.Update
                    .Set("reporteNoTerminado", new BsonDocument { { "texto", text }, { _stateField, VisitCloseRules.UnderReview }, { "en", nowUtc } })
                    .Set(_updatedField, nowUtc),
                cancellationToken: token);
            if (changed.MatchedCount != 1)
            {
                return false;
            }

            await outbox.AddAsync(transaction, OutboxMessage.Create(VisitCloseRules.NotFinishedEvent, visit.Id.ToString(), VisitStore.Identifiers(visit), nowUtc), token);
            return true;
        }, false, cancellationToken);
    }

    /// <summary>
    /// Guarda el permiso del cliente de publicar las fotos del trabajo. Si cambia, escribe <c>PermisoFotosCambiado</c> (quien publica o retira las fotos lo consume); repetir el
    /// mismo valor no cambia nada ni escribe evento.
    /// </summary>
    public Task<bool> SetPhotoPermissionAsync(VisitRecord visit, bool allowed, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(visit);
        return MarketTransactions.RunAsync(database, async (transaction, token) =>
        {
            var filter = Builders<BsonDocument>.Filter;
            var changed = await Visits.UpdateOneAsync(
                transaction,
                filter.Eq("_id", visit.Id) & filter.Ne(_permissionField, allowed),
                Builders<BsonDocument>.Update.Set(_permissionField, allowed).Set(_updatedField, nowUtc),
                cancellationToken: token);
            if (changed.MatchedCount != 1)
            {
                return false;
            }

            var payload = VisitStore.Identifiers(visit);
            payload.Add("permiso", allowed);
            await outbox.AddAsync(transaction, OutboxMessage.Create(VisitCloseRules.PhotoPermissionEvent, $"{visit.Id}:{nowUtc.Ticks}", payload, nowUtc), token);
            return true;
        }, false, cancellationToken);
    }
}
