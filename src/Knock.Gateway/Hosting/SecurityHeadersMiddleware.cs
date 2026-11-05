using Microsoft.Extensions.Primitives;

namespace Knock.Gateway.Hosting;

/// <summary>
/// Encabezados de seguridad de toda respuesta del gateway (spec 009, R-02): <c>X-Content-Type-Options</c>, <c>X-Frame-Options</c>,
/// <c>Referrer-Policy</c> y una CSP estricta (HSTS lo pone <c>UseHsts</c>). Se fijan en <c>OnStarting</c> para que valgan también en 401, 429 y 503
/// y no se dupliquen con los del servicio. El <c>Cache-Control</c> de la respuesta se respeta; si el servicio no lo trae, se pone <c>no-store</c>.
/// </summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    private static readonly string[] _leakyHeaders = ["Server", "X-Powered-By", "X-AspNet-Version"];

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.OnStarting(state => Apply((HttpContext)state), context);
        return next(context);
    }

    private static Task Apply(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers.ContentSecurityPolicy = ContentSecurityPolicy;
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
