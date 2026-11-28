using Knock.Messaging.Api.Modules.Conversaciones.Data;

namespace Knock.Messaging.Api.Modules.Conversaciones.Services;

/// <summary>Respuesta de la exportación de Mensajería (spec 039, R-22): el contenido de <c>mensajeria.json</c>, solo mensajes propios.</summary>
internal sealed record ExportacionMensajeriaResponse(IReadOnlyList<ConversacionExportada> Conversaciones);

/// <summary>Arma la exportación de datos personales de Mensajería (spec 039, R-22): únicamente los mensajes que escribió la persona del <c>sub</c> ya validado por el controlador.</summary>
internal sealed class ExportacionService(IExportacionStore store)
{
    public async Task<ExportacionMensajeriaResponse> ExportAsync(string sub, CancellationToken cancellationToken) =>
        new(await store.ListOwnMessagesAsync(sub, cancellationToken));
}
