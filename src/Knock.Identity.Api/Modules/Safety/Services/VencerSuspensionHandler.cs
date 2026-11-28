using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;
using Knock.Identity.Api.Modules.Safety.Domain;
using Knock.Identity.Api.Modules.Sesiones;

namespace Knock.Identity.Api.Modules.Safety.Services;

/// <summary>
/// Tarea <c>VencerSuspension</c> (spec 060, R-06a; spec 054, R-09). Al cumplirse los días de la suspensión gradual la reactiva con <c>Reactivar</c> (spec 059, R-15). Es idempotente: si la cuenta ya no está
/// suspendida por ese reporte (apelación aceptada, otra suspensión) o la suspensión no es gradual no cambia nada; la crítica no vence nunca. La clave es <c>{sub}:{reporteId}</c>, solo identificadores.
/// </summary>
internal sealed partial class VencerSuspensionHandler(ISesionesApi accounts, AccountSuspension suspension, TimeProvider clock, ILogger<VencerSuspensionHandler> logger) : IManejadorDeTarea
{
    public string Tipo => CatalogoDeTareas.VencerSuspension;

    public async Task HandleAsync(TareaReservada task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        var parts = task.ClaveNegocio.Split(':');
        if (parts.Length != 2)
        {
            LogBadKey(logger);
            return;
        }

        var current = await accounts.FindSuspensionBySubAsync(parts[0], cancellationToken);
        if (current is null || current.ReportId != parts[1] || !AccountSuspensionRules.HasExpired(current.Type, current.ExpiresAt, clock.GetUtcNow().UtcDateTime))
        {
            LogSkipped(logger);
            return;
        }

        try
        {
            await suspension.ReactivarAsync(parts[0], parts[1], AccountSuspension.SystemActor, cancellationToken);
            LogDone(logger);
        }
        catch (DomainException exception) when (exception.Code == SafetyErrorCodes.AccountNotSuspendedCode)
        {
            // Una apelación aceptada la reactivó entre la lectura y la escritura: no hay nada que hacer.
            LogSkipped(logger);
        }
    }

    [LoggerMessage(EventId = 6011, Level = LogLevel.Warning, Message = "Suspension expiry task ignored: the business key is not valid")]
    private static partial void LogBadKey(ILogger logger);

    [LoggerMessage(EventId = 6012, Level = LogLevel.Information, Message = "Suspension expiry task left the account as it was")]
    private static partial void LogSkipped(ILogger logger);

    [LoggerMessage(EventId = 6013, Level = LogLevel.Information, Message = "Suspension expiry task reactivated an account")]
    private static partial void LogDone(ILogger logger);
}
