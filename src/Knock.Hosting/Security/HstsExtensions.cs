using System.Globalization;
using Microsoft.Extensions.Options;

namespace Knock.Hosting.Security;

/// <summary>
/// HSTS fijado por el middleware de encabezados de seguridad (<c>Security:Hsts</c>, BUG-002): <c>max-age</c> e <c>includeSubDomains</c>. Nunca <c>preload</c>. Los valores por defecto son los
/// documentados en <c>docs/security-headers.md</c> (365 días con subdominios); un valor menor o igual a cero impide el arranque.
/// </summary>
public sealed class HstsSettings
{
    public const string SectionName = "Security:Hsts";

    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(365);

    public bool IncludeSubDomains { get; set; } = true;

    /// <summary>Envía el encabezado también en Development, para el ambiente de desarrollo desplegado con HTTPS; nunca a <c>localhost</c> (BUG-002, enmienda 2026-11-27). Apagado por omisión.</summary>
    public bool SendInDevelopment { get; set; }

    /// <summary>Valor del encabezado <c>Strict-Transport-Security</c>: siempre en segundos enteros y sin <c>preload</c>.</summary>
    public string HeaderValue =>
        $"max-age={((long)MaxAge.TotalSeconds).ToString(CultureInfo.InvariantCulture)}{(IncludeSubDomains ? "; includeSubDomains" : string.Empty)}";
}

public static class HstsExtensions
{
    /// <summary>Registra y valida <c>Security:Hsts</c> al arrancar; el mensaje nombra la clave y nunca repite el valor.</summary>
    public static IServiceCollection AddKnockHsts(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<HstsSettings>()
            .BindConfiguration(HstsSettings.SectionName)
            .Validate(settings => settings.MaxAge > TimeSpan.Zero, "Security:Hsts:MaxAge must be greater than zero.")
            .ValidateOnStart();
        return services;
    }
}
