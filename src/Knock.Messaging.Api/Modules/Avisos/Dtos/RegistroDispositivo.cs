namespace Knock.Messaging.Api.Modules.Avisos.Dtos;

internal sealed record RegistroDispositivo(string? Identificador, string? Plataforma);

/// <summary>Respuesta de <c>POST /dispositivos</c>: el id del registro (nunca el token de notificaciones, spec 037, R-03).</summary>
internal sealed record IdDispositivo(string Id);
