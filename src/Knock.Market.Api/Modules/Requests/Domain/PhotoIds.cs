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

/// <summary>Valores del <c>uso</c> de una foto privada que <c>IMediaApi</c> entiende (spec 058): la de una solicitud y la de un trabajo terminado.</summary>
internal static class PhotoUses
{
    public const string Request = "solicitud";
    public const string Job = "trabajo";
}
