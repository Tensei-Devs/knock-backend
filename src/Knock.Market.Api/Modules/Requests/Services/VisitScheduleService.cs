using Knock.Hosting.Concurrency;
using Knock.Market.Api.Common.TareasProgramadas;
using Knock.Market.Api.Modules.Requests.Data;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;
using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Requests.Services;

/// <summary>Por qué una operación de la spec 050 no se aplicó, o <see cref="Done"/>. Cada caso es una respuesta fija del controller.</summary>
internal enum VisitScheduleStatus
{
    Done,
    NotATechnician,
    NotFound,
    InvalidProposal,
    NotReschedulable,
    NoPendingProposal,
    AlreadyClosed,
    OutsideArrivalWindow,
    NotInProgress,
    ReportWindowClosed,
}

internal sealed record VisitScheduleOutcome<T>(VisitScheduleStatus Status, T? Value = default)
    where T : class;

/// <summary>
/// Agenda del técnico y cambios de la visita (spec 050, VIS-02 y VIS-04 a VIS-07): resuelve el papel de la persona por el <c>sub</c> del token (R-31), aplica las reglas de plazo y
/// de estado con el reloj del servidor y delega las escrituras atómicas en <see cref="VisitScheduleStore"/>. No toca MongoDB y no registra motivos ni fechas de las personas (R-32).
/// </summary>
internal sealed class VisitScheduleService(VisitScheduleStore store, VisitStore visits, ITechnicianDirectory technicians, TimeProvider clock)
{
    /// <summary>VIS-02: las visitas abiertas del técnico entre dos días (por omisión hoy y los 14 días siguientes), por fecha y hora.</summary>
    public async Task<VisitScheduleOutcome<AgendaResponse>> AgendaAsync(string sub, AgendaQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!await technicians.ExistsAsync(sub, cancellationToken))
        {
            return new(VisitScheduleStatus.NotATechnician);
        }

        var today = VisitScheduleRules.LocalToday(clock.GetUtcNow().UtcDateTime);
        var from = VisitScheduleRules.TryParseDay(query.Desde, out var parsedFrom) ? parsedFrom : today;
        var to = VisitScheduleRules.TryParseDay(query.Hasta, out var parsedTo) ? parsedTo : from.AddDays(VisitScheduleRules.DefaultAgendaDays);
        if (!VisitScheduleRules.IsValidAgendaRange(from, to))
        {
            return new(VisitScheduleStatus.InvalidProposal);
        }

        var page = await store.ListAgendaAsync(sub, Format(from), Format(to), query.Cursor, query.Limite, cancellationToken);
        var items = page.Items.Select(row => new AgendaItem(
            row.Id.ToString(),
            row.Estado,
            row.Modality,
            new DateTimeOffset(VisitScheduleRules.AppointmentUtc(row.Day, row.Time), TimeSpan.Zero),
            row.Day,
            row.Time,
            row.Price)).ToList();
        return new(VisitScheduleStatus.Done, new AgendaResponse(items, page.NextCursor));
    }

    /// <summary>VIS-04: una parte propone otra fecha. Solo con la visita <c>confirmada</c>, sin propuesta pendiente y con menos de dos reprogramaciones aceptadas.</summary>
    public async Task<VisitScheduleOutcome<RescheduleProposalResponse>> ProposeAsync(string sub, string id, DateTimeOffset proposed, CancellationToken cancellationToken)
    {
        var (visit, role) = await ResolveAsync(sub, id, cancellationToken);
        if (visit is null)
        {
            return new(VisitScheduleStatus.NotFound);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var proposedUtc = proposed.UtcDateTime;
        if (!VisitScheduleRules.IsValidProposal(proposedUtc, now))
        {
            return new(VisitScheduleStatus.InvalidProposal);
        }

        var appointment = VisitScheduleRules.AppointmentUtc(visit.Day, visit.Time);
        var expires = VisitScheduleRules.ProposalExpiresAt(now, appointment);
        if (visit.Estado != VisitRules.Confirmed || visit.Proposal is not null || visit.Reschedules >= VisitScheduleRules.MaxReschedules || expires is not { } expiresAt)
        {
            return new(VisitScheduleStatus.NotReschedulable);
        }

        var by = ProposerName(role);
        return await store.ProposeAsync(visit, by, proposedUtc, expiresAt, now, cancellationToken)
            ? new(VisitScheduleStatus.Done, new RescheduleProposalResponse(visit.Id.ToString(), new DateTimeOffset(proposedUtc, TimeSpan.Zero), by, new DateTimeOffset(expiresAt, TimeSpan.Zero)))
            : new(VisitScheduleStatus.NotReschedulable);
    }

    /// <summary>VIS-05: la otra parte (no quien propuso) acepta o rechaza la propuesta vigente.</summary>
    public async Task<VisitScheduleOutcome<RescheduleReplyResponse>> ReplyAsync(string sub, string id, bool accept, CancellationToken cancellationToken)
    {
        var (visit, role) = await ResolveAsync(sub, id, cancellationToken);
        if (visit is null)
        {
            return new(VisitScheduleStatus.NotFound);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var responder = ProposerName(role);
        if (visit.Estado != VisitRules.Confirmed || visit.Proposal is not { } proposal || proposal.ExpiresAtUtc <= now || proposal.ProposedBy == responder)
        {
            return new(VisitScheduleStatus.NoPendingProposal);
        }

        if (accept)
        {
            var (day, time) = VisitScheduleRules.ToLocal(proposal.ProposedAtUtc);
            return await store.AcceptProposalAsync(visit, responder, day, time, now, cancellationToken)
                ? new(VisitScheduleStatus.Done, new RescheduleReplyResponse(visit.Id.ToString(), true, new DateTimeOffset(proposal.ProposedAtUtc, TimeSpan.Zero), visit.Reschedules + 1))
                : new(VisitScheduleStatus.NoPendingProposal);
        }

        var current = new DateTimeOffset(VisitScheduleRules.AppointmentUtc(visit.Day, visit.Time), TimeSpan.Zero);
        return await store.RejectProposalAsync(visit, responder, now, cancellationToken)
            ? new(VisitScheduleStatus.Done, new RescheduleReplyResponse(visit.Id.ToString(), false, current, visit.Reschedules))
            : new(VisitScheduleStatus.NoPendingProposal);
    }

    /// <summary>VIS-06: cualquiera de las dos partes cancela con motivo. Con menos de 24 horas queda <c>tardia</c>. Solo una visita <c>confirmada</c> se cancela.</summary>
    public async Task<VisitScheduleOutcome<CancelVisitResponse>> CancelAsync(string sub, string id, string reason, CancellationToken cancellationToken)
    {
        var (visit, role) = await ResolveAsync(sub, id, cancellationToken);
        if (visit is null)
        {
            return new(VisitScheduleStatus.NotFound);
        }

        if (visit.Estado != VisitRules.Confirmed)
        {
            return new(VisitScheduleStatus.AlreadyClosed);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var late = VisitScheduleRules.IsLateCancellation(now, VisitScheduleRules.AppointmentUtc(visit.Day, visit.Time));
        return await store.CancelAsync(visit, ProposerName(role), reason.Trim(), late, now, cancellationToken)
            ? new(VisitScheduleStatus.Done, new CancelVisitResponse(visit.Id.ToString(), VisitScheduleRules.Cancelled, late))
            : new(VisitScheduleStatus.AlreadyClosed);
    }

    /// <summary>VIS-07: el técnico de la visita registra que el cliente no estaba, desde 15 minutos después de la cita y hasta 2 horas después.</summary>
    public async Task<VisitScheduleOutcome<ClientAbsentResponse>> ClientAbsentAsync(string sub, string id, CancellationToken cancellationToken)
    {
        if (!await technicians.ExistsAsync(sub, cancellationToken))
        {
            return new(VisitScheduleStatus.NotATechnician);
        }

        var (visit, role) = await ResolveAsync(sub, id, cancellationToken);
        if (visit is null || role != VisitRole.Technician)
        {
            return new(VisitScheduleStatus.NotFound);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (visit.Estado != VisitRules.Confirmed || !VisitScheduleRules.IsInAbsenceWindow(now, VisitScheduleRules.AppointmentUtc(visit.Day, visit.Time)))
        {
            return new(VisitScheduleStatus.OutsideArrivalWindow);
        }

        return await store.MarkClientAbsentAsync(visit, now, cancellationToken)
            ? new(VisitScheduleStatus.Done, new ClientAbsentResponse(visit.Id.ToString(), VisitScheduleRules.NoShow, VisitScheduleRules.ClientAbsent))
            : new(VisitScheduleStatus.OutsideArrivalWindow);
    }

    /// <summary>
    /// VIS-12 (spec 064): el cliente dueño reporta que el técnico no llegó, desde 15 minutos después de la cita y hasta 2 horas después. Repetirlo con la visita ya marcada así responde
    /// igual sin otro evento. Las llamadas de la misma visita se turnan (<see cref="KeyedGate"/>) y la visita se relee dentro del turno.
    /// </summary>
    public async Task<VisitScheduleOutcome<TechnicianAbsentResponse>> TechnicianAbsentAsync(string sub, string id, CancellationToken cancellationToken)
    {
        var (visit, role) = await ResolveAsync(sub, id, cancellationToken);
        if (visit is null || role != VisitRole.Client)
        {
            return new(VisitScheduleStatus.NotFound);
        }

        return await KeyedGate.RunAsync($"tecnico-ausente:{visit.Id}", () => TechnicianAbsentSerializedAsync(visit.Id, sub, cancellationToken), cancellationToken);
    }

    private async Task<VisitScheduleOutcome<TechnicianAbsentResponse>> TechnicianAbsentSerializedAsync(ObjectId visitId, string sub, CancellationToken cancellationToken)
    {
        var visit = await visits.FindAsync(visitId, cancellationToken);
        if (visit is null || VisitAccess.RoleOf(sub, visit.ClientId, visit.TechnicianUserId) != VisitRole.Client)
        {
            return new(VisitScheduleStatus.NotFound);
        }

        var done = new VisitScheduleOutcome<TechnicianAbsentResponse>(VisitScheduleStatus.Done, new TechnicianAbsentResponse(visit.Id.ToString(), VisitScheduleRules.NoShow, VisitScheduleRules.TechnicianAbsent));
        if (VisitScheduleRules.IsTechnicianAbsentRecorded(visit.Estado, visit.WhoMissed))
        {
            return done;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (!VisitScheduleRules.CanReportTechnicianAbsent(visit.Estado, now, VisitScheduleRules.AppointmentUtc(visit.Day, visit.Time)))
        {
            return new(VisitScheduleStatus.OutsideArrivalWindow);
        }

        return await store.MarkTechnicianAbsentAsync(visit, now, cancellationToken) ? done : new(VisitScheduleStatus.OutsideArrivalWindow);
    }

    private async Task<(VisitRecord? Visit, VisitRole Role)> ResolveAsync(string sub, string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var visitId))
        {
            throw new Common.Errors.DomainException(Common.Errors.DomainErrorKind.BadRequest, "The visit id is not valid.");
        }

        var visit = await visits.FindAsync(visitId, cancellationToken);
        var role = visit is null ? VisitRole.Stranger : VisitAccess.RoleOf(sub, visit.ClientId, visit.TechnicianUserId);
        return role == VisitRole.Stranger ? (null, role) : (visit, role);
    }

    private static string ProposerName(VisitRole role) => role == VisitRole.Client ? VisitScheduleRules.ProposedByClient : VisitScheduleRules.ProposedByTechnician;

    private static string Format(DateOnly day) => day.ToString(VisitScheduleRules.DayFormat, System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// Tarea <c>VencerPropuestaDeReprogramacion</c> (spec 054): la propuesta sin respuesta vence y la cita se mantiene. La clave es <c>{visitaId}:{propuesta}</c> (cada propuesta programa la suya);
/// las tareas anteriores traen solo la visita.
/// </summary>
internal sealed class VencerPropuestaDeReprogramacion(VisitScheduleStore store, TimeProvider clock) : IManejadorDeTarea
{
    public string Tipo => VisitScheduleRules.ProposalExpiryTask;

    public async Task HandleAsync(TareaReservada task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (!ObjectId.TryParse(task.ClaveNegocio.Split(':')[0], out var id))
        {
            throw new FormatException("Invalid expiry task key.");
        }

        await store.ExpireProposalAsync(id, clock.GetUtcNow().UtcDateTime, cancellationToken);
    }
}
