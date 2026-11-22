namespace Knock.Market.Api.Modules.Media.Domain;

/// <summary>Motivo cerrado de un enlace privado de lectura (spec 032, R-16).</summary>
internal enum MotivoAcceso { VerificacionRevision, VerificacionDueno, FotoSolicitud, FotoTrabajo }

internal static class MotivoAccesoExtensions
{
    public static string ToContractValue(this MotivoAcceso motivo) => motivo switch
    {
        MotivoAcceso.VerificacionRevision => "verificacion_revision",
        MotivoAcceso.VerificacionDueno => "verificacion_dueno",
        MotivoAcceso.FotoSolicitud => "foto_solicitud",
        MotivoAcceso.FotoTrabajo => "foto_trabajo",
        _ => throw new ArgumentOutOfRangeException(nameof(motivo)),
    };
}

/// <summary>Blob de la cuenta privada que se puede entregar con un enlace firmado. Solo contenedores de verificación; nunca la cuarentena ni la cuenta pública.</summary>
internal sealed record BlobPrivado(string Contenedor, string Nombre)
{
    public const string ContenedorVerificaciones = "verificaciones";

    /// <summary>Contenedor de las fotos privadas ya confirmadas (<c>Media:FilesContainer</c>, por omisión <c>archivos</c>); solo se enlaza su versión reducida (spec 020, spec 058).</summary>
    public const string ContenedorArchivos = "archivos";

    public static readonly IReadOnlySet<string> ContenedoresPermitidos = new HashSet<string>(StringComparer.Ordinal) { ContenedorVerificaciones, ContenedorArchivos };
}

/// <summary>Documento de <c>accesos_privados</c>: motivo, identificador interno del blob, huella HMAC del <c>sub</c> y fecha. Nunca guarda el <c>sub</c> ni la SAS.</summary>
internal sealed record AccesoPrivado(string Motivo, string BlobId, string SubHmac, DateTime EmitidoEn);
