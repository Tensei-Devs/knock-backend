using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;

namespace Knock.Hosting.Http;

/// <summary>Lo que cambia entre los servicios en el 413 (spec 055, R-05): el <c>code</c> del error; Identidad no lo escribe todavía.</summary>
public sealed record RequestBodyLimitOptions
{
    /// <summary>Valor del campo <c>code</c> del 413; <see langword="null"/> no lo escribe.</summary>
    public string? PayloadTooLargeCode { get; init; }
}

/// <summary>
/// Aplica el límite de cuerpo declarado con <c>[RequestSizeLimit]</c> y responde 413 en ProblemDetails.
/// Con <c>Content-Length</c> se rechaza antes de leer; sin él, el servidor corta la lectura y se traduce a 413.
/// </summary>
public sealed class RequestBodyLimitMiddleware(RequestDelegate next, RequestBodyLimitOptions? options = null)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var limit = context.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize;
        if (limit is null)
        {
            await next(context);
            return;
        }

        if (context.Request.ContentLength > limit)
        {
            await ProblemResponses.WriteAsync(context, StatusCodes.Status413PayloadTooLarge, "Request body too large", options?.PayloadTooLargeCode);
            return;
        }

        var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
        {
            feature.MaxRequestBodySize = limit;
        }

        try
        {
            await next(context);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge && !context.Response.HasStarted)
        {
            await ProblemResponses.WriteAsync(context, StatusCodes.Status413PayloadTooLarge, "Request body too large", options?.PayloadTooLargeCode);
        }
    }
}
