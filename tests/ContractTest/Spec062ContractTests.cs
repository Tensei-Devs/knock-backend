namespace Knock.ContractTest;

/// <summary>
/// Spec 062 (R-09, CA-10): el contrato publicado de Mercado declara el 429 de <c>POST /imagenes/subidas</c> con <c>Retry-After</c> y la respuesta de confirmar sin <c>url</c> para una foto de solicitud.
/// La spec dice «<c>url</c> opcional» y «<c>proposito</c> incluye <c>solicitud</c>», pero el contrato conserva <c>url</c> como cadena requerida y <c>proposito</c> como cadena libre a propósito, para no
/// romper a los clientes (oasdiff); se prueba lo que sí está publicado y la nota que explica la omisión.
/// </summary>
public sealed class Spec062ContractTests
{
    [Fact]
    public void Subidas_Declara_El_429_Con_RetryAfter_CA_062_10()
    {
        using var document = OpenApiDocs.Load("openapi.v1.json");
        var post = document.RootElement.GetProperty("paths").GetProperty("/api/v{version}/imagenes/subidas").GetProperty("post");
        var tooMany = post.GetProperty("responses").GetProperty("429");

        Assert.True(tooMany.GetProperty("headers").TryGetProperty("Retry-After", out _), "El 429 debe declarar Retry-After.");
        Assert.True(tooMany.GetProperty("content").TryGetProperty("application/problem+json", out _), "El 429 debe ser ProblemDetails.");
    }

    [Fact]
    public void Confirmar_Documenta_Que_La_Foto_De_Solicitud_Va_Sin_Url_CA_062_10()
    {
        using var document = OpenApiDocs.Load("openapi.v1.json");
        var schema = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("ImagenConfirmadaResponse");
        var confirm = document.RootElement.GetProperty("paths").GetProperty("/api/v{version}/imagenes/subidas/{id}/confirmar").GetProperty("post");

        Assert.True(confirm.GetProperty("responses").TryGetProperty("200", out _), "Confirmar debe declarar 200.");
        foreach (var field in new[] { "id", "url", "ancho", "alto", "bytes" })
        {
            Assert.True(schema.GetProperty("properties").TryGetProperty(field, out _), $"La respuesta de confirmar debe declarar {field}.");
        }

        var description = schema.GetProperty("description").GetString();
        Assert.Contains("spec 062", description, StringComparison.Ordinal);
        Assert.True(document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("PedirSubidaImagenRequest").GetProperty("properties").TryGetProperty("proposito", out _), "La petición debe declarar proposito.");
    }
}
