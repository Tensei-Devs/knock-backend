using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Account.Data;
using Knock.Identity.Api.Modules.Account.Domain;
using Knock.Identity.Api.Modules.Account.Dtos;

namespace Knock.Identity.Api.Modules.Account.Services;

/// <summary>
/// Consulta de la última exportación (spec 039, R-22 y enmienda del 2026-11-23; USR-06b). El aviso <c>exportacion_lista</c> no lleva enlace: la app lo pide aquí al abrir el aviso. En <c>lista</c> se firma en
/// ese momento un enlace de solo lectura de 24 horas; el ZIP vive 7 días en el contenedor (ciclo de vida de Terraform), después queda <c>vencida</c>. El enlace no se guarda, no se registra y no se cachea.
/// </summary>
internal sealed partial class LatestExportService(IAccountStore store, IExportStorage storage, TimeProvider clock, ILogger<LatestExportService> logger)
{
    /// <summary>Validez del enlace de descarga (R-22).</summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(24);

    /// <summary>Conservación del ZIP en el contenedor <c>exportaciones</c> (7 días).</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    /// <exception cref="DomainException">404 <c>export_not_found</c> si la persona nunca pidió una exportación o la cuenta no existe; 503 <c>export_unavailable</c> si el contenedor no responde.</exception>
    public async Task<LatestExportResponse> GetAsync(string sub, CancellationToken cancellationToken)
    {
        var status = await store.FindExportStatusAsync(sub, cancellationToken);
        if (status is null || (!status.InProgress && status.BlobName is null))
        {
            throw new DomainException(DomainErrorKind.NotFound, "The person has not requested an export.", AccountErrorCodes.ExportNotFound);
        }

        if (status.InProgress)
        {
            return new LatestExportResponse(ExportStates.Preparing, null, null);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (status.ReadyAt is not { } readyAt || readyAt + Retention <= now)
        {
            return new LatestExportResponse(ExportStates.Expired, null, null);
        }

        try
        {
            var link = await storage.CreateLinkAsync(status.BlobName!, LinkLifetime, cancellationToken);
            return new LatestExportResponse(ExportStates.Ready, link.AbsoluteUri, now + LinkLifetime);
        }
        catch (InvalidOperationException)
        {
            LogUnavailable(logger);
            throw new DomainException(DomainErrorKind.ServiceUnavailable, "The export container is not available.", AccountErrorCodes.ExportUnavailable);
        }
    }

    [LoggerMessage(EventId = 3922, Level = LogLevel.Warning, Message = "The export container could not sign a link")]
    private static partial void LogUnavailable(ILogger logger);
}
