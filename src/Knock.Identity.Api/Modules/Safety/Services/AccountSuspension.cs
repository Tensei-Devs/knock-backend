using System.Globalization;
using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Hosting.Procesos.Outbox;
using Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;
using Knock.Identity.Api.Modules.Panel;
using Knock.Identity.Api.Modules.Safety.Data;
using Knock.Identity.Api.Modules.Safety.Domain;
using Knock.Identity.Api.Modules.Sesiones;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace Knock.Identity.Api.Modules.Safety.Services;

/// <summary>Cómo terminó <c>Suspender</c>. Solo <see cref="Suspended"/> cambió algo.</summary>
internal enum SuspensionOutcome
{
    Suspended,
    AlreadySuspended,
    NotTechnician,
}

/// <summary>Resultado de suspender. <see cref="SessionIds"/> son los <c>sid</c> de la app revocados: tras confirmar la transacción hay que cancelarlos en Redis (R-08).</summary>
internal sealed record SuspensionResult(SuspensionOutcome Outcome, IReadOnlyList<string> SessionIds);

/// <summary>
/// Suspensión preventiva (spec 059): <c>Suspender</c> y <c>Reactivar</c> son operaciones internas del módulo, no rutas (R-01, R-20). La transacción (R-04) cambia el estado con una operación
/// condicionada, revoca las sesiones de la app y del panel, deja <c>CuentaSuspendida</c> y un <c>sesion_revocada</c> por sesión en la bandeja y escribe la auditoría; si algo falla no queda nada.
/// La cancelación en Redis va después del commit con reintento y sin revertir (R-08). SEGURIDAD: eventos y auditoría llevan solo identificadores (R-18).
/// </summary>
internal sealed class AccountSuspension(ISuspensionStore store, ISesionesApi accounts, IPanelApi panel, TimeProvider clock, ILogger<AccountSuspension> logger, IOptions<SuspensionOptions>? options = null)
{
    /// <summary>Quien levanta una suspensión gradual al vencer (spec 060, R-06a): no es una persona; la auditoría guarda este valor.</summary>
    public const string SystemActor = "sistema";

    public const string AuditSuspended = "cuenta_suspendida";
    public const string AuditReactivated = "cuenta_reactivada";
    public const string EventSuspended = "CuentaSuspendida";
    public const string EventReactivated = "CuentaReactivada";
    public const string EventSessionRevoked = "sesion_revocada";

    private const int _cancelAttempts = 3;

    private static readonly TimeSpan _cancelBackoff = TimeSpan.FromMilliseconds(250);

    private static readonly Action<ILogger, int, Exception?> _logCancelFailed =
        LoggerMessage.Define<int>(LogLevel.Error, new EventId(5901, "SuspendedSessionCancelFailed"), "Redis cancellation of {Count} suspended sessions failed; renewal and sign-in stay closed by the database.");

    /// <summary>Suspende en su propia transacción y luego cancela las sesiones en Redis. Rechaza la propia cuenta con 403 <c>cannot_suspend_self</c> y una inexistente con 404.</summary>
    public async Task<SuspensionResult> SuspenderAsync(string subject, string reportId, string moderatorSub, string type, CancellationToken cancellationToken)
    {
        var result = await store.RunAsync(transaction => ApplySuspensionAsync(transaction, subject, reportId, moderatorSub, type, cancellationToken), cancellationToken);
        await CancelSessionsAsync(result.SessionIds, cancellationToken);
        return result;
    }

    /// <summary>
    /// Lo mismo dentro de la transacción de quien llama (la decisión de REP-05, spec 053). Quien llama debe invocar <see cref="CancelSessionsAsync"/> con <see cref="SuspensionResult.SessionIds"/>
    /// después de confirmar.
    /// </summary>
    public async Task<SuspensionResult> ApplySuspensionAsync(StoreTransaction transaction, string subject, string reportId, string moderatorSub, string type, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(moderatorSub);
        RequireReportId(reportId);
        var account = await accounts.ReadAccountForSuspensionAsync(transaction, subject, cancellationToken)
            ?? throw new DomainException(DomainErrorKind.NotFound, "The person does not exist.", SafetyErrorCodes.UserNotFoundCode);
        switch (AccountSuspensionRules.Suspender(account.State, account.IsTechnician, subject, moderatorSub))
        {
            case SuspendDecision.SelfSuspension:
                throw new DomainException(DomainErrorKind.Forbidden, "A moderator cannot suspend their own account.", SafetyErrorCodes.CannotSuspendSelfCode);
            case SuspendDecision.AlreadySuspended:
                return new SuspensionResult(SuspensionOutcome.AlreadySuspended, []);
            case SuspendDecision.NotTechnician:
                return new SuspensionResult(SuspensionOutcome.NotTechnician, []);
        }

        var now = Millisecond(clock.GetUtcNow().UtcDateTime);
        var expiresAt = AccountSuspensionRules.ExpiresAt(type, now, options?.Value.DuracionGradualDias ?? SuspensionOptions.DefaultGradualDays);
        var sessionIds = await accounts.SuspendAccountAsync(transaction, subject, account.State, type, reportId, now, expiresAt, cancellationToken);
        if (sessionIds is null)
        {
            // Otra decisión la suspendió entre la lectura y la escritura: un solo cambio y un solo evento (R-05).
            return new SuspensionResult(SuspensionOutcome.AlreadySuspended, []);
        }

        await panel.RevokeSessionsInTransactionAsync(transaction, subject, cancellationToken);
        if (expiresAt is { } due)
        {
            // La suspensión gradual vence sola (spec 060, R-06a): la tarea lleva solo identificadores y se confirma con la suspensión.
            await store.ScheduleTaskAsync(transaction, CatalogoDeTareas.VencerSuspension, ExpiryKey(subject, reportId), due, now, cancellationToken);
        }

        var suspendedId = OutboxReglas.BuildId(EventSuspended, reportId, 1);
        var events = new List<EventoDeOutbox>
        {
            new(suspendedId, EventSuspended, "market", new BsonDocument { ["eventoId"] = suspendedId, ["usuarioId"] = subject, ["reporteId"] = reportId, ["desde"] = Iso(now) }),
        };
        events.AddRange(sessionIds.Select(sid =>
        {
            var revokedId = OutboxReglas.BuildId(EventSessionRevoked, sid, 1);
            return new EventoDeOutbox(revokedId, EventSessionRevoked, "messaging", new BsonDocument { ["eventoId"] = revokedId, ["sid"] = sid, ["usuarioId"] = subject });
        }));
        await store.AddEventsAsync(transaction, events, now, cancellationToken);
        await panel.RecordActionInTransactionAsync(transaction, AuditSuspended, PanelApiAuditValues.ResultAllowed, moderatorSub, reportId, cancellationToken);
        return new SuspensionResult(SuspensionOutcome.Suspended, sessionIds);
    }

    /// <summary>Clave de negocio de la tarea de vencimiento: <c>{sub}:{reporteId}</c>.</summary>
    public static string ExpiryKey(string subject, string reportId) => $"{subject}:{reportId}";

    /// <summary>Reactiva en su propia transacción (R-15). Rechaza una cuenta que no está suspendida con 409 <c>account_not_suspended</c>. No restaura sesiones.</summary>
    public Task ReactivarAsync(string subject, string reportId, string moderatorSub, CancellationToken cancellationToken) =>
        store.RunAsync(
            async transaction =>
            {
                await ApplyReactivationAsync(transaction, subject, reportId, moderatorSub, cancellationToken);
                return true;
            },
            cancellationToken);

    /// <summary>Reactivar dentro de la transacción de quien llama (la resolución de la apelación).</summary>
    public async Task ApplyReactivationAsync(StoreTransaction transaction, string subject, string reportId, string moderatorSub, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(moderatorSub);
        RequireReportId(reportId);
        var account = await accounts.ReadAccountForSuspensionAsync(transaction, subject, cancellationToken)
            ?? throw new DomainException(DomainErrorKind.NotFound, "The person does not exist.", SafetyErrorCodes.UserNotFoundCode);
        if (AccountSuspensionRules.Reactivar(account.State) == ReactivateDecision.NotSuspended || !await accounts.ReactivateAccountAsync(transaction, subject, cancellationToken))
        {
            throw new DomainException(DomainErrorKind.Conflict, "The account is not suspended.", SafetyErrorCodes.AccountNotSuspendedCode);
        }

        var now = Millisecond(clock.GetUtcNow().UtcDateTime);
        var eventId = OutboxReglas.BuildId(EventReactivated, reportId, 1);
        await store.AddEventsAsync(
            transaction,
            [new EventoDeOutbox(eventId, EventReactivated, "market", new BsonDocument { ["eventoId"] = eventId, ["usuarioId"] = subject, ["desde"] = Iso(now) })],
            now,
            cancellationToken);
        await panel.RecordActionInTransactionAsync(transaction, AuditReactivated, PanelApiAuditValues.ResultAllowed, moderatorSub, reportId, cancellationToken);
    }

    /// <summary>
    /// Escribe <c>sesion-cancelada:{sid}</c> con reintento (R-08). Si Redis sigue fallando NO revierte la suspensión: registra una alerta sin datos personales; el JWT ya emitido sirve a lo
    /// sumo 15 minutos y la renovación y el inicio de sesión siguen cerrados por MongoDB. Devuelve si todas quedaron canceladas.
    /// </summary>
    public async Task<bool> CancelSessionsAsync(IReadOnlyList<string> sessionIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionIds);
        if (sessionIds.Count == 0)
        {
            return true;
        }

        for (var attempt = 1; attempt <= _cancelAttempts; attempt++)
        {
            try
            {
                await accounts.CancelSessionsAsync(sessionIds, cancellationToken);
                return true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (attempt == _cancelAttempts)
                {
                    _logCancelFailed(logger, sessionIds.Count, exception);
                    return false;
                }

                await Task.Delay(_cancelBackoff * attempt, clock, cancellationToken);
            }
        }

        return false;
    }

    private static void RequireReportId(string reportId)
    {
        if (!ObjectId.TryParse(reportId, out _))
        {
            throw new ArgumentException("The report identifier is not valid.", nameof(reportId));
        }
    }

    private static DateTime Millisecond(DateTime value) => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMillisecond));

    private static string Iso(DateTime value) => value.ToString("O", CultureInfo.InvariantCulture);
}
