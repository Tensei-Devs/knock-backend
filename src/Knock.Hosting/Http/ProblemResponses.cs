using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Hosting.Http;

/// <summary>
/// Lo que cambia entre el gateway y los servicios en el cuerpo de error (spec 055, R-05): <see cref="W3CTraceId"/> (Identidad deja el <c>traceId</c> por defecto de ASP.NET Core, el <c>Activity.Id</c> completo) y
/// <see cref="ExposeExceptionDetail"/> (el gateway nunca escribe el detalle de una excepción).
/// </summary>
public sealed record KnockProblemDetailsOptions
{
    /// <summary>El <c>traceId</c> es el identificador de traza W3C (32 hexadecimales) en vez del <c>Activity.Id</c> completo que escribe ASP.NET Core por defecto.</summary>
    public bool W3CTraceId { get; init; } = true;

    /// <summary>Permite escribir el mensaje y la traza de la excepción en Development o con <c>Errores:ExponerDetalle</c>.</summary>
    public bool ExposeExceptionDetail { get; init; } = true;
}

/// <summary>
/// Cuerpo de error uniforme (ProblemDetails, RFC 9457) para el gateway y los tres servicios: lo que escriben el manejador de excepciones, el JWT, el limitador y los middleware.
/// Nunca repite datos de la petición; <c>instance</c> es la plantilla de la ruta.
/// </summary>
public static class ProblemResponses
{
    /// <summary>Clave con la que el middleware de correlación deja el identificador en <c>HttpContext.Items</c> (la misma de <c>Knock.Contracts.Correlation.CorrelationId.ItemKey</c>; esta biblioteca no referencia a Contracts, R-02).</summary>
    public const string CorrelationItemKey = "Knock.CorrelationId";

    /// <summary>RFC 9457 para toda respuesta de error, con <c>correlationId</c> y, según <paramref name="options"/>, <c>traceId</c> y el detalle de la excepción solo en Development.</summary>
    public static IServiceCollection AddKnockProblemDetails(this IServiceCollection services, KnockProblemDetailsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var settings = options ?? new KnockProblemDetailsOptions();
        services.AddProblemDetails(problemOptions => problemOptions.CustomizeProblemDetails = context => Customize(context, settings));
        return services;
    }

    /// <summary>Escribe el error por <see cref="IProblemDetailsService"/> con el estado, el título estándar, el <c>code</c> opcional y extensiones opcionales; el 401 lleva <c>WWW-Authenticate: Bearer</c>.</summary>
    public static async Task WriteAsync(HttpContext context, int statusCode, string title, string? code = null, IReadOnlyDictionary<string, string>? extensions = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.StatusCode = statusCode;
        if (statusCode == StatusCodes.Status401Unauthorized)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
        }

        var problems = context.RequestServices.GetRequiredService<IProblemDetailsService>();
        var details = new ProblemDetails { Status = statusCode, Title = title };
        if (code is not null)
        {
            details.Extensions["code"] = code;
        }

        foreach (var (key, value) in extensions ?? new Dictionary<string, string>())
        {
            details.Extensions[key] = value;
        }

        await problems.WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = details });
    }

    private static void Customize(ProblemDetailsContext context, KnockProblemDetailsOptions options)
    {
        var http = context.HttpContext;
        // Sin «type» propio, «about:blank»: todos los errores (401, 429, 413, 422…) tienen la misma forma.
        context.ProblemDetails.Type ??= "about:blank";
        // «instance» es la plantilla de la ruta, nunca la ruta con los identificadores de la petición.
        context.ProblemDetails.Instance = http.GetEndpoint() is RouteEndpoint { RoutePattern.RawText: { } route }
            ? "/" + route.TrimStart('/')
            : http.Request.Path;
        if (options.W3CTraceId)
        {
            context.ProblemDetails.Extensions["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? http.TraceIdentifier;
        }

        if (http.Items.TryGetValue(CorrelationItemKey, out var correlationId) && correlationId is string id)
        {
            context.ProblemDetails.Extensions["correlationId"] = id;
        }

        if (options.ExposeExceptionDetail)
        {
            AddExceptionDetail(context, http);
        }
    }

    private static void AddExceptionDetail(ProblemDetailsContext context, HttpContext http)
    {
        var environment = http.RequestServices.GetRequiredService<IHostEnvironment>();
        var exception = http.Features.Get<IExceptionHandlerFeature>()?.Error;
        // `Errores:ExponerDetalle` apaga el detalle y la traza aun en Development (el entorno de dev de Azure corre así: la respuesta llega a clientes reales por el gateway).
        var exposeDetail = http.RequestServices.GetService<IConfiguration>()?.GetValue("Errores:ExponerDetalle", environment.IsDevelopment()) ?? environment.IsDevelopment();
        if (exception is not null && exposeDetail)
        {
            context.ProblemDetails.Detail = exception.Message;
            context.ProblemDetails.Extensions["stackTrace"] = exception.StackTrace;
        }
    }
}
