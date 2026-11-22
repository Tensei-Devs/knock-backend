using System.Text.Json;
using System.Text.Json.Serialization;

namespace Knock.Messaging.Api.Modules.Avisos.Dtos;

/// <summary>Cuerpo de <c>POST /api/v1/interno/avisos</c> (spec 037, R-11). Cualquier otro campo (texto libre) queda en <see cref="Extra"/> y el validador lo rechaza con 422.</summary>
internal sealed record AvisoInterno(string? EventoId, string? Tipo, string? DestinatarioId, string? Rol, string? Recurso, string? Sid)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}
