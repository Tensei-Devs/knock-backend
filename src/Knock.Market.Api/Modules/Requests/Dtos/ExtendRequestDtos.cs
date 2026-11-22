namespace Knock.Market.Api.Modules.Requests.Dtos;

/// <summary>Ruta de «Pedir también a otros»: el identificador de la solicitud (spec 048, R-02).</summary>
internal sealed class ExtendRequestRoute
{
    [Microsoft.AspNetCore.Mvc.FromRoute(Name = "id")]
    public string? Id { get; set; }
}

/// <summary>Respuesta de «Pedir también a otros» (spec 048, SOL-05): la solicitud y que la oleada siguiente quedó pedida.</summary>
internal sealed record ExtendRequestResponse(string Id, string Estado);
