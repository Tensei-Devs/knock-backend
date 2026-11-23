namespace Knock.Market.Api.Modules.Tecnicos;

/// <summary>Contrato público del módulo Tecnicos hacia el resto del servicio (spec 052, §8: el asistente solo atiende cuentas con perfil de técnico; spec 039: exportación y anonimización del perfil).</summary>
public interface ITecnicosApi
{
    string ModuleName { get; }

    /// <summary>Verdadero si la cuenta del <paramref name="userId"/> tiene perfil de técnico.</summary>
    Task<bool> HasProfileAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Perfil del técnico de la persona como texto JSON para su exportación (spec 039, R-22): solo campos que la persona dio, sin dirección del taller ni ubicación. <see langword="null"/> si no es técnica.</summary>
    Task<string?> ExportProfileJsonAsync(string accountSub, CancellationToken cancellationToken);

    /// <summary>
    /// Oculta y anonimiza el perfil del técnico de la persona que eliminó su cuenta y borra sus fotos de trabajos (spec 039, R-25). Conserva el identificador del perfil para no romper el historial de
    /// quien lo contrató. Repetirlo no cambia nada.
    /// </summary>
    Task AnonymizeProfileAsync(string accountSub, CancellationToken cancellationToken);
}
