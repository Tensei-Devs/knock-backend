using System.Text.Json;

namespace Knock.ContractTest;

/// <summary>Spec 063 (R-08, CA-11): las dos rutas de lectura de la visita declaran 200, 401, 404, 422 y 429 en el contrato de Mercado.</summary>
public sealed class Spec063ContractTests
{
    [Theory]
    [InlineData("/api/v{version}/visitas/{id}/cotizacion-final")]
    [InlineData("/api/v{version}/visitas/{id}/ajustes-pendientes")]
    public void Lecturas_Declaran_Sus_Respuestas_CA_063_11(string path)
    {
        using var document = OpenApiDocs.Load("openapi.v1.json");
        var operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty("get");
        var responses = operation.GetProperty("responses");

        foreach (var code in new[] { "200", "401", "404", "422", "429" })
        {
            Assert.True(responses.TryGetProperty(code, out _), $"GET {path} debe declarar {code}.");
        }

        foreach (var code in new[] { "401", "404", "422", "429" })
        {
            Assert.True(responses.GetProperty(code).GetProperty("content").TryGetProperty("application/problem+json", out _), $"{code} debe ser ProblemDetails.");
        }

        Assert.Equal(JsonValueKind.Object, responses.GetProperty("200").GetProperty("content").ValueKind);
    }
}
