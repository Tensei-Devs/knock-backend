using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Knock.Identity.Api.Hosting;

/// <summary>
/// El 400 automático de <c>[ApiController]</c> (cuerpo con JSON de un tipo incorrecto o mal formado) sin el texto interno de .NET («The JSON value
/// could not be converted to System.String…»): cada campo con error recibe el mismo motivo genérico que el resto de la API (KNOCK-203, H-2).
/// </summary>
internal static class ModelStateProblems
{
    public static IServiceCollection AddKnockModelStateProblems(this IServiceCollection services) =>
        services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = Create);

    private static BadRequestObjectResult Create(ActionContext context)
    {
        var sanitized = new ModelStateDictionary();
        foreach (var (key, entry) in context.ModelState)
        {
            var field = FieldOf(key);
            if (entry.Errors.Count > 0 && !sanitized.ContainsKey(field))
            {
                sanitized.AddModelError(field, ValidationFilter.GenericReason);
            }
        }

        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateValidationProblemDetails(context.HttpContext, sanitized, StatusCodes.Status400BadRequest);
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    private static string FieldOf(string key)
    {
        var path = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;
        return path is "" or "$" ? "body" : ValidationFilter.ToCamelCasePath(path);
    }
}
