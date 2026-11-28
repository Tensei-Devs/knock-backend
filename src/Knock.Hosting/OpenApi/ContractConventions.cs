using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Knock.Hosting.OpenApi;

/// <summary>
/// Spec 034 R-14: cada contrato de servicio declara los campos de error comunes. <c>ProblemDetails</c> con <c>code</c>, <c>correlationId</c> y
/// <c>traceId</c>; el encabezado <c>X-Correlation-Id</c> en cada respuesta; <c>Retry-After</c> en 429 y 503. Lo registra cada servicio con
/// <see cref="AddKnockContractConventions"/> para que el contrato generado y el real no se separen.
/// </summary>
public static class ContractConventions
{
    private const string _correlationHeader = "X-Correlation-Id";
    private const string _retryHeader = "Retry-After";
    private const string _problemJson = "application/problem+json";

    public static OpenApiOptions AddKnockContractConventions(this OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.AddDocumentTransformer((document, _, _) =>
        {
            DeclareProblemFields(document);
            foreach (var response in Responses(document))
            {
                NormalizeErrorContent(response.Key, response.Value);
                response.Value.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.OrdinalIgnoreCase);
                response.Value.Headers[_correlationHeader] = Header("Identificador de correlación de la petición (se reenvía o se genera en el gateway).", JsonSchemaType.String);
                if (response.Key is "429" or "503")
                {
                    response.Value.Headers[_retryHeader] = Header("Segundos que el cliente debe esperar antes de reintentar.", JsonSchemaType.Integer);
                }
            }

            return Task.CompletedTask;
        });
    }

    /// <summary>Cada error con cuerpo es <c>application/problem+json</c> (así responde el host); se declara una sola vez aquí y no atributo por atributo.</summary>
    private static void NormalizeErrorContent(string code, OpenApiResponse response)
    {
        if (!int.TryParse(code, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var status)
            || status < 400
            || response.Content is not { Count: > 0 } content
            || content.Keys.SequenceEqual([_problemJson], StringComparer.Ordinal))
        {
            return;
        }

        var media = content.TryGetValue(_problemJson, out var existing) ? existing : content.Values.First();
        response.Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal) { [_problemJson] = media };
    }

    private static void DeclareProblemFields(OpenApiDocument document)
    {
        if (document.Components?.Schemas is not { } schemas
            || !schemas.TryGetValue("ProblemDetails", out var schema)
            || schema is not OpenApiSchema problem)
        {
            return;
        }

        problem.Properties ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        problem.Properties["code"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Description = "Código estable del error para que el cliente decida qué mostrar." };
        problem.Properties["correlationId"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Description = "Mismo valor que X-Correlation-Id." };
        problem.Properties["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Description = "Identificador de traza para soporte." };
    }

    private static IEnumerable<KeyValuePair<string, OpenApiResponse>> Responses(OpenApiDocument document)
    {
        foreach (var path in document.Paths.Values)
        {
            foreach (var operation in path.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
            {
                foreach (var response in operation.Responses ?? [])
                {
                    if (response.Value is OpenApiResponse concrete)
                    {
                        yield return new(response.Key, concrete);
                    }
                }
            }
        }
    }

    private static OpenApiHeader Header(string description, JsonSchemaType type) =>
        new() { Description = description, Schema = new OpenApiSchema { Type = type } };
}
