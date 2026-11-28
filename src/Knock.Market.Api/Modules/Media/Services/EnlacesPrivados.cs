using System.Security.Cryptography;
using System.Text;
using Knock.Market.Api.Modules.Media.Data;
using Knock.Market.Api.Modules.Media.Domain;
using Knock.Market.Api.Modules.Media.Options;
using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Modules.Media.Services;

/// <summary>Enlace de lectura firmado: dirección y vencimiento.</summary>
internal sealed record EnlaceFirmado(Uri Url, DateTimeOffset VenceEn);

/// <summary>
/// Emisión de enlaces de lectura para blobs privados (spec 032, R-16, CA-20). Solo lo llama código que ya autorizó al solicitante; deja un registro de acceso.
/// </summary>
internal interface IEnlacesPrivados
{
    Task<EnlaceFirmado> CrearLecturaAsync(BlobPrivado blob, MotivoAcceso motivo, string subSolicitante, CancellationToken cancellationToken);
}

/// <summary>HMAC-SHA256 del <c>sub</c> con la llave de búsqueda (spec 009, R-07): el registro identifica a quien pidió sin guardar el <c>sub</c>.</summary>
internal interface IHuellaSub
{
    string Calcular(string sub);
}

internal sealed class HuellaSubHmac(IOptions<ImagenesOptions> opciones) : IHuellaSub
{
    public string Calcular(string sub)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);
        var llave = opciones.Value.LlaveBusqueda;
        if (string.IsNullOrWhiteSpace(llave))
        {
            throw new InvalidOperationException("Imagenes:LlaveBusqueda is not configured.");
        }

        return Convert.ToHexStringLower(HMACSHA256.HashData(Convert.FromBase64String(llave), Encoding.UTF8.GetBytes(sub)));
    }
}

internal sealed class EnlacesPrivados(IAlmacenLectura almacen, IAccesosPrivadosStore accesos, IHuellaSub huella, TimeProvider reloj) : IEnlacesPrivados
{
    public async Task<EnlaceFirmado> CrearLecturaAsync(BlobPrivado blob, MotivoAcceso motivo, string subSolicitante, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(blob);
        ArgumentException.ThrowIfNullOrWhiteSpace(subSolicitante);
        if (!Enum.IsDefined(motivo))
        {
            throw new ArgumentOutOfRangeException(nameof(motivo));
        }

        if (!BlobPrivado.ContenedoresPermitidos.Contains(blob.Contenedor))
        {
            throw new ArgumentException("Only private verification blobs can be linked.", nameof(blob));
        }

        var enlace = await almacen.CrearEnlaceLecturaAsync(blob.Contenedor, blob.Nombre, cancellationToken);
        var blobId = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{blob.Contenedor}/{blob.Nombre}")));
        await accesos.InsertAsync(new AccesoPrivado(motivo.ToContractValue(), blobId, huella.Calcular(subSolicitante), reloj.GetUtcNow().UtcDateTime), cancellationToken);
        return new EnlaceFirmado(enlace.Url, enlace.VenceEn);
    }
}
