using System.Text.Json.Nodes;
using Knock.Hosting.OpenApi;
using Knock.Messaging.Api.Modules.Conversaciones.Dtos;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Knock.Messaging.Api.Hosting;

/// <summary>Cierra en el contrato los valores que los frontends necesitan (spec 030, R-03 y R-05): <c>estado</c>, <c>motivoCierre</c> y el tipo de <c>avisoSeguridad</c>.</summary>
internal static class OpenApiSchemas
{
    private static readonly string[] _states = ["abierta", "cerrada"];
    private static readonly string[] _closureReasons = ["request_cancelled", "request_expired", "other_quote_chosen", "dismissed", "service_finished"];

    public static IServiceCollection AddKnockOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(options => options.AddKnockContractConventions().AddSchemaTransformer((schema, context, _) =>
        {
            var type = context.JsonTypeInfo.Type;
            if (type == typeof(ConversacionView) || type == typeof(ConversacionItem))
            {
                // visitaId y motivoCierre se omiten del JSON cuando no hay valor (WhenWritingNull): no son obligatorios en el contrato.
                schema.Required?.Remove("visitaId");
                schema.Required?.Remove("motivoCierre");
                Close(schema, "estado", _states, "Estado de la conversación: abierta o cerrada.");
            }

            if (type == typeof(ConversacionView))
            {
                Close(schema, "motivoCierre", _closureReasons, "Solo con estado cerrada: request_cancelled, request_expired, other_quote_chosen, dismissed o service_finished.");
                Describe(schema, "avisoSeguridad", "Texto fijo de seguridad que la app muestra tal cual (cadena no vacía, nunca null).");
            }

            return Task.CompletedTask;
        }));

    private static void Close(OpenApiSchema schema, string property, string[] values, string description)
    {
        if (schema.Properties is { } properties && properties.TryGetValue(property, out var value) && value is OpenApiSchema target)
        {
            target.Enum = [.. values.Select(item => (JsonNode)JsonValue.Create(item))];
            target.Type = JsonSchemaType.String;
            target.Description = description;
        }
    }

    private static void Describe(OpenApiSchema schema, string property, string description)
    {
        if (schema.Properties is { } properties && properties.TryGetValue(property, out var value) && value is OpenApiSchema target)
        {
            target.Description = description;
        }
    }
}
