using Microsoft.AspNetCore.Mvc;

namespace Knock.Messaging.Api.Hosting;

/// <summary>Cuerpo de error uniforme (ProblemDetails) para el 401 y el 429 que escriben el JWT y el limitador; nunca repite datos de la petición.</summary>
internal static class ProblemResponses
{
    public static IServiceCollection AddKnockProblemDetails(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddProblemDetails();
        return services;
    }

    public static async Task WriteAsync(HttpContext context, int statusCode, string title)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.StatusCode = statusCode;
        if (statusCode == StatusCodes.Status401Unauthorized)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
        }

        var problems = context.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = { Status = statusCode, Title = title },
        });
    }
}
