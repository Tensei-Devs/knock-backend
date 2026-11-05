namespace Knock.Identity.Api.Modules.Usuarios.Domain;

/// <summary>Estado de la cuenta que el perfil necesita conocer (spec 017, R-12). El módulo Sesiones es el dueño del ciclo de vida; aquí solo se lee.</summary>
internal static class ProfileStates
{
    public const string DeletionScheduled = "eliminacion_programada";
}

/// <summary>
/// Perfil de la persona (spec 017, R-01): lo que ve ella misma. Nunca lleva el celular ni su hash. <c>Id</c> es el <c>sub</c> público,
/// no el <c>_id</c> interno.
/// </summary>
internal sealed record UserProfile(
    string Id,
    string Name,
    string? PhotoUrl,
    string? Colonia,
    string? PostalCode,
    bool IsTechnician,
    string State,
    bool ProfileComplete,
    DateTime CreatedAt,
    int Version)
{
    /// <summary>Sin datos: un registro del perfil imprimiría el nombre (spec 017, R-10).</summary>
    public sealed override string ToString() => nameof(UserProfile);
}

/// <summary>Los cuatro campos que la persona puede cambiar (spec 017, R-04 y R-08). Los opcionales vacíos se guardan como «sin valor».</summary>
internal sealed record ProfileUpdate(string Name, string? PhotoUrl, string? Colonia, string? PostalCode)
{
    public sealed override string ToString() => nameof(ProfileUpdate);
}

/// <summary>Resultado de guardar: el perfil nuevo, o el motivo por el que no se guardó.</summary>
internal enum ProfileUpdateOutcome
{
    Updated,
    NotFound,
    DeletionScheduled,
}
