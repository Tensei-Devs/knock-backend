using System.Text.Json;
using Knock.Identity.Api.Hosting.Procesos.Reintentos;
using Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;
using Knock.Identity.Api.Modules.Account.Data;
using Knock.Identity.Api.Modules.Addresses;

namespace Knock.Identity.Api.Modules.Account.Services;

/// <summary>
/// Tarea <c>PrepararExportacion</c> (spec 039, R-22; spec 054, R-09). Junta los datos de la persona en Identidad y en Mercado, arma el ZIP (un JSON por servicio: identidad, mercado y mensajería, esta última solo con los mensajes propios), lo guarda en el contenedor privado
/// <c>exportaciones</c>, deja el aviso fijo sin enlace y quita la marca de exportación en curso. Es idempotente: reintentar sobrescribe el mismo archivo. Si agota los reintentos quita la marca para que la
/// persona pueda pedirla de nuevo. Los registros no llevan datos de la persona ni el enlace (R-26).
/// </summary>
internal sealed partial class PrepararExportacionHandler(
    IAccountExportSource source,
    IAddressesApi addresses,
    IMarketExport market,
    IMessagingExport messaging,
    IExportStorage storage,
    IExportNotice notice,
    IAccountStore accounts,
    TimeProvider clock,
    ILogger<PrepararExportacionHandler> logger) : IManejadorDeTarea
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string Tipo => AccountOutbox.ExportTask;

    public async Task HandleAsync(TareaReservada task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        var userId = task.ClaveNegocio.Split(':')[0];
        try
        {
            await ExportAsync(userId, task.ClaveNegocio, cancellationToken);
        }
        catch (Exception) when (task.Intentos + 1 >= PoliticaDeReintentos.MaxIntentos && !cancellationToken.IsCancellationRequested)
        {
            // Sin más reintentos la marca bloquearía a la persona para siempre: se quita y el error sigue su camino.
            await accounts.ClearExportAsync(userId, CancellationToken.None);
            LogGaveUp(logger);
            throw;
        }
    }

    private async Task ExportAsync(string userId, string businessKey, CancellationToken cancellationToken)
    {
        if (await source.FindAsync(userId, cancellationToken) is not { } account)
        {
            LogNoAccount(logger);
            return;
        }

        var stored = await addresses.ExportAsync(userId, cancellationToken);
        var identity = new
        {
            perfil = new { account.Name, account.Colonia, account.PostalCode, account.IsTechnician, account.State, account.CreatedAt },
            consentimientos = account.Consents.Select(c => new { c.Document, c.Version, c.At, c.Active }),
            direcciones = stored.Select(a => new { a.Street, a.Colonia, a.References, a.Principal, a.CreatedAt }),
        };
        var files = new Dictionary<string, string>
        {
            [ExportArchive.IdentityFile] = JsonSerializer.Serialize(identity, _json),
            [ExportArchive.MarketFile] = await market.ExportJsonAsync(account.Sub, cancellationToken),
            [ExportArchive.MessagingFile] = await messaging.ExportJsonAsync(account.Sub, cancellationToken),
        };
        var name = ExportArchive.BlobName(businessKey);
        await storage.SaveAsync(name, ExportArchive.Build(files), cancellationToken);
        await accounts.SetLastExportAsync(userId, name, clock.GetUtcNow().UtcDateTime, cancellationToken);
        await notice.NotifyReadyAsync(userId, account.Sub, businessKey.Replace(':', '-'), cancellationToken);
        await accounts.ClearExportAsync(userId, cancellationToken);
        LogReady(logger);
    }

    [LoggerMessage(EventId = 3919, Level = LogLevel.Information, Message = "Data export ready and the person was notified")]
    private static partial void LogReady(ILogger logger);

    [LoggerMessage(EventId = 3920, Level = LogLevel.Information, Message = "Data export did nothing: the account no longer exists")]
    private static partial void LogNoAccount(ILogger logger);

    [LoggerMessage(EventId = 3921, Level = LogLevel.Warning, Message = "Data export failed after the last retry; the in-progress mark was cleared")]
    private static partial void LogGaveUp(ILogger logger);
}
