using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Knock.Hosting.Security;

/// <summary>
/// Lo que cambia entre el gateway y los servicios en los encabezados de seguridad: <see cref="StrictTransportSecurity"/> (el gateway lo fija aquí porque detrás del ingreso la
/// petición llega como HTTP y <c>UseHsts</c> no respondería nunca; los servicios usan <c>UseHsts</c>) y <see cref="RelaxContentSecurityPolicyForApiDocsInDevelopment"/> (Mercado e Identidad sirven
/// Scalar en Development).
/// </summary>
public sealed record SecurityHeadersOptions
{
    /// <summary>Valor de <c>Strict-Transport-Security</c>; <c>null</c> no lo envía. En Development nunca se envía: fijaría HSTS en <c>localhost</c> del navegador de quien desarrolla.</summary>
    public string? StrictTransportSecurity { get; init; }

    /// <summary>En Development, y solo para <c>/scalar</c> y <c>/openapi</c>, la CSP estricta no se aplica (el resto sí).</summary>
    public bool RelaxContentSecurityPolicyForApiDocsInDevelopment { get; init; }
}

/// <summary>
/// Encabezados de seguridad de respuesta (TT-API-06, spec 009 R-02). Se fijan en <c>Response.OnStarting</c> y por asignación: el manejador de excepciones limpia los
/// encabezados antes de escribir el error y el limitador responde antes que el resto, así que ninguna respuesta (200, 4xx, 429, 500) queda sin ellos
/// y ninguno se duplica. Va antes del manejador de excepciones. El <c>Cache-Control</c> del endpoint se respeta; si no trae, se pone <c>no-store</c>.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment, SecurityHeadersOptions options)
{
    public const string ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    private static readonly string[] _leakyHeaders = ["Server", "X-Powered-By", "X-AspNet-Version"];

    private readonly string? _strictTransportSecurity = environment.IsDevelopment() ? null : options.StrictTransportSecurity;

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.OnStarting(state => Apply((HttpContext)state), context);
        return next(context);
    }

    private Task Apply(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        if (SkipsContentSecurityPolicy(context.Request.Path))
        {
            headers.Remove(HeaderNames.ContentSecurityPolicy);
        }
        else
        {
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
        }

        if (_strictTransportSecurity is not null)
        {
            headers.StrictTransportSecurity = _strictTransportSecurity;
        }

        // `no-store` solo si el endpoint no declaró el suyo: un endpoint con ETag podrá fijar su propio Cache-Control.
        if (StringValues.IsNullOrEmpty(headers.CacheControl))
        {
            headers.CacheControl = "no-store";
        }

        foreach (var leaky in _leakyHeaders)
        {
            headers.Remove(leaky);
        }

        return Task.CompletedTask;
    }

    /// <summary>Scalar sirve HTML y scripts: en Development, y solo para su ruta y la del documento OpenAPI, la CSP estricta no se aplica (el resto sí).</summary>
    private bool SkipsContentSecurityPolicy(PathString path) =>
        options.RelaxContentSecurityPolicyForApiDocsInDevelopment
        && environment.IsDevelopment()
        && (path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase) || path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase));
}

/// <summary>Registro del middleware de encabezados de seguridad.</summary>
public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseKnockSecurityHeaders(this IApplicationBuilder app, SecurityHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(options);
        return app.UseMiddleware<SecurityHeadersMiddleware>(options);
    }
}
