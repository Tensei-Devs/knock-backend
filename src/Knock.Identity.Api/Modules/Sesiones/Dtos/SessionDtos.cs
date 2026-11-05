namespace Knock.Identity.Api.Modules.Sesiones.Dtos;

/// <summary>
/// Petición del canje (spec 015, R-03). Los campos son anulables a propósito y no se declaran requeridos en el esquema: si falta alguno, el validador
/// responde 422 con <c>errors</c> en su campo, en lugar del 400 automático de MVC. El celular y el <c>uid</c> no se aceptan: salen del token verificado.
/// </summary>
internal sealed record FirebaseExchangeRequest(string? FirebaseIdToken = null, string? DeviceId = null, bool? AceptaAviso = null, string? Dispositivo = null);

/// <summary>Sesión emitida. <c>EsNuevo</c> distingue el 201 (cuenta creada) del 200 (cuenta existente).</summary>
internal sealed record SessionResponse(string AccessToken, string RefreshToken, bool EsNuevo, bool PerfilCompleto);

/// <summary>Petición de renovación (spec 016, R-01). El campo es anulable a propósito: si falta, el validador responde 422 en su campo.</summary>
internal sealed record RefreshRequest(string? RefreshToken = null);

/// <summary>Par nuevo de la renovación (spec 016, contrato). <c>ExpiresIn</c> son los segundos de vigencia del JWT de acceso.</summary>
internal sealed record RefreshResponse(string AccessToken, int ExpiresIn, string RefreshToken);
