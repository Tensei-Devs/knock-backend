namespace Knock.Messaging.Api.Modules.Conversaciones.Dtos;

/// <summary>Cuerpo de <c>POST /conversaciones/{id}/mensajes</c> (spec 030, R-08). El texto se valida en <c>EnviarMensajeRequestValidator</c>.</summary>
internal sealed record EnviarMensajeRequest(string? Texto);

/// <summary>El mensaje como lo ve la app (R-08 y R-16): sin <c>sub</c>, nombre ni teléfono de quien lo escribió. <c>ContactoOculto</c> avisa que se reemplazó un dato de contacto.</summary>
internal sealed record MensajeResponse(string MensajeId, string AutorRol, string Texto, bool ContactoOculto, DateTimeOffset EnviadoEn);
