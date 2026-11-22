namespace Knock.Market.Api.Modules.Requests.Domain;

/// <summary>Identificador de una foto en el cuerpo de las solicitudes y de las visitas: el <c>archivoId</c> del módulo de archivos (UUID v7, 32 hexadecimales sin guiones, spec 058 R-01).</summary>
internal static class PhotoIds
{
    public static bool TryParse(string? value, out Guid fileId)
    {
        fileId = Guid.Empty;
        return value is { Length: 32 } && Guid.TryParseExact(value, "N", out fileId);
    }

    /// <summary>Forma canónica (minúscula) con la que se guarda y se devuelve el identificador.</summary>
    public static string Canonical(Guid fileId) => fileId.ToString("N");
}
