namespace Knock.ContractTest;

/// <summary>Spec 064 (R-09, CA-09): la ruta «el técnico no llegó» declara 200, 400, 401, 404, 409 y 429 (enmienda 2026-11-24: 400 en lugar de 422; la base de los controladores sigue declarando también el 422) como ProblemDetails en el contrato de Mercado.</summary>
public sealed class Spec064ContractTests
{
    [Fact]
    public void Ruta_Declara_Sus_Respuestas_CA_064_09()
    {
        using var document = OpenApiDocs.Load("openapi.v1.json");
        var operation = document.RootElement.GetProperty("paths").GetProperty("/api/v{version}/visitas/{id}/tecnico-ausente").GetProperty("post");
        var responses = operation.GetProperty("responses");

        foreach (var code in new[] { "200", "400", "401", "404", "409", "429" })
        {
            Assert.True(responses.TryGetProperty(code, out _), $"POST tecnico-ausente debe declarar {code}.");
        }

        foreach (var code in new[] { "400", "401", "404", "409", "429" })
        {
            Assert.True(responses.GetProperty(code).GetProperty("content").TryGetProperty("application/problem+json", out _), $"{code} debe ser ProblemDetails.");
        }
    }
}
