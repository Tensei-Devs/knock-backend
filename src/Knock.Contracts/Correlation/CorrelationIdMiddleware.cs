using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Knock.Contracts.Correlation;

/// <summary>
/// Asigna o reenvía el <c>X-Correlation-Id</c>: lo valida, lo deja en la petición (para que el gateway lo reenvíe al servicio),
/// lo devuelve en la respuesta y lo escribe en cada registro de la petición (spec 001, R-05).
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    /// <summary>Procesa la petición.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var incoming = context.Request.Headers[CorrelationId.HeaderName];
        var id = incoming.Count == 1 && CorrelationId.IsValid(incoming[0]) ? incoming[0]! : CorrelationId.Create();

        context.Items[CorrelationId.ItemKey] = id;
        context.Request.Headers[CorrelationId.HeaderName] = id;
        // OnStarting y no un encabezado directo: así se escribe una sola vez aunque el servicio de destino también lo devuelva.
        context.Response.OnStarting(
            state =>
            {
                var ctx = (HttpContext)state;
                ctx.Response.Headers[CorrelationId.HeaderName] = id;
                return Task.CompletedTask;
            },
            context);

        using (logger.BeginScope(new Dictionary<string, object> { [CorrelationId.LogProperty] = id }))
        {
            await next(context);
        }
    }
}

/// <summary>Extensión para registrar el middleware de correlación.</summary>
public static class CorrelationIdApplicationBuilderExtensions
{
    /// <summary>Agrega el middleware de correlación; debe ir al inicio de la tubería.</summary>
    public static IApplicationBuilder UseKnockCorrelationId(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
