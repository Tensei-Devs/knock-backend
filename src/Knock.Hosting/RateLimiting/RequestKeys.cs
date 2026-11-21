using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;

namespace Knock.Hosting.RateLimiting;

/// <summary>Claves y rutas que comparten el limitador de los servicios y el del gateway.</summary>
public static class RequestKeys
{
    public const string UnknownClient = "unknown";

    /// <summary>
    /// IP de la conexión (ya sustituida por la reenviada solo si viene de un proxy de confianza). IPv4 mapeada se normaliza y las IPv6
    /// se agrupan por /64 para que rotar direcciones de un mismo prefijo no evada el límite.
    /// </summary>
    public static string ClientKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return UnknownClient;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return $"{new IPAddress(bytes)}/64";
    }

    /// <summary>Es <c>/api/v{version}/{segment}</c> o lo que cuelga de ahí; sin distinguir mayúsculas porque el enrutador tampoco las distingue.</summary>
    public static bool IsApiPath(PathString path, string segment)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return segments.Length >= 3
            && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            && segments[1][0] is 'v' or 'V'
            && segments[2].Equals(segment, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Spec 033, R-27: <c>/api/v{n}/panel/sesion/*</c> y <c>/api/v{n}/panel/activacion</c>, los endpoints de sesión del panel que llevan el límite estricto por IP.</summary>
    public static bool IsPanelSessionPath(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return IsApiPath(path, "panel")
            && segments.Length >= 4
            && (segments[3].Equals("sesion", StringComparison.OrdinalIgnoreCase) || segments[3].Equals("activacion", StringComparison.OrdinalIgnoreCase));
    }
}
