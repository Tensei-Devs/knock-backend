using Knock.Contracts.Safety;

namespace Knock.Identity.Api.Modules.Safety.Services;

/// <summary>
/// Salida de los eventos de bloqueo hacia la bandeja de salida (spec 043, R-23; spec 054). La bandeja todavía no existe: mientras tanto <see cref="PendingBlockEvents"/> solo deja constancia
/// sin identificadores. Cuando exista, se implementa con la escritura en la bandeja y reemplaza el registro.
/// </summary>
internal interface IBlockEvents
{
    Task PublishCreatedAsync(BlockCreated blockCreated, CancellationToken cancellationToken);

    Task PublishRemovedAsync(BlockRemoved blockRemoved, CancellationToken cancellationToken);
}

/// <summary>Sustituto mientras no existe la bandeja de salida: registra que el evento quedó pendiente, sin ids de las dos personas (R-22).</summary>
internal sealed partial class PendingBlockEvents(ILogger<PendingBlockEvents> logger) : IBlockEvents
{
    public Task PublishCreatedAsync(BlockCreated blockCreated, CancellationToken cancellationToken)
    {
        LogPending(logger, BlockCreated.EventType);
        return Task.CompletedTask;
    }

    public Task PublishRemovedAsync(BlockRemoved blockRemoved, CancellationToken cancellationToken)
    {
        LogPending(logger, BlockRemoved.EventType);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 3994, Level = LogLevel.Warning, Message = "Block event {EventType} not delivered: the outbox (spec 054) is not implemented yet")]
    private static partial void LogPending(ILogger logger, string eventType);
}
