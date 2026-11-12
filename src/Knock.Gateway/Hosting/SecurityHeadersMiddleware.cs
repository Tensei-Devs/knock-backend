using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Knock.Gateway.Hosting;

/// <summary>
/// Encabezados de seguridad de toda respuesta del gateway (spec 009, R-02): <c>X-Content-Type-Options</c>, <c>X-Frame-Options</c>,
/// <c>Referrer-Policy</c>, una CSP estricta y, fuera de Development, <c>Strict-Transport-Security</c> (BUG-002). Este último se fija aquí y no con
/// <c>UseHsts</c>, que solo responde cuando <c>Request.IsHttps</c> es verdadero: detrás del ingreso la petición llega como HTTP y el encabezado no salía nunca. Es inofensivo por
/// HTTP (los navegadores lo ignoran) y no depende de <c>X-Forwarded-Proto</c>. Se fijan en <c>OnStarting</c> para que valgan también en 401, 429 y 503
/// y no se dupliquen con los del servicio. El <c>Cache-Control</c> de la respuesta se respeta; si el servicio no lo trae, se pone <c>no-store</c>.
/// </summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment, IOptions<HstsSettings> hsts)
{
    public const string ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    private static readonly string[] _leakyHeaders = ["Server", "X-Powered-By", "X-AspNet-Version"];

    /// <summary>En Development no se envía: fijaría HSTS en <c>localhost</c> del navegador de quien desarrolla.</summary>
    private readonly string? _strictTransportSecurity = environment.IsDevelopment() ? null : hsts.Value.HeaderValue;

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
        headers.ContentSecurityPolicy = ContentSecurityPolicy;
        if (_strictTransportSecurity is not null)
        {
            headers.StrictTransportSecurity = _strictTransportSecurity;
        }

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
}
