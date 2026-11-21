using Knock.Identity.Api.Modules.Trust.Data;

namespace Knock.Identity.Api.Modules.Trust.Services;

/// <summary>
/// Vencimiento de fotos (spec 025, R-16 y CA-16): a los 7 días en <c>en_revision</c> sin decisión borra los blobs, la llave envuelta y deja la verificación en <c>sin_verificar</c> con
/// <c>fotos_borradas</c>. SEGURIDAD: primero se borran los blobs y solo después se destruye la llave y se cambia el estado; si el borrado falla, la verificación sigue vencida y la
/// siguiente corrida la reintenta. Es idempotente y no registra ids, rutas ni contenido.
/// </summary>
internal sealed partial class VerificationExpiryService(IVerificationExpiryStore store, FotoVerificacionStore photos, TimeProvider clock, ILogger<VerificationExpiryService> logger)
{
    public const string TaskName = "vencimiento-verificaciones";
    public const int BatchSize = 100;

    /// <summary>Corre una vez; devuelve cuántas verificaciones quedaron borradas. 0 si otra réplica tiene el arrendamiento.</summary>
    public async Task<int> RunOnceAsync(TimeSpan lease, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (!await store.TryAcquireLeaseAsync(TaskName, now, now + lease, cancellationToken))
        {
            return 0;
        }

        var cleaned = 0;
        foreach (var expired in await store.FindExpiredAsync(now, BatchSize, cancellationToken))
        {
            try
            {
                await photos.PurgeAsync(expired.Id, expired.PhotoTypes, cancellationToken);
                if (await store.MarkPhotosDeletedAsync(expired.Id, now, cancellationToken))
                {
                    cleaned++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(logger, ex.GetType().Name);
            }
        }

        return cleaned;
    }

    [LoggerMessage(EventId = 3992, Level = LogLevel.Warning, Message = "A verification could not be purged and will be retried ({ErrorType})")]
    private static partial void LogFailed(ILogger logger, string errorType);
}

/// <summary>Servicio en segundo plano que corre el vencimiento cada hora.</summary>
internal sealed partial class VencimientoVerificaciones(IServiceScopeFactory scopes, ILogger<VencimientoVerificaciones> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<VerificationExpiryService>().RunOnceAsync(Interval - TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogRunFailed(logger, ex.GetType().Name);
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    [LoggerMessage(EventId = 3993, Level = LogLevel.Warning, Message = "The verification expiry run failed ({ErrorType})")]
    private static partial void LogRunFailed(ILogger logger, string errorType);
}
