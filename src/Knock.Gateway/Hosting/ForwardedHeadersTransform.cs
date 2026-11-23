using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Knock.Gateway.Hosting;

/// <summary>
/// Spec 035, R-13: al reenviar a un servicio, el gateway reemplaza <c>X-Forwarded-For</c> por un único valor (la IP del cliente que él resolvió), fija
/// <c>X-Forwarded-Proto</c> al esquema resuelto y quita los encabezados reenviados que escribió el cliente. Así un servicio nunca ve valores del cliente.
/// </summary>
internal static class ForwardedHeadersTransform
{
    private static readonly string[] _removed = ["X-Forwarded-Host", "X-Forwarded-Prefix", "Forwarded", "X-Original-For"];

    public static IReverseProxyBuilder AddKnockForwardedHeadersTransform(this IReverseProxyBuilder builder)
    {
        return builder.AddTransforms(context =>
        {
            // Declarar el transformador X-Forwarded desactiva los de YARP por defecto (que agregan a lo que trae el cliente); lo propio se escribe abajo.
            context.AddXForwarded(ForwardedTransformActions.Remove);
            context.AddRequestTransform(transform =>
            {
                Apply(transform.HttpContext, transform.ProxyRequest.Headers);
                return ValueTask.CompletedTask;
            });
        });
    }

    internal static void Apply(HttpContext http, System.Net.Http.Headers.HttpRequestHeaders headers)
    {
        foreach (var name in _removed)
        {
            headers.Remove(name);
        }

        headers.Remove("X-Forwarded-For");
        headers.Remove("X-Forwarded-Proto");
        var ip = http.Connection.RemoteIpAddress;
        if (ip is not null)
        {
            headers.TryAddWithoutValidation("X-Forwarded-For", ip.ToString());
        }

        headers.TryAddWithoutValidation("X-Forwarded-Proto", http.Request.Scheme);
    }
}
