using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Knock.Messaging.Api.Hosting;

/// <summary>
/// Tope del cuerpo de una acción (spec 009, R-17): antes de leer nada responde 413 ProblemDetails si <c>Content-Length</c> lo excede y, para cuerpos sin longitud declarada, fija el mismo
/// tope en el servidor. Corre antes del enlace de datos, así un cuerpo excesivo nunca llega a deserializarse ni se repite en la respuesta.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class BodyLimitAttribute(int maxBytes) : Attribute, IAsyncResourceFilter
{
    public const string PayloadTooLargeCode = "payload_too_large";

    public int MaxBytes { get; } = maxBytes;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var http = context.HttpContext;
        if (http.Request.ContentLength is { } length && length > MaxBytes)
        {
            await ProblemResponses.WriteAsync(http, StatusCodes.Status413PayloadTooLarge, "Payload Too Large", PayloadTooLargeCode);
            context.Result = new EmptyResult();
            return;
        }

        var feature = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
        {
            feature.MaxRequestBodySize = MaxBytes;
        }

        await next();
    }
}
