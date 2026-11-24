namespace Knock.ContractTest;

/// <summary>
/// Spec 058 (R-14, R-18, CA-21, CA-23): el contrato de Mercado no expone rutas ni cuerpos del registro interno de archivos y el campo <c>fotos</c> de <c>POST /solicitudes</c> documenta el identificador de 32 hexadecimales.
/// La comparación contra el contrato anterior la hace <c>oasdiff</c> en el CI; aquí se prueba lo que la spec fija.
/// </summary>
public sealed class Spec058ContractTests
{
    [Fact]
    public void Contrato_Http_No_Expone_El_Registro_Interno_Ni_Los_Contenedores_Retirados_CA_058_21()
    {
        using var document = OpenApiDocs.Load("openapi.v1.json");
        var text = document.RootElement.GetRawText();

        Assert.DoesNotContain("registros-temporales", text, StringComparison.Ordinal);
        Assert.DoesNotContain("blobKey", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("propietarioId", text, StringComparison.Ordinal);
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            Assert.DoesNotContain("/archivos", path.Name, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Fotos_De_Solicitud_Documenta_32_Hexadecimales_Y_No_24_CA_058_23()
    {
        using var document = OpenApiDocs.Load("openapi.v1.json");
        var fotos = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("CreateRequestBody").GetProperty("properties").GetProperty("fotos");
        var description = fotos.GetProperty("description").GetString();

        Assert.Contains("32 hexadecimales", description, StringComparison.Ordinal);
        Assert.DoesNotContain("24 hexadecimales", description, StringComparison.Ordinal);
    }
}
