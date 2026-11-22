using System.Security.Cryptography;
using Knock.Contracts.Moderation;
using Knock.Market.Api.Modules.Media.Data;
using Knock.Market.Api.Modules.Media.Domain;

namespace Knock.Market.Api.Modules.Media.Services;

/// <summary>Resultado de una operación de subida: éxito o el código estable de ProblemDetails que la API responde (spec 032, §5).</summary>
internal enum ResultadoSubida
{
    Ok,
    LimitExceeded,
    UploadNotFound,
    UploadExpired,
    FileNotUploaded,
    ImageTooLarge,
    FormatNotSupported,
    ImageInvalid,
    ImageRejected,
    ModerationUnavailable,
    StorageUnavailable,
}

/// <summary>Enlace de subida emitido: lo que la API devuelve con 201 (R-05).</summary>
internal sealed record EnlaceSubidaEmitido(
    string Id, Uri UrlSubida, string Metodo, IReadOnlyDictionary<string, string> Encabezados, DateTimeOffset VenceEn, DateTimeOffset ConfirmarAntesDe);

/// <summary>Imagen publicada: lo que la confirmación devuelve con 200 (R-12).</summary>
internal sealed record ImagenConfirmada(Uri Url, int Ancho, int Alto, int Bytes);

/// <summary>Resultado con su valor, si lo hay, y el tiempo de espera que acompaña a 429 y 503 (<c>Retry-After</c>).</summary>
internal sealed record Respuesta<T>(ResultadoSubida Resultado, T? Valor, TimeSpan? ReintentarEn = null)
    where T : class
{
    public static Respuesta<T> Ok(T valor) => new(ResultadoSubida.Ok, valor);

    public static Respuesta<T> Falla(ResultadoSubida resultado, TimeSpan? reintentarEn = null) => new(resultado, null, reintentarEn);
}

/// <summary>
/// Orquesta las imágenes públicas (spec 032, plan, paso 4). Pedir enlace: límites, SAS y documento <c>pendiente</c>. Confirmar: dueño, vigencia, propiedades,
/// descarga, procesamiento, moderación con falla cerrada, publicación por huella, limpieza de la cuarentena y estado. Nunca registra SAS, <c>sub</c> ni nombres de blob (R-18).
/// </summary>
internal sealed class SubidaImagenService(
    IAlmacenBlob almacen,
    ISubidasImagenStore subidas,
    LimitesImagen limites,
    ProcesadorImagen procesador,
    IContentModerator moderador,
    ImagenPublicaUrl direcciones,
    TimeProvider reloj)
{
    /// <summary>Espera sugerida cuando la moderación o el almacenamiento no responden.</summary>
    internal static readonly TimeSpan EsperaPorFalla = TimeSpan.FromSeconds(30);

    public async Task<Respuesta<EnlaceSubidaEmitido>> PedirEnlaceAsync(
        string sub, PropositoImagen proposito, string tipoContenido, int tamanoBytes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoContenido);
        var limite = await limites.ConsumirAsync(LimiteImagen.Subidas, sub, cancellationToken);
        if (!limite.Permitido)
        {
            return Respuesta<EnlaceSubidaEmitido>.Falla(ResultadoSubida.LimitExceeded, limite.ReintentarEn);
        }

        var ahora = reloj.GetUtcNow();
        var pendientes = await subidas.CountPendientesVigentesAsync(sub, ahora.UtcDateTime, cancellationToken);
        if (!LimitesImagen.PermiteNuevaSubida(pendientes))
        {
            return Respuesta<EnlaceSubidaEmitido>.Falla(ResultadoSubida.LimitExceeded, TimeSpan.FromMinutes(SubidaImagen.MinutosParaConfirmar));
        }

        var id = Guid.CreateVersion7().ToString("N");
        var clave = AlmacenBlob.NuevoNombreCuarentena();
        EnlaceSubida enlace;
        try
        {
            enlace = await almacen.CrearEnlaceSubidaAsync(clave, tipoContenido, cancellationToken);
        }
        catch (IOException)
        {
            return Respuesta<EnlaceSubidaEmitido>.Falla(ResultadoSubida.StorageUnavailable, EsperaPorFalla);
        }

        var subida = SubidaImagen.Nueva(id, clave, sub, proposito, tipoContenido, tamanoBytes, ahora.UtcDateTime);
        await subidas.InsertAsync(subida, cancellationToken);
        return Respuesta<EnlaceSubidaEmitido>.Ok(new EnlaceSubidaEmitido(
            id, enlace.Url, "PUT", enlace.Encabezados, enlace.VenceEn, new DateTimeOffset(subida.ConfirmarAntesDe, TimeSpan.Zero)));
    }

    public async Task<Respuesta<ImagenConfirmada>> ConfirmarAsync(string sub, string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var limite = await limites.ConsumirAsync(LimiteImagen.Confirmaciones, sub, cancellationToken);
        if (!limite.Permitido)
        {
            return Respuesta<ImagenConfirmada>.Falla(ResultadoSubida.LimitExceeded, limite.ReintentarEn);
        }

        var subida = await subidas.FindOwnedAsync(id, sub, cancellationToken);
        if (subida is null)
        {
            return Respuesta<ImagenConfirmada>.Falla(ResultadoSubida.UploadNotFound);
        }

        var yaResuelta = Resuelta(subida);
        if (yaResuelta is not null)
        {
            return yaResuelta;
        }

        if (reloj.GetUtcNow().UtcDateTime > subida.ConfirmarAntesDe)
        {
            return Respuesta<ImagenConfirmada>.Falla(ResultadoSubida.UploadExpired);
        }

        try
        {
            return await ProcesarAsync(subida, cancellationToken);
        }
        catch (IOException)
        {
            return Respuesta<ImagenConfirmada>.Falla(ResultadoSubida.StorageUnavailable, EsperaPorFalla);
        }
    }

    /// <summary>Una subida ya resuelta responde sin procesar ni moderar otra vez (R-12): publicada devuelve lo mismo; rechazada o inválida, su 422.</summary>
    private static Respuesta<ImagenConfirmada>? Resuelta(SubidaImagen subida) => subida.Estado switch
    {
        EstadoSubida.Publicada when subida.Url is not null && subida.Ancho is { } ancho && subida.Alto is { } alto && subida.Bytes is { } bytes =>
            Respuesta<ImagenConfirmada>.Ok(new ImagenConfirmada(new Uri(subida.Url), ancho, alto, bytes)),
        EstadoSubida.Rechazada => Respuesta<ImagenConfirmada>.Falla(ResultadoSubida.ImageRejected),
        EstadoSubida.Invalida => Respuesta<ImagenConfirmada>.Falla(ResultadoSubida.ImageInvalid),
        _ => null,
    };

    private async Task<Respuesta<ImagenConfirmada>> ProcesarAsync(SubidaImagen subida, CancellationToken cancellationToken)
    {
        var propiedades = await almacen.ObtenerPropiedadesAsync(subida.ClaveBlob, cancellationToken);
        if (propiedades is null)
        {
            return Respuesta<ImagenConfirmada>.Falla(ResultadoSubida.FileNotUploaded);
        }

        if (propiedades.Bytes > MediaRules.MaxFileBytes)
        {
            return await DescartarAsync(subida, ResultadoSubida.ImageTooLarge, cancellationToken);
        }

        var original = await almacen.DescargarAsync(subida.ClaveBlob, propiedades, (int)MediaRules.MaxFileBytes, cancellationToken);
        if (original is null)
        {
            return Respuesta<ImagenConfirmada>.Falla(ResultadoSubida.FileNotUploaded);
        }

        var procesada = await procesador.ProcesarAsync(new ArraySegment<byte>(original), subida.Proposito, cancellationToken);
        if (procesada.Imagen is null)
        {
            var motivo = procesada.Codigo == CodigosImagen.FormatNotSupportedCode ? ResultadoSubida.FormatNotSupported : ResultadoSubida.ImageInvalid;
            return await DescartarAsync(subida, motivo, cancellationToken);
        }

        var moderacion = await moderador.ReviewImageAsync(procesada.Imagen.Bytes, cancellationToken);
        if (moderacion.Outcome == ModerationOutcome.Rejected)
        {
            return await DescartarAsync(subida, ResultadoSubida.ImageRejected, cancellationToken);
        }

        if (moderacion.Outcome != ModerationOutcome.Allowed)
        {
            // Falla cerrada (constitución, regla 13): nada se publica y la subida sigue confirmable hasta confirmarAntesDe.
            return Respuesta<ImagenConfirmada>.Falla(ResultadoSubida.ModerationUnavailable, EsperaPorFalla);
        }

        return await PublicarAsync(subida, procesada.Imagen, cancellationToken);
    }

    private async Task<Respuesta<ImagenConfirmada>> PublicarAsync(SubidaImagen subida, ImagenProcesada imagen, CancellationToken cancellationToken)
    {
        var huella = Convert.ToHexStringLower(SHA256.HashData(imagen.Bytes));
        var publica = direcciones.Build(subida.Proposito, huella);
        await almacen.PublicarAsync($"{subida.Proposito.ToContractValue()}/{huella}.jpg", imagen.Bytes, cancellationToken);
        await almacen.BorrarCuarentenaAsync(subida.ClaveBlob, cancellationToken);
        var url = publica.Url.AbsoluteUri;
        if (!await subidas.MarcarPublicadaAsync(subida.Id, url, imagen.Ancho, imagen.Alto, imagen.Bytes.Length, cancellationToken))
        {
            // Otra confirmación simultánea ya resolvió la subida: se responde lo que quedó guardado (R-12).
            var actual = await subidas.FindOwnedAsync(subida.Id, subida.Sub, cancellationToken);
            return (actual is null ? null : Resuelta(actual)) ?? Respuesta<ImagenConfirmada>.Falla(ResultadoSubida.UploadNotFound);
        }

        return Respuesta<ImagenConfirmada>.Ok(new ImagenConfirmada(publica.Url, imagen.Ancho, imagen.Alto, imagen.Bytes.Length));
    }

    /// <summary>Borra la cuarentena y deja la subida en su estado final; cada rechazo de contenido o formato termina así (R-07, R-08, R-10, R-19).</summary>
    private async Task<Respuesta<ImagenConfirmada>> DescartarAsync(SubidaImagen subida, ResultadoSubida motivo, CancellationToken cancellationToken)
    {
        await almacen.BorrarCuarentenaAsync(subida.ClaveBlob, cancellationToken);
        await subidas.MarcarFinalAsync(subida.Id, motivo == ResultadoSubida.ImageRejected ? EstadoSubida.Rechazada : EstadoSubida.Invalida, cancellationToken);
        return Respuesta<ImagenConfirmada>.Falla(motivo);
    }
}
