using Microsoft.AspNetCore.Http;

namespace Knock.Contracts.Correlation;

/// <summary>
/// Reglas del <c>X-Correlation-Id</c> que comparten todos los servicios: máximo 64 caracteres de <c>[A-Za-z0-9._-]</c>;
/// si no cumple, se genera un GUID sin guiones. Es solo para rastrear peticiones, nunca para autorizar.
/// </summary>
public static class CorrelationId
{
    /// <summary>Nombre del encabezado que viaja del cliente al gateway y de ahí a cada servicio.</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>Clave con la que el middleware deja el identificador en <see cref="HttpContext.Items"/>.</summary>
    public const string ItemKey = "Knock.CorrelationId";

    /// <summary>Nombre de la propiedad con la que se escribe el identificador en los registros.</summary>
    public const string LogProperty = "CorrelationId";

    /// <summary>Longitud máxima aceptada.</summary>
    public const int MaxLength = 64;

    /// <summary>Indica si el valor recibido se puede reutilizar como identificador de correlación.</summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return false;
        }

        foreach (var c in value)
        {
            var allowed = c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '.' or '_' or '-';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Genera un identificador nuevo.</summary>
    public static string Create() => Guid.NewGuid().ToString("N");

    /// <summary>Identificador de la petición actual; <c>null</c> si el middleware no corrió.</summary>
    public static string? Current(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Items.TryGetValue(ItemKey, out var value) ? value as string : null;
    }
}
