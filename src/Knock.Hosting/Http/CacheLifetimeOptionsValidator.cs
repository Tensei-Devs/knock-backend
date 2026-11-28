using Microsoft.Extensions.Options;

namespace Knock.Hosting.Http;

/// <summary>
/// Valida al arrancar la vida en segundos de una caché del servidor (spec 010, R-04): un entero entre un mínimo y un máximo. Cada servicio hereda y dice qué clave de configuración y qué propiedad lee.
/// Los mensajes nombran la clave, nunca el valor.
/// </summary>
public abstract class CacheLifetimeOptionsValidator<TOptions>(string key, int minimumSeconds, int maximumSeconds) : IValidateOptions<TOptions>
    where TOptions : class
{
    /// <summary>La vida configurada en segundos; <c>null</c> si falta o no es un entero.</summary>
    protected abstract int? LifetimeSeconds(TOptions options);

    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (LifetimeSeconds(options) is not { } seconds || seconds < minimumSeconds || seconds > maximumSeconds)
        {
            return ValidateOptionsResult.Fail($"{key} must be an integer from {minimumSeconds} to {maximumSeconds} (seconds).");
        }

        return ValidateOptionsResult.Success;
    }
}
