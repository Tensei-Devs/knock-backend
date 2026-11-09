namespace Knock.Identity.Api.Modules.Usuarios.Dtos;

/// <summary>
/// Cuerpo del <c>PUT /me</c> (spec 017, R-04 y R-08): solo los cuatro campos editables. Cualquier otro campo del JSON (<c>sub</c>, <c>estado</c>,
/// <c>esTecnico</c>…) no existe aquí, así que se ignora. Los campos son anulables a propósito: la validación propia responde 400 por campo.
/// </summary>
internal sealed record UpdateProfileRequest(string? Nombre = null, string? FotoUrl = null, string? Colonia = null, string? CodigoPostal = null);

/// <summary>Perfil de la persona (spec 017, R-01). Nunca incluye el celular, su hash ni datos de otra persona.</summary>
internal sealed record ProfileResponse(
    string Id,
    string Nombre,
    string? FotoUrl,
    string? Colonia,
    string? CodigoPostal,
    bool EsTecnico,
    string Estado,
    bool PerfilCompleto,
    DateTime CreadoEn);
