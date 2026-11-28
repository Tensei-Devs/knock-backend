using System.Text.Json;
using System.Text.Json.Serialization;

namespace Knock.Messaging.Api.Modules.Bloqueos.Dtos;

/// <summary>
/// Cuerpo de <c>POST /api/v1/interno/bloqueos</c> (spec 043, R-23): <c>BloqueoCreado</c> o <c>BloqueoEliminado</c> que Identidad entrega por su bandeja de salida. Solo identificadores y fecha;
/// cualquier otro campo queda en <see cref="Extra"/> y el validador lo rechaza con 422.
/// </summary>
internal sealed record EventoDeBloqueo(string? EventoId, string? Tipo, string? BloqueaId, string? BloqueadoId, DateTime? OcurridoEn)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}
