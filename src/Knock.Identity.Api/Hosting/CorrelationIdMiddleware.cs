// jscpd:ignore-start -- EXC-015
using System.Diagnostics;
using Serilog.Context;

namespace Knock.Identity.Api.Hosting;

/// <summary>Reglas del <c>X-Correlation-Id</c>: las define <c>Knock.Contracts.Correlation.CorrelationId</c> y las comparten todos los servicios (spec 001, R-05).</summary>
internal static class CorrelationId
{
    public const string HeaderName = Knock.Contracts.Correlation.CorrelationId.HeaderName;
    public const string LogProperty = Knock.Contracts.Correlation.CorrelationId.LogProperty;
    public const string ItemKey = Knock.Contracts.Correlation.CorrelationId.ItemKey;
    public const int MaxLength = Knock.Contracts.Correlation.CorrelationId.MaxLength;

    public static bool IsValid(string? value) => Knock.Contracts.Correlation.CorrelationId.IsValid(value);

    public static string Create() => Knock.Contracts.Correlation.CorrelationId.Create();

    /// <summary>Id de la petición actual (lo fija <see cref="CorrelationIdMiddleware"/>); <c>null</c> si el middleware no corrió.</summary>
    public static string? Current(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Items.TryGetValue(ItemKey, out var value) ? value as string : null;
    }
}

/// <summary>
/// Asigna o propaga el <c>X-Correlation-Id</c>: lo valida, lo devuelve en la respuesta (incluso en errores), lo pone en el cuerpo de los errores
/// (vía <see cref="ProblemDetailsExtensions"/>) y en cada log de la petición con <c>LogContext</c>. Es solo para rastrear, nunca para autorizar.
/// </summary>
internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var incoming = context.Request.Headers[CorrelationId.HeaderName];
        var id = incoming.Count == 1 && CorrelationId.IsValid(incoming[0]) ? incoming[0]! : CorrelationId.Create();

        context.Items[CorrelationId.ItemKey] = id;
        // Enlaza el id con la traza de la petición (TT-OBS-01): mismo valor que el encabezado de respuesta.
        Activity.Current?.SetTag(ObservabilityExtensions.CorrelationIdAttribute, id);
        // OnStarting y no un encabezado directo: el manejador de excepciones limpia la respuesta antes de escribir el error.
        context.Response.OnStarting(
            state =>
            {
                var (response, value) = ((HttpResponse, string))state;
                response.Headers[CorrelationId.HeaderName] = value;
                return Task.CompletedTask;
            },
            (context.Response, id));

        using (LogContext.PushProperty(CorrelationId.LogProperty, id))
        {
            await next(context);
        }
    }
}
// jscpd:ignore-end
