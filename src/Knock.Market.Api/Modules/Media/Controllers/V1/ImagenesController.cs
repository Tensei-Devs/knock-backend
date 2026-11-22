using System.Globalization;
using Asp.Versioning;
using Knock.Market.Api.Modules.Media.Domain;
using Knock.Market.Api.Modules.Media.Dtos;
using Knock.Market.Api.Modules.Media.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Knock.Market.Api.Modules.Media.Controllers.V1;

/// <summary>
/// Imágenes públicas (spec 032, R-04 a R-06 y R-15): pedir un enlace de subida y confirmar la subida. No toca Blob ni MongoDB; delega en
/// <see cref="SubidaImagenService"/>. Las respuestas llevan <c>Cache-Control: private, no-store</c> y no se comprimen (la firma de <c>urlSubida</c> es un secreto).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/imagenes/subidas")]
[Authorize]
[Hosting.PrivateNoStore]
internal sealed class ImagenesController(SubidaImagenService service) : ControllerBase
{
    private const string _problemJson = "application/problem+json";
    private const int _longitudId = 32;

    /// <summary>Cuerpo máximo de la petición de enlace: 1 KB (R-04).</summary>
    internal const int MaximoCuerpoBytes = 1024;

    [HttpPost]
    [RequestSizeLimit(MaximoCuerpoBytes)]
    [ProducesResponseType<SubidaImagenResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, _problemJson)]
    public async Task<IActionResult> PedirEnlace([FromBody] PedirSubidaImagenRequest body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var proposito = PropositoImagenExtensions.Parse(body.Proposito) ?? throw new InvalidOperationException("The validator guarantees a valid purpose.");
        var respuesta = await service.PedirEnlaceAsync(Sub(), proposito, body.TipoContenido!, (int)body.TamanoBytes!.Value, cancellationToken);
        if (respuesta.Valor is not { } enlace)
        {
            return Problema(respuesta);
        }

        var cuerpo = new SubidaImagenResponse(enlace.Id, enlace.UrlSubida.AbsoluteUri, enlace.Metodo, enlace.Encabezados, enlace.VenceEn, enlace.ConfirmarAntesDe);
        return StatusCode(StatusCodes.Status201Created, cuerpo);
    }

    [HttpPost("{id}/confirmar")]
    [ProducesResponseType<ImagenConfirmadaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, _problemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, _problemJson)]
    public async Task<IActionResult> Confirmar(string id, CancellationToken cancellationToken)
    {
        if (id is null || id.Length != _longitudId || !id.All(char.IsAsciiHexDigitLower))
        {
            return Regla(StatusCodes.Status400BadRequest, "invalid_id", null);
        }

        var respuesta = await service.ConfirmarAsync(Sub(), id, cancellationToken);
        return respuesta.Valor is { } imagen
            ? Ok(new ImagenConfirmadaResponse(imagen.Url.AbsoluteUri, imagen.Ancho, imagen.Alto, imagen.Bytes))
            : Problema(respuesta);
    }

    private string Sub() => User.FindFirst("sub")?.Value ?? string.Empty;

    private ObjectResult Problema<T>(Respuesta<T> respuesta)
        where T : class
    {
        var (estado, codigo) = respuesta.Resultado switch
        {
            ResultadoSubida.LimitExceeded => (StatusCodes.Status429TooManyRequests, "limit_exceeded"),
            ResultadoSubida.UploadNotFound => (StatusCodes.Status404NotFound, "upload_not_found"),
            ResultadoSubida.UploadExpired => (StatusCodes.Status409Conflict, "upload_expired"),
            ResultadoSubida.FileNotUploaded => (StatusCodes.Status409Conflict, "file_not_uploaded"),
            ResultadoSubida.ImageTooLarge => (StatusCodes.Status422UnprocessableEntity, "image_too_large"),
            ResultadoSubida.FormatNotSupported => (StatusCodes.Status422UnprocessableEntity, CodigosImagen.FormatNotSupportedCode),
            ResultadoSubida.ImageInvalid => (StatusCodes.Status422UnprocessableEntity, CodigosImagen.ImageInvalidCode),
            ResultadoSubida.ImageRejected => (StatusCodes.Status422UnprocessableEntity, "image_rejected"),
            ResultadoSubida.ModerationUnavailable => (StatusCodes.Status503ServiceUnavailable, "moderation_unavailable"),
            ResultadoSubida.ModerationLimit => (StatusCodes.Status429TooManyRequests, "moderation_limit"),
            _ => (StatusCodes.Status503ServiceUnavailable, "storage_unavailable"),
        };
        return Regla(estado, codigo, respuesta.ReintentarEn);
    }

    private ObjectResult Regla(int estado, string codigo, TimeSpan? reintentarEn)
    {
        if (reintentarEn is { } espera)
        {
            Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var problema = ProblemDetailsFactory.CreateProblemDetails(HttpContext, estado, ReasonPhrases.GetReasonPhrase(estado));
        problema.Extensions["code"] = codigo;
        return new ObjectResult(problema) { StatusCode = estado, ContentTypes = { _problemJson } };
    }
}
