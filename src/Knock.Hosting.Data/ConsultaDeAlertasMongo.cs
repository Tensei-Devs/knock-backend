using Knock.Hosting.Procesos;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Hosting.Data;

/// <summary>
/// Eventos de <c>outbox</c> y tareas de <c>tareas_programadas</c> en alerta (spec 054, R-13). Solo lee <c>_id</c>, <c>tipo</c> e
/// <c>intentos</c>: nunca la carga ni la clave de negocio. Usa el índice <c>{ estado, programadaPara }</c>; un mensaje sin
/// <c>programadaPara</c> (los que escribe Mercado) se mide desde <c>creadoEn</c>.
/// </summary>
public sealed class ConsultaDeAlertasMongo(IMongoDatabase database) : IConsultaDeAlertas
{
    /// <summary>Tope por colección y por motivo en cada evaluación, para no cargar la base ni la telemetría.</summary>
    public const int Limite = 100;

    private const string _status = "estado";
    private const string _due = "programadaPara";

    private static readonly string[] _unfinished = ["pendiente", "en_proceso"];

    public async Task<IReadOnlyList<AlertaDeProceso>> FindAsync(DateTime atrasadasAntesDe, CancellationToken cancellationToken)
    {
        var alerts = new List<AlertaDeProceso>();
        await AddAsync(alerts, ProcesosMetrics.Outbox, ProcesosMetrics.Fallido, atrasadasAntesDe, cancellationToken);
        await AddAsync(alerts, ProcesosMetrics.Tareas, ProcesosMetrics.Fallida, atrasadasAntesDe, cancellationToken);
        return alerts;
    }

    /// <summary>El nombre del proceso es el de su colección.</summary>
    private async Task AddAsync(List<AlertaDeProceso> alerts, string process, string failedStatus, DateTime lateBefore, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<BsonDocument>(process);
        var filter = Builders<BsonDocument>.Filter;
        var failed = filter.Eq(_status, failedStatus);
        var late = filter.In(_status, _unfinished)
            & (filter.Lt(_due, lateBefore) | (filter.Exists(_due, false) & filter.Lt("creadoEn", lateBefore)));
        foreach (var (query, result) in new[] { (failed, failedStatus), (late, ProcesosMetrics.Atrasado) })
        {
            var documents = await collection.Find(query)
                .Sort(Builders<BsonDocument>.Sort.Descending(_due))
                .Limit(Limite)
                .Project(Builders<BsonDocument>.Projection.Include("tipo").Include("intentos"))
                .ToListAsync(cancellationToken);
            alerts.AddRange(documents.Select(document => new AlertaDeProceso(
                process,
                document.GetValue("tipo", string.Empty).ToString()!,
                document["_id"].ToString()!,
                document.GetValue("intentos", 0).ToInt32(),
                result)));
        }
    }
}
