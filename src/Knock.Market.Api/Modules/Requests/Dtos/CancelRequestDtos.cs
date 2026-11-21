namespace Knock.Market.Api.Modules.Requests.Dtos;

/// <summary>Cuerpo opcional de <c>POST /solicitudes/{id}/cancelar</c> (spec 029, R-02): <c>Motivo</c> es uno de <c>no_longer_needed</c>, <c>solved_elsewhere</c>, <c>takes_too_long</c> u <c>other</c>.</summary>
internal sealed record CancelRequestBody(string? Motivo)
{
    /// <summary>Tamaño máximo del cuerpo: 8 KB (spec 002, límite común de los cuerpos JSON).</summary>
    public const int MaxBodyBytes = 8192;
}

/// <summary>Respuesta de la cancelación (spec 029, R-03): la misma si la solicitud ya estaba cancelada (R-04).</summary>
internal sealed record CancelRequestResponse(string Id, string Estado, DateTimeOffset CanceladaEn, string? Motivo);
