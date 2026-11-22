using System.Security.Cryptography;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Knock.Market.Api.Modules.Media.Domain;
using Knock.Market.Api.Modules.Media.Options;

namespace Knock.Market.Api.Modules.Media.Data;

/// <summary>SAS de escritura de un solo blob: la dirección firmada, su vencimiento y los encabezados que la app debe enviar (spec 032, R-05).</summary>
internal sealed record EnlaceSubida(Uri Url, DateTimeOffset VenceEn, IReadOnlyDictionary<string, string> Encabezados);

/// <summary>Tamaño real y versión (ETag) de un blob, leídos antes de descargarlo (spec 032, R-07).</summary>
internal sealed record PropiedadesBlob(long Bytes, string ETag);

/// <summary>Llave de delegación de usuario con su cuenta, para reutilizarla mientras le quede margen.</summary>
internal sealed record LlaveDelegacion(UserDelegationKey Llave, string Cuenta);

/// <summary>Operaciones de Blob que necesita el servicio de subidas; permite probar el servicio sin almacenamiento.</summary>
internal interface IAlmacenBlob
{
    Task<EnlaceSubida> CrearEnlaceSubidaAsync(string nombreBlob, string tipoContenido, CancellationToken cancellationToken);

    Task<PropiedadesBlob?> ObtenerPropiedadesAsync(string nombreBlob, CancellationToken cancellationToken);

    Task<byte[]?> DescargarAsync(string nombreBlob, PropiedadesBlob esperado, int maximoBytes, CancellationToken cancellationToken);

    Task<bool> PublicarAsync(string ruta, byte[] bytes, CancellationToken cancellationToken);

    Task BorrarCuarentenaAsync(string nombreBlob, CancellationToken cancellationToken);
}

/// <summary>Emisión de SAS de lectura para blobs de la cuenta privada (spec 032, R-16).</summary>
internal interface IAlmacenLectura
{
    Task<EnlaceSubida> CrearEnlaceLecturaAsync(string contenedor, string nombreBlob, CancellationToken cancellationToken);
}

/// <summary>
/// Único punto del módulo <c>Media</c> que habla con Azure Blob para las imágenes públicas (spec 032, plan, paso 3).
/// Usa dos clientes: el de la cuenta privada (cuarentena y llave de delegación) y el de la cuenta pública (publicación).
/// Las SAS son siempre de delegación de usuario, firmadas con la identidad administrada del servicio; nunca de llave de cuenta (R-02).
/// </summary>
internal sealed class AlmacenBlob(BlobServiceClient privado, BlobServiceClient publico, TimeProvider reloj) : IAlmacenBlob, IAlmacenLectura, IDisposable
{
    /// <summary>Vigencia de una SAS de subida o de lectura (R-05, R-16).</summary>
    internal static readonly TimeSpan VigenciaSas = TimeSpan.FromMinutes(5);

    /// <summary>Tolerancia de reloj: la SAS empieza un minuto antes de emitirse (R-05).</summary>
    internal static readonly TimeSpan ToleranciaReloj = TimeSpan.FromMinutes(1);

    /// <summary>Vigencia pedida de la llave de delegación y margen mínimo para reutilizarla.</summary>
    internal static readonly TimeSpan VigenciaLlave = TimeSpan.FromHours(24);
    internal static readonly TimeSpan MargenLlave = TimeSpan.FromHours(1);

    internal const string CacheControlPublico = "public, max-age=31536000, immutable";
    internal const string ContentDispositionPublico = "inline";

    private readonly BlobContainerClient _cuarentena = privado.GetBlobContainerClient(ImagenesOptions.ContenedorCuarentena);
    private readonly BlobContainerClient _publico = publico.GetBlobContainerClient(ImagenesOptions.ContenedorPublico);
    private readonly SemaphoreSlim _bloqueoLlave = new(1, 1);
    private LlaveDelegacion? _llave;

    /// <summary>Nombre de un blob de cuarentena: 128 bits de un generador criptográfico en hexadecimal, sin relación con el id ni la hora (R-05).</summary>
    public static string NuevoNombreCuarentena() => RandomNumberGenerator.GetHexString(32, lowercase: true);

    /// <summary>Emite la SAS <c>cw</c> de un solo blob de cuarentena, solo HTTPS, que vence a los 5 minutos (R-05, CA-01).</summary>
    public Task<EnlaceSubida> CrearEnlaceSubidaAsync(string nombreBlob, string tipoContenido, CancellationToken cancellationToken) =>
        Proteger(() => CrearEnlaceSubidaCore(nombreBlob, tipoContenido, cancellationToken));

    private async Task<EnlaceSubida> CrearEnlaceSubidaCore(string nombreBlob, string tipoContenido, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreBlob);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoContenido);
        var ahora = reloj.GetUtcNow();
        var llave = await ObtenerLlaveAsync(ahora, cancellationToken);
        var url = FirmarBlob(_cuarentena.GetBlobClient(nombreBlob), llave, BlobSasPermissions.Create | BlobSasPermissions.Write, ahora);
        var encabezados = new Dictionary<string, string>
        {
            ["x-ms-blob-type"] = "BlockBlob",
            ["Content-Type"] = tipoContenido,
        };
        return new EnlaceSubida(url, ahora + VigenciaSas, encabezados);
    }

    /// <summary>
    /// SAS de lectura (<c>r</c>) de un solo blob de la cuenta privada, 5 minutos, solo HTTPS, con <c>Cache-Control: no-store</c> y <c>Content-Disposition: inline</c> forzados (R-16).
    /// Nunca firma blobs de <c>imagenes-cuarentena</c> ni de la cuenta pública.
    /// </summary>
    public Task<EnlaceSubida> CrearEnlaceLecturaAsync(string contenedor, string nombreBlob, CancellationToken cancellationToken) =>
        Proteger(() => CrearEnlaceLecturaCore(contenedor, nombreBlob, cancellationToken));

    private async Task<EnlaceSubida> CrearEnlaceLecturaCore(string contenedor, string nombreBlob, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contenedor);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreBlob);
        if (!BlobPrivado.ContenedoresPermitidos.Contains(contenedor))
        {
            throw new ArgumentException("The container is not readable through private links.", nameof(contenedor));
        }

        var ahora = reloj.GetUtcNow();
        var llave = await ObtenerLlaveAsync(ahora, cancellationToken);
        var url = FirmarBlob(privado.GetBlobContainerClient(contenedor).GetBlobClient(nombreBlob), llave, BlobSasPermissions.Read, ahora, forzarSinCache: true);
        return new EnlaceSubida(url, ahora + VigenciaSas, new Dictionary<string, string>());
    }

    /// <summary>Propiedades del blob de cuarentena, o <see langword="null"/> si la persona no subió nada (R-06, R-07).</summary>
    public Task<PropiedadesBlob?> ObtenerPropiedadesAsync(string nombreBlob, CancellationToken cancellationToken) =>
        Proteger(() => ObtenerPropiedadesCore(nombreBlob, cancellationToken));

    private async Task<PropiedadesBlob?> ObtenerPropiedadesCore(string nombreBlob, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreBlob);
        try
        {
            var propiedades = await _cuarentena.GetBlobClient(nombreBlob).GetPropertiesAsync(cancellationToken: cancellationToken);
            return new PropiedadesBlob(propiedades.Value.ContentLength, propiedades.Value.ETag.ToString());
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// Descarga el blob solo si sigue siendo la versión consultada y nunca más de <paramref name="maximoBytes"/> + 1 bytes;
    /// devuelve <see langword="null"/> si cambió o desapareció.
    /// </summary>
    public Task<byte[]?> DescargarAsync(string nombreBlob, PropiedadesBlob esperado, int maximoBytes, CancellationToken cancellationToken) =>
        Proteger(() => DescargarCore(nombreBlob, esperado, maximoBytes, cancellationToken));

    private async Task<byte[]?> DescargarCore(string nombreBlob, PropiedadesBlob esperado, int maximoBytes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreBlob);
        ArgumentNullException.ThrowIfNull(esperado);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximoBytes, 1);
        var capacidad = (int)Math.Min(esperado.Bytes, maximoBytes) + 1;
        var opciones = new BlobDownloadOptions
        {
            Range = new HttpRange(0, capacidad),
            Conditions = new BlobRequestConditions { IfMatch = new ETag(esperado.ETag) },
        };
        try
        {
            var resultado = await _cuarentena.GetBlobClient(nombreBlob).DownloadStreamingAsync(opciones, cancellationToken);
            using var descarga = resultado.Value;
            return await LeerHastaAsync(descarga.Content, capacidad, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status is 404 or 412 or 416)
        {
            return null;
        }
    }

    /// <summary>
    /// Publica los bytes recodificados en <c>imagenes-publicas/{ruta}</c> con caché inmutable (R-11). Si ya existe un blob con ese
    /// nombre no lo reescribe (mismo contenido): devuelve <see langword="false"/> en ese caso y <see langword="true"/> si lo creó.
    /// </summary>
    public Task<bool> PublicarAsync(string ruta, byte[] bytes, CancellationToken cancellationToken) =>
        Proteger(() => PublicarCore(ruta, bytes, cancellationToken));

    private async Task<bool> PublicarCore(string ruta, byte[] bytes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);
        ArgumentNullException.ThrowIfNull(bytes);
        var opciones = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "image/jpeg",
                CacheControl = CacheControlPublico,
                ContentDisposition = ContentDispositionPublico,
            },
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
        };
        try
        {
            await _publico.GetBlobClient(ruta).UploadAsync(BinaryData.FromBytes(bytes), opciones, cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            return false;
        }
    }

    /// <summary>Borra el blob de cuarentena (publicada, rechazada o inválida); no falla si ya no existe (R-19).</summary>
    public Task BorrarCuarentenaAsync(string nombreBlob, CancellationToken cancellationToken) =>
        Proteger(async () =>
        {
            await BorrarCore(nombreBlob, cancellationToken);
            return true;
        });

    private async Task BorrarCore(string nombreBlob, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreBlob);
        await _cuarentena.GetBlobClient(nombreBlob).DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Firma la dirección de un solo blob con la llave de delegación: HTTPS, de un minuto antes a 5 minutos después de <paramref name="ahora"/>.
    /// Es estática y recibe la llave para poder probarse sin red.
    /// </summary>
    internal static Uri FirmarBlob(BlobClient blob, LlaveDelegacion llave, BlobSasPermissions permisos, DateTimeOffset ahora, bool forzarSinCache = false)
    {
        ArgumentNullException.ThrowIfNull(blob);
        ArgumentNullException.ThrowIfNull(llave);
        var sas = new BlobSasBuilder(permisos, ahora + VigenciaSas)
        {
            BlobContainerName = blob.BlobContainerName,
            BlobName = blob.Name,
            Resource = "b",
            Protocol = SasProtocol.Https,
            StartsOn = ahora - ToleranciaReloj,
        };
        if (forzarSinCache)
        {
            sas.CacheControl = "no-store";
            sas.ContentDisposition = ContentDispositionPublico;
        }

        return new BlobUriBuilder(blob.Uri) { Sas = sas.ToSasQueryParameters(llave.Llave, llave.Cuenta) }.ToUri();
    }

    private static async Task<T> Proteger<T>(Func<Task<T>> operacion)
    {
        try
        {
            return await operacion();
        }
        catch (RequestFailedException ex)
        {
            throw new IOException("Blob storage is unavailable.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new IOException("Blob storage is unavailable.", ex);
        }
    }

    private async Task<LlaveDelegacion> ObtenerLlaveAsync(DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        var actual = _llave;
        if (actual is not null && actual.Llave.SignedExpiresOn - ahora > MargenLlave)
        {
            return actual;
        }

        await _bloqueoLlave.WaitAsync(cancellationToken);
        try
        {
            actual = _llave;
            if (actual is not null && actual.Llave.SignedExpiresOn - ahora > MargenLlave)
            {
                return actual;
            }

            var respuesta = await privado.GetUserDelegationKeyAsync(ahora - ToleranciaReloj, ahora + VigenciaLlave, cancellationToken);
            _llave = new LlaveDelegacion(respuesta.Value, privado.AccountName);
            return _llave;
        }
        finally
        {
            _bloqueoLlave.Release();
        }
    }

    public void Dispose() => _bloqueoLlave.Dispose();

    private static async Task<byte[]> LeerHastaAsync(Stream contenido, int capacidad, CancellationToken cancellationToken)
    {
        var buffer = new byte[capacidad];
        var total = 0;
        int leidos;
        while (total < capacidad && (leidos = await contenido.ReadAsync(buffer.AsMemory(total), cancellationToken)) > 0)
        {
            total += leidos;
        }

        return buffer[..total];
    }
}
