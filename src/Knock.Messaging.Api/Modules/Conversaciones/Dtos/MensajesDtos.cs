namespace Knock.Messaging.Api.Modules.Conversaciones.Dtos;

/// <summary>Cuerpo de <c>POST /conversaciones/{id}/mensajes</c> (spec 030, R-08). El texto se valida en <c>EnviarMensajeRequestValidator</c>.</summary>
internal sealed record EnviarMensajeRequest(string? Texto);
