using System.IO.Compression;
using System.Text;
using Knock.Identity.Api.Modules.Account.Domain;

namespace Knock.Identity.Api.Modules.Account.Services;

/// <summary>
/// Contenedor privado <c>exportaciones</c> de la cuenta de almacenamiento privada (spec 039, R-22; ciclo de vida de 7 días en Terraform). Detrás de una interfaz para que el manejador no conozca
/// el proveedor: la implementación de Azure Blob con enlaces de delegación de usuario llega con el recurso (<c>bloqueo: costo</c> hasta que José autorice el despliegue). Mientras no exista,
/// <see cref="UnavailableExportStorage"/> falla cerrado: la tarea se reintenta y nunca se finge que el archivo existe.
/// </summary>
internal interface IExportStorage
{
    /// <summary>Guarda el ZIP con ese nombre, reemplazando uno anterior (la tarea es idempotente).</summary>
    Task SaveAsync(string name, byte[] zip, CancellationToken cancellationToken);

    /// <summary>Enlace firmado de solo lectura del archivo, válido el tiempo dado.</summary>
    Task<Uri> CreateLinkAsync(string name, TimeSpan validFor, CancellationToken cancellationToken);
}

/// <summary>Aviso a la persona de que su exportación está lista, con el enlace (spec 037: avisos dentro de la app). Nunca se registra el enlace.</summary>
internal interface IExportNotice
{
    Task NotifyReadyAsync(string userId, Uri link, CancellationToken cancellationToken);
}

/// <summary>Sustituto mientras no existe el contenedor: falla cerrado.</summary>
internal sealed class UnavailableExportStorage : IExportStorage
{
    public Task SaveAsync(string name, byte[] zip, CancellationToken cancellationToken) => throw Unavailable();

    public Task<Uri> CreateLinkAsync(string name, TimeSpan validFor, CancellationToken cancellationToken) => throw Unavailable();

    private static InvalidOperationException Unavailable() => new("The export container is not configured.");
}

/// <summary>Sustituto mientras no existe el aviso de exportación lista hacia Mensajería: falla cerrado.</summary>
internal sealed class UnavailableExportNotice : IExportNotice
{
    public Task NotifyReadyAsync(string userId, Uri link, CancellationToken cancellationToken) => throw new InvalidOperationException("The export notice is not configured.");
}

/// <summary>Un archivo JSON por servicio dentro del ZIP (spec 039, R-22): <c>identidad.json</c> y <c>mercado.json</c>.</summary>
internal static class ExportArchive
{
    public const string IdentityFile = "identidad.json";
    public const string MarketFile = "mercado.json";

    public static byte[] Build(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, json) in files)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                using var stream = entry.Open();
                stream.Write(Encoding.UTF8.GetBytes(json));
            }
        }

        return buffer.ToArray();
    }

    /// <summary>Nombre del blob: el identificador interno y el instante de la solicitud, sin datos personales.</summary>
    public static string BlobName(string businessKey) => $"{businessKey.Replace(':', '-')}.zip";
}

/// <summary>Datos de la cuenta que entran en <c>identidad.json</c> (spec 039, R-22): lo que la persona dio o aceptó. Nunca el HMAC ni el celular cifrado.</summary>
internal sealed record AccountExportRecord(string Sub, string State, bool IsTechnician, string? Name, string? Colonia, string? PostalCode, DateTime? CreatedAt, IReadOnlyList<StoredConsent> Consents);
