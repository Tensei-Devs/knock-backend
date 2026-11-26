using System.Globalization;
using System.Text;
using Knock.Hosting.Data;
using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Requests.Data;

/// <summary>Una fila de la agenda: la visita con su día y hora locales (spec 050, R-24).</summary>
internal sealed record AgendaRow(ObjectId Id, string Estado, string Modality, string Day, string Time, int? Price);

internal sealed record AgendaPage(IReadOnlyList<AgendaRow> Items, string? NextCursor);

/// <summary>
/// Agenda, reprogramación, cancelación y cliente ausente de la visita (spec 050). Cada cambio es una actualización condicionada por el estado y se escribe junto con su evento
/// del buzón (y la tarea de vencimiento, si corresponde) en la misma transacción; los eventos solo llevan identificadores (R-32). Dos peticiones simultáneas no pueden ganar las
/// dos. Único punto del módulo con acceso a MongoDB para estas operaciones.
/// </summary>
internal sealed class VisitScheduleStore(
    [FromKeyedServices(RequestsModuleExtensions.ModuleKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> mongoOptions,
    IOutbox outbox)
{
    private const string _stateField = "estado";
    private const string _updatedField = "actualizadoEn";
    private const string _proposalField = "propuestaReprogramacion";
    private const char _cursorSeparator = '|';

    private static readonly string[] _openStates = [VisitRules.Confirmed, VisitRules.InProgress];

    private readonly FindOptions _findOptions = new() { MaxTime = mongoOptions.Value.QueryMaxTime };

    private IMongoCollection<BsonDocument> Visits => database.GetCollection<BsonDocument>(ChoiceStore.VisitsCollection);

    /// <summary>
    /// Visitas abiertas del técnico entre dos días (<c>yyyy-MM-dd</c>, ambos incluidos), ordenadas por día, hora e id. El cursor lleva la última fila entregada; uno ilegible
    /// responde 400 <c>invalid_cursor</c>.
    /// </summary>
    public async Task<AgendaPage> ListAgendaAsync(string technicianUserId, string fromDay, string toDay, string? cursor, int limit, CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter;
        var condition = filter.Eq("tecnicoUsuarioId", technicianUserId) & filter.In(_stateField, _openStates) & filter.Gte("dia", fromDay) & filter.Lte("dia", toDay);
        if (cursor is not null)
        {
            var (day, time, id) = DecodeCursor(cursor);
            condition &= filter.Gt("dia", day)
                | (filter.Eq("dia", day) & filter.Gt("hora", time))
                | (filter.Eq("dia", day) & filter.Eq("hora", time) & filter.Gt("_id", id));
        }

        var found = await Visits.Find(condition, _findOptions)
            .Sort(Builders<BsonDocument>.Sort.Ascending("dia").Ascending("hora").Ascending("_id"))
            .Limit(limit + 1)
            .ToListAsync(cancellationToken);
        var rows = found.Take(limit).Select(ToRow).ToList();
        var next = found.Count > limit && rows.Count > 0 ? EncodeCursor(rows[^1]) : null;
        return new AgendaPage(rows, next);
    }

    /// <summary>
    /// Registra la propuesta en una visita <c>confirmada</c> sin otra pendiente y con menos de dos reprogramaciones aceptadas, programa su vencimiento y escribe
    /// <c>ReprogramacionPropuesta</c>. <see langword="false"/> si otra petición o el estado de la visita lo impiden.
    /// </summary>
    public Task<bool> ProposeAsync(VisitRecord visit, string proposedBy, DateTime proposedUtc, DateTime expiresUtc, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(visit);
        return MarketTransactions.RunAsync(database, async (transaction, token) =>
        {
            var filter = Builders<BsonDocument>.Filter;
            var changed = await Visits.UpdateOneAsync(
                transaction,
                filter.Eq("_id", visit.Id) & filter.Eq(_stateField, VisitRules.Confirmed) & filter.Exists(_proposalField, false) & filter.Not(filter.Gte("reprogramaciones", VisitScheduleRules.MaxReschedules)),
                Builders<BsonDocument>.Update
                    .Set(_proposalField, new BsonDocument { { "fechaHora", proposedUtc }, { "propuestaPor", proposedBy }, { "venceEn", expiresUtc } })
                    .Set(_updatedField, nowUtc),
                cancellationToken: token);
            if (changed.MatchedCount != 1)
            {
                return false;
            }

            await outbox.AddAsync(transaction, OutboxMessage.Create("ReprogramacionPropuesta", visit.Id.ToString(), VisitStore.Identifiers(visit), nowUtc), token);
            await new TareasProgramadasStore(database).ScheduleByKeyAsync(transaction, VisitScheduleRules.ProposalExpiryTask, visit.Id.ToString(), expiresUtc, nowUtc, token);
            return true;
        }, false, cancellationToken);
    }

    /// <summary>
    /// Acepta la propuesta vigente que no hizo quien responde: la cita cambia al día y hora de la propuesta, el código de una visita a domicilio vence con el día nuevo y se cuenta una
    /// reprogramación. <see langword="false"/> si ya no hay propuesta, venció, cambió o la ganó otra petición.
    /// </summary>
    public Task<bool> AcceptProposalAsync(VisitRecord visit, string responderRole, string newDay, string newTime, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(visit);
        return MarketTransactions.RunAsync(database, async (transaction, token) =>
        {
            var update = Builders<BsonDocument>.Update
                .Set("dia", newDay)
                .Set("hora", newTime)
                .Inc("reprogramaciones", 1)
                .Unset(_proposalField)
                .Set(_updatedField, nowUtc);
            if (visit.Modality == VisitRules.Domicile)
            {
                update = update.Set("codigoVenceEn", VisitCodeFactory.ExpiresAt(newDay));
            }

            var changed = await Visits.UpdateOneAsync(transaction, Pending(visit, responderRole, nowUtc), update, cancellationToken: token);
            if (changed.MatchedCount != 1)
            {
                return false;
            }

            await outbox.AddAsync(transaction, OutboxMessage.Create("VisitaReprogramada", visit.Id.ToString(), VisitStore.Identifiers(visit), nowUtc), token);
            return true;
        }, false, cancellationToken);
    }

    /// <summary>Rechaza la propuesta vigente que no hizo quien responde: la cita no cambia. <see langword="false"/> si ya no hay propuesta o la ganó otra petición.</summary>
    public Task<bool> RejectProposalAsync(VisitRecord visit, string responderRole, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(visit);
        return MarketTransactions.RunAsync(database, async (transaction, token) =>
        {
            var changed = await Visits.UpdateOneAsync(
                transaction,
                Pending(visit, responderRole, nowUtc),
                Builders<BsonDocument>.Update.Unset(_proposalField).Set(_updatedField, nowUtc),
                cancellationToken: token);
            if (changed.MatchedCount != 1)
            {
                return false;
            }

            await outbox.AddAsync(transaction, OutboxMessage.Create("ReprogramacionRechazada", visit.Id.ToString(), VisitStore.Identifiers(visit), nowUtc), token);
            return true;
        }, false, cancellationToken);
    }

    /// <summary>Vence la propuesta sin respuesta: la cita se mantiene (R-25). Idempotente; <see langword="false"/> si ya no estaba pendiente o aún no vence.</summary>
    public async Task<bool> ExpireProposalAsync(ObjectId visitId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var found = await Visits.Find(Builders<BsonDocument>.Filter.Eq("_id", visitId), _findOptions).Limit(1).ToListAsync(cancellationToken);
        if (found.Count == 0)
        {
            return false;
        }

        var visit = VisitStore.ToRecord(found[0]);
        return await MarketTransactions.RunAsync(database, async (transaction, token) =>
        {
            var filter = Builders<BsonDocument>.Filter;
            var changed = await Visits.UpdateOneAsync(
                transaction,
                filter.Eq("_id", visitId) & filter.Lte($"{_proposalField}.venceEn", nowUtc),
                Builders<BsonDocument>.Update.Unset(_proposalField).Set(_updatedField, nowUtc),
                cancellationToken: token);
            if (changed.MatchedCount != 1)
            {
                return false;
            }

            await outbox.AddAsync(transaction, OutboxMessage.Create("ReprogramacionVencida", visitId.ToString(), VisitStore.Identifiers(visit), nowUtc), token);
            return true;
        }, false, cancellationToken);
    }

    /// <summary>
    /// Cancela la visita <c>confirmada</c> con su motivo y quién cancela, y escribe <c>VisitaCerrada</c> (y <c>CancelacionTardia</c> si faltaban menos de 24 horas, para avisar de
    /// inmediato a la otra parte) en la misma transacción. <see langword="false"/> si la visita ya no estaba <c>confirmada</c>.
    /// </summary>
    public Task<bool> CancelAsync(VisitRecord visit, string cancelledBy, string reason, bool late, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(visit);
        return MarketTransactions.RunAsync(database, async (transaction, token) =>
        {
            var changed = await Visits.UpdateOneAsync(
                transaction,
                Builders<BsonDocument>.Filter.Eq("_id", visit.Id) & Builders<BsonDocument>.Filter.Eq(_stateField, VisitRules.Confirmed),
                Builders<BsonDocument>.Update
                    .Set(_stateField, VisitScheduleRules.Cancelled)
                    .Set("tardia", late)
                    .Set("cancelacion", new BsonDocument { { "motivo", reason }, { "por", cancelledBy }, { "tardia", late }, { "en", nowUtc } })
                    .Unset(_proposalField)
                    .Set(_updatedField, nowUtc),
                cancellationToken: token);
            if (changed.MatchedCount != 1)
            {
                return false;
            }

            await SolicitudDeLaVisita.CancelAsync(database, transaction, visit.RequestId, TransicionesDeSolicitud.VisitCancelledReason, nowUtc, token);
            await outbox.AddAsync(transaction, ClosedEvent(visit, VisitScheduleRules.Cancelled, cancelledBy, nowUtc), token);
            if (late)
            {
                await outbox.AddAsync(transaction, OutboxMessage.Create("CancelacionTardia", visit.Id.ToString(), VisitStore.Identifiers(visit), nowUtc), token);
            }

            return true;
        }, false, cancellationToken);
    }

    /// <summary>
    /// Marca la visita <c>confirmada</c> como <c>no_presentado</c> con <c>quienFalto = cliente</c>, sin penalización, y escribe <c>VisitaCerrada</c> (el cliente recibe el aviso)
    /// en la misma transacción. <see langword="false"/> si la visita ya no estaba <c>confirmada</c>.
    /// </summary>
    public Task<bool> MarkClientAbsentAsync(VisitRecord visit, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(visit);
        return MarketTransactions.RunAsync(database, async (transaction, token) =>
        {
            var changed = await Visits.UpdateOneAsync(
                transaction,
                Builders<BsonDocument>.Filter.Eq("_id", visit.Id) & Builders<BsonDocument>.Filter.Eq(_stateField, VisitRules.Confirmed),
                Builders<BsonDocument>.Update
                    .Set(_stateField, VisitScheduleRules.NoShow)
                    .Set("quienFalto", VisitScheduleRules.ClientAbsent)
                    .Unset(_proposalField)
                    .Set(_updatedField, nowUtc),
                cancellationToken: token);
            if (changed.MatchedCount != 1)
            {
                return false;
            }

            await SolicitudDeLaVisita.CancelAsync(database, transaction, visit.RequestId, TransicionesDeSolicitud.VisitNoShowReason, nowUtc, token);
            await outbox.AddAsync(transaction, ClosedEvent(visit, VisitScheduleRules.NoShow, null, nowUtc), token);
            return true;
        }, false, cancellationToken);
    }

    /// <summary>
    /// El cliente dueño marca la visita <c>confirmada</c> como <c>no_presentado</c> con <c>quienFalto = tecnico</c> (spec 064, R-03), sin penalización, y escribe <c>TecnicoNoSePresento</c>
    /// (solo identificadores) en la misma transacción. <see langword="false"/> si la visita ya no estaba <c>confirmada</c> o no es de ese cliente.
    /// </summary>
    public Task<bool> MarkTechnicianAbsentAsync(VisitRecord visit, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(visit);
        return MarketTransactions.RunAsync(database, async (transaction, token) =>
        {
            var filter = Builders<BsonDocument>.Filter;
            var changed = await Visits.UpdateOneAsync(
                transaction,
                filter.Eq("_id", visit.Id) & filter.Eq("clienteId", visit.ClientId) & filter.Eq(_stateField, VisitRules.Confirmed),
                Builders<BsonDocument>.Update
                    .Set(_stateField, VisitScheduleRules.NoShow)
                    .Set("quienFalto", VisitScheduleRules.TechnicianAbsent)
                    .Unset(_proposalField)
                    .Set(_updatedField, nowUtc),
                cancellationToken: token);
            if (changed.MatchedCount != 1)
            {
                return false;
            }

            await SolicitudDeLaVisita.CancelAsync(database, transaction, visit.RequestId, TransicionesDeSolicitud.VisitNoShowReason, nowUtc, token);
            await outbox.AddAsync(transaction, OutboxMessage.Create("TecnicoNoSePresento", visit.Id.ToString(), VisitStore.Identifiers(visit), nowUtc), token);
            return true;
        }, false, cancellationToken);
    }

    private static FilterDefinition<BsonDocument> Pending(VisitRecord visit, string responderRole, DateTime nowUtc)
    {
        var filter = Builders<BsonDocument>.Filter;
        var proposal = visit.Proposal;
        return filter.Eq("_id", visit.Id)
            & filter.Eq(_stateField, VisitRules.Confirmed)
            & filter.Ne($"{_proposalField}.propuestaPor", responderRole)
            & filter.Eq($"{_proposalField}.fechaHora", proposal?.ProposedAtUtc ?? DateTime.MinValue)
            & filter.Gt($"{_proposalField}.venceEn", nowUtc);
    }

    private static OutboxMessage ClosedEvent(VisitRecord visit, string reason, string? by, DateTime nowUtc)
    {
        var payload = new BsonDocument { { "visitaId", visit.Id.ToString() }, { "motivo", reason } };
        if (by is not null)
        {
            payload.Add("canceladaPor", by);
        }
        else
        {
            payload.Add("quienFalto", VisitScheduleRules.ClientAbsent);
        }

        return OutboxMessage.Create("VisitaCerrada", visit.Id.ToString(), payload, nowUtc);
    }

    private static AgendaRow ToRow(BsonDocument d) => new(
        d["_id"].AsObjectId,
        d[_stateField].AsString,
        d["modalidad"].AsString,
        d["dia"].AsString,
        d["hora"].AsString,
        d.TryGetValue("precio", out var price) && price.IsNumeric ? price.ToInt32() : null);

    private static string EncodeCursor(AgendaRow row) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{row.Day}{_cursorSeparator}{row.Time}{_cursorSeparator}{row.Id}")));

    private static (string Day, string Time, ObjectId Id) DecodeCursor(string cursor)
    {
        try
        {
            var parts = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(cursor)).Split(_cursorSeparator);
            if (parts.Length == 3 && VisitScheduleRules.TryParseDay(parts[0], out _) && TimeOnly.TryParseExact(parts[1], VisitScheduleRules.TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _) && ObjectId.TryParse(parts[2], out var id))
            {
                return (parts[0], parts[1], id);
            }
        }
        catch (FormatException)
        {
            // Se responde igual que con cualquier otro cursor ilegible.
        }

        throw new DomainException(DomainErrorKind.BadRequest, "The cursor is not valid.", RequestErrorCodes.InvalidCursorCode);
    }
}
