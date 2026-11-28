using Microsoft.AspNetCore.Http;

namespace Knock.Hosting.Security;

/// <summary>Escribe una respuesta de problema: estado, título y código de error (spec 056, R-03). Lo aporta cada servicio con su propio <c>ProblemResponses</c>.</summary>
public delegate Task ServiceProblemWriter(HttpContext context, int status, string title, string code);

/// <summary>
/// Parámetros de la validación única de la credencial de servicio (spec 056, R-01 a R-06 y R-10; catálogo de la spec 055). Cada servicio fija su esquema, su destinatario
/// (<c>aud</c>) y la clave de configuración de cada emisor aceptado; la regla es la misma para todos.
/// </summary>
public sealed class ServiceCredentialSettings
{
    /// <summary>Nombre del esquema de autenticación.</summary>
    public required string Scheme { get; init; }

    /// <summary>Destinatario esperado (<c>aud</c>).</summary>
    public required string Audience { get; init; }

    /// <summary>Clave de configuración de la clave HMAC de cada emisor aceptado, por <c>iss</c> (cada emisor firma con la suya: spec 056, R-05).</summary>
    public required IReadOnlyDictionary<string, string> IssuerKeyConfigurationKeys { get; init; }

    /// <summary>Escritor de respuestas de 401 y 403 del servicio.</summary>
    public required ServiceProblemWriter WriteProblem { get; init; }

    /// <summary>Código de error del 401.</summary>
    public required string UnauthorizedCode { get; init; }

    /// <summary>Código de error del 403 (sin el permiso que pide la ruta).</summary>
    public string ForbiddenCode { get; init; } = ServiceCredential.ServiceNotAllowedCode;
}
