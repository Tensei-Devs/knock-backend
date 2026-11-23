using System.Text.Json.Serialization;

namespace Knock.Market.Api.Modules.Media.Dtos;

/// <summary>Cuerpo de <c>POST /api/v1/imagenes/subidas</c> (spec 032, R-04). Los campos son opcionales para que la validación responda 422 con <c>errors</c> por campo.</summary>
internal sealed record PedirSubidaImagenRequest(
    [property: JsonPropertyName("proposito")] string? Proposito,
    [property: JsonPropertyName("tipoContenido")] string? TipoContenido,
    [property: JsonPropertyName("tamanoBytes")] long? TamanoBytes);

/// <summary>Respuesta 201 de la subida (R-05): <c>urlSubida</c> es una SAS de escritura de un solo blob y no debe registrarse ni cachearse.</summary>
internal sealed record SubidaImagenResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("urlSubida")] string UrlSubida,
    [property: JsonPropertyName("metodo")] string Metodo,
    [property: JsonPropertyName("encabezados")] IReadOnlyDictionary<string, string> Encabezados,
    [property: JsonPropertyName("venceEn")] DateTimeOffset VenceEn,
    [property: JsonPropertyName("confirmarAntesDe")] DateTimeOffset ConfirmarAntesDe);

/// <summary>Respuesta 200 de la confirmación (R-12).</summary>
internal sealed record ImagenConfirmadaResponse(
    [property: JsonPropertyName("id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Id,
    [property: JsonPropertyName("url"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Url,
    [property: JsonPropertyName("ancho")] int Ancho,
    [property: JsonPropertyName("alto")] int Alto,
    [property: JsonPropertyName("bytes")] int Bytes);
