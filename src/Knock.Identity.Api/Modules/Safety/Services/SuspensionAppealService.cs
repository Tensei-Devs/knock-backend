using Knock.Hosting.Concurrency;
using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Account.Services;
using Knock.Identity.Api.Modules.Safety.Data;
using Knock.Identity.Api.Modules.Safety.Domain;
using Knock.Identity.Api.Modules.Safety.Dtos;
using Knock.Identity.Api.Modules.Sesiones;
using Knock.Identity.Api.Modules.Sesiones.Services;
using MongoDB.Bson;

namespace Knock.Identity.Api.Modules.Safety.Services;

/// <summary>Resultado de una petición pública de apelación: el token no sirve (401), demasiados intentos con ese número (429) o la apelación.</summary>
internal abstract record SuspensionAppealOutcome
{
    private SuspensionAppealOutcome()
    {
    }

    public sealed record Unauthorized : SuspensionAppealOutcome;

    public sealed record RateLimited(TimeSpan RetryAfter) : SuspensionAppealOutcome;

    public sealed record Found(SuspensionAppealView Appeal) : SuspensionAppealOutcome;
}

/// <summary>
/// Apelación de una persona suspendida (spec 060). La persona prueba que controla el número con el <c>idToken</c> de Firebase de un inicio de sesión por SMS reciente (el verificador de la spec 015; no abre
/// sesión). Sin cuenta con ese número, con la cuenta no suspendida o sin una apelación posible la respuesta es siempre la misma 404 (R-02). Ni el token, ni el número, ni el texto llegan a los registros (R-08).
/// </summary>
internal sealed partial class SuspensionAppealService(
    IFirebaseIdTokenVerifier verifier,
    ISesionesApi accounts,
    PhoneProtector phones,
    SuspensionAppealStore store,
    SuspensionAppealLimiter limiter,
    TimeProvider clock,
    ILogger<SuspensionAppealService> logger)
{
    /// <summary>Presenta la apelación de la suspensión vigente. 409 <c>appeal_not_allowed</c> si no se permite otra (R-04, R-04a).</summary>
    public async Task<SuspensionAppealOutcome> AppealAsync(string idToken, string text, CancellationToken cancellationToken)
    {
        var (refused, suspension) = await EnterAsync(idToken, cancellationToken);
        if (refused is not null)
        {
            return refused;
        }

        var current = suspension!;
        var trimmed = text.Trim();
        return await KeyedGate.RunAsync(current.ReportId, () => AppealSerializedAsync(current, trimmed, cancellationToken), cancellationToken);
    }

    /// <summary>El estado de la apelación de la suspensión vigente (R-09). 404 uniforme si no hay una.</summary>
    public async Task<SuspensionAppealOutcome> GetAsync(string idToken, CancellationToken cancellationToken)
    {
        var (refused, suspension) = await EnterAsync(idToken, cancellationToken);
        if (refused is not null)
        {
            return refused;
        }

        var report = await store.FindAsync(ObjectId.Parse(suspension!.ReportId), cancellationToken);
        if (report is not { Appeal: { Origin: AppealOrigins.Suspension } appeal } || report.ReportedId != suspension.Sub)
        {
            throw NotFound();
        }

        return new SuspensionAppealOutcome.Found(View(appeal));
    }

    private async Task<SuspensionAppealOutcome> AppealSerializedAsync(AppSuspension suspension, string text, CancellationToken cancellationToken)
    {
        var id = ObjectId.Parse(suspension.ReportId);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var report = await store.FindAsync(id, cancellationToken);
            if (report is null || report.ReportedId != suspension.Sub)
            {
                throw NotFound();
            }

            switch (SuspensionAppealRules.Evaluate(report, text))
            {
                case AppealDecision.NotFound:
                    throw NotFound();
                case AppealDecision.NotAllowed:
                    LogNotAllowed(logger);
                    throw new DomainException(DomainErrorKind.Conflict, "The suspension cannot be appealed.", SafetyErrorCodes.AppealNotAllowedCode);
                case AppealDecision.Repeat:
                    return new SuspensionAppealOutcome.Found(View(report.Appeal!));
                default:
                    var now = Millisecond(clock.GetUtcNow().UtcDateTime);
                    if (await store.OpenAsync(id, suspension.Sub, text, now, cancellationToken))
                    {
                        LogOpened(logger);
                        return new SuspensionAppealOutcome.Found(new SuspensionAppealView(AppealStates.Pending, now, null));
                    }

                    // Otra instancia ganó la carrera entre la lectura y la escritura: se vuelve a leer y se decide otra vez.
                    break;
            }
        }

        throw new DomainException(DomainErrorKind.Conflict, "The suspension cannot be appealed.", SafetyErrorCodes.AppealNotAllowedCode);
    }

    // Verifica el token (R-01), cuenta el intento por número (R-07) y busca la suspensión (R-02).
    private async Task<(SuspensionAppealOutcome? Refused, AppSuspension? Suspension)> EnterAsync(string idToken, CancellationToken cancellationToken)
    {
        var identity = await verifier.VerifyAsync(idToken, cancellationToken);
        if (identity is null || !identity.IsRecent(clock.GetUtcNow(), FirebaseIdentity.RecentSignInWindow))
        {
            LogRejected(logger);
            return (new SuspensionAppealOutcome.Unauthorized(), null);
        }

        if (!limiter.TryAcquire("suspension:" + Convert.ToHexStringLower(phones.Hash(identity.PhoneNumber).Value), out var retryAfter))
        {
            LogLimited(logger);
            return (new SuspensionAppealOutcome.RateLimited(retryAfter), null);
        }

        var suspension = await accounts.FindSuspensionByPhoneAsync(identity.PhoneNumber, cancellationToken) ?? throw NotFound();
        return (null, suspension);
    }

    private static SuspensionAppealView View(StoredAppeal appeal) => new(appeal.State, appeal.CreatedAtUtc, appeal.ResolvedAtUtc);

    private static DomainException NotFound() => new(DomainErrorKind.NotFound, "Not found.", CommonErrorCodes.NotFound);

    private static DateTime Millisecond(DateTime value) => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMillisecond));

    // Los registros no llevan token, número, texto, sub ni identificadores del reporte.
    [LoggerMessage(EventId = 6001, Level = LogLevel.Warning, Message = "Suspension appeal refused: the Firebase token was rejected or is not recent")]
    private static partial void LogRejected(ILogger logger);

    [LoggerMessage(EventId = 6002, Level = LogLevel.Warning, Message = "Suspension appeal refused: too many attempts for the same number")]
    private static partial void LogLimited(ILogger logger);

    [LoggerMessage(EventId = 6003, Level = LogLevel.Information, Message = "Suspension appeal opened")]
    private static partial void LogOpened(ILogger logger);

    [LoggerMessage(EventId = 6004, Level = LogLevel.Information, Message = "Suspension appeal refused: not allowed")]
    private static partial void LogNotAllowed(ILogger logger);
}

/// <summary>Límite de 5 intentos por hora por huella de número (spec 060, R-07), en la memoria del proceso como los demás límites por número de Identidad.</summary>
internal sealed class SuspensionAppealLimiter : IDisposable
{
    public const int PermitLimit = 5;

    private readonly PerKeyAttemptLimiter _limiter = new(PermitLimit, TimeSpan.FromHours(1));

    public bool TryAcquire(string key, out TimeSpan retryAfter) => _limiter.TryAcquire(key, out retryAfter);

    public void Dispose() => _limiter.Dispose();
}
