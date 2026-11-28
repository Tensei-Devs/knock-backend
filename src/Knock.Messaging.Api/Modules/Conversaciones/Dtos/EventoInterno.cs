using System.Text.Json;
using System.Text.Json.Serialization;

namespace Knock.Messaging.Api.Modules.Conversaciones.Dtos;

/// <summary>
/// Cuerpo de <c>POST /api/v1/interno/eventos</c> (spec 048, CA-21 y spec 054, R-03): el evento <c>CotizacionElegida</c> que Mercado entrega desde su bandeja de salida. Solo lleva identificadores; cualquier
/// otro campo (texto libre) queda en <see cref="Extra"/> y el validador lo rechaza con 422.
/// </summary>
internal sealed record EventoInterno(string? EventoId, string? Tipo, string? SolicitudId, string? TecnicoUsuarioId)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}
