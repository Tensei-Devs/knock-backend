using System.Text.Json;

namespace Knock.ContractTest.Support;

/// <summary>
/// Validador mínimo de cuerpos JSON contra los esquemas de un contrato OpenAPI 3.1 (spec 034, R-16): tipo (incluido <c>["x","null"]</c>), <c>required</c>,
/// <c>properties</c>, <c>items</c>, <c>enum</c>, <c>$ref</c> y <c>allOf/anyOf/oneOf</c>. Devuelve la lista de problemas con la ruta del valor.
/// </summary>
internal sealed class SchemaValidator(JsonElement contract)
{
    public IReadOnlyList<string> Validate(JsonElement schema, JsonElement value)
    {
        var problems = new List<string>();
        Check(schema, value, "$", problems);
        return problems;
    }

    /// <summary>Todos los <c>$ref</c> internos del contrato apuntan a un esquema que existe.</summary>
    public IEnumerable<string> UnresolvedReferences() => Refs(contract).Where(reference => Resolve(reference) is null);

    private IEnumerable<string> Refs(JsonElement node)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in node.EnumerateObject())
                {
                    if (property.Name == "$ref" && property.Value.ValueKind == JsonValueKind.String)
                    {
                        yield return property.Value.GetString()!;
                    }
                    else
                    {
                        foreach (var inner in Refs(property.Value))
                        {
                            yield return inner;
                        }
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var inner in node.EnumerateArray().SelectMany(Refs))
                {
                    yield return inner;
                }

                break;
        }
    }

    private JsonElement? Resolve(string reference)
    {
        if (!reference.StartsWith("#/", StringComparison.Ordinal))
        {
            return null;
        }

        var current = contract;
        foreach (var part in reference[2..].Split('/'))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current))
            {
                return null;
            }
        }

        return current;
    }

    private void Check(JsonElement schema, JsonElement value, string path, List<string> problems)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var target = Resolve(reference.GetString()!);
            if (target is null)
            {
                problems.Add($"{path}: referencia sin resolver {reference.GetString()}");
                return;
            }

            Check(target.Value, value, path, problems);
            return;
        }

        CheckCombinations(schema, value, path, problems);
        if (schema.TryGetProperty("type", out var type) && !TypeMatches(type, value))
        {
            problems.Add($"{path}: se esperaba {type} y llegó {value.ValueKind}");
            return;
        }

        if (schema.TryGetProperty("enum", out var allowed) && !allowed.EnumerateArray().Any(item => item.ToString() == value.ToString()))
        {
            problems.Add($"{path}: valor fuera del enum");
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            CheckObject(schema, value, path, problems);
        }
        else if (value.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out var items))
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                Check(items, item, $"{path}[{index++}]", problems);
            }
        }
    }

    private void CheckCombinations(JsonElement schema, JsonElement value, string path, List<string> problems)
    {
        if (schema.TryGetProperty("allOf", out var all))
        {
            foreach (var part in all.EnumerateArray())
            {
                Check(part, value, path, problems);
            }
        }

        foreach (var keyword in new[] { "anyOf", "oneOf" })
        {
            if (schema.TryGetProperty(keyword, out var options) && !options.EnumerateArray().Any(option => Matches(option, value)))
            {
                problems.Add($"{path}: no cumple ninguna opción de {keyword}");
            }
        }
    }

    private bool Matches(JsonElement schema, JsonElement value)
    {
        var problems = new List<string>();
        Check(schema, value, "$", problems);
        return problems.Count == 0;
    }

    private void CheckObject(JsonElement schema, JsonElement value, string path, List<string> problems)
    {
        if (schema.TryGetProperty("required", out var required))
        {
            foreach (var name in required.EnumerateArray().Select(item => item.GetString()!).Where(name => !value.TryGetProperty(name, out _)))
            {
                problems.Add($"{path}: falta el campo obligatorio {name}");
            }
        }

        if (!schema.TryGetProperty("properties", out var properties))
        {
            return;
        }

        foreach (var property in properties.EnumerateObject().Where(property => value.TryGetProperty(property.Name, out _)))
        {
            Check(property.Value, value.GetProperty(property.Name), $"{path}.{property.Name}", problems);
        }
    }

    private static bool TypeMatches(JsonElement type, JsonElement value) =>
        type.ValueKind == JsonValueKind.Array
            ? type.EnumerateArray().Any(item => TypeMatches(item, value))
            : type.GetString() switch
            {
                "object" => value.ValueKind == JsonValueKind.Object,
                "array" => value.ValueKind == JsonValueKind.Array,
                "string" => value.ValueKind == JsonValueKind.String,
                "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "null" => value.ValueKind == JsonValueKind.Null,
                "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
                "number" => value.ValueKind == JsonValueKind.Number,
                _ => true,
            };
}
