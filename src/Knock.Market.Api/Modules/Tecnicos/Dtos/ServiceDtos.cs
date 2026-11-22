using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Tecnicos.Dtos;

/// <summary>Un servicio del técnico con su precio de referencia (spec 047). El precio solo orienta: no sustituye a la cotización (R-17).</summary>
internal sealed record TechnicianService(
    string Id,
    string Nombre,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Detalle,
    int PrecioReferencia,
    string Oficio);

/// <summary>Los servicios de un técnico visible (SRV-01); sin paginación, 50 como máximo por técnico.</summary>
internal sealed record TechnicianServiceList(IReadOnlyList<TechnicianService> Items);

/// <summary>Cuerpo de <c>POST /tecnicos/me/servicios</c> (SRV-02, R-06): ningún identificador de persona se acepta (R-18).</summary>
internal sealed record CreateServiceBody(string? Nombre, string? Detalle, int? PrecioReferencia, string? Oficio)
{
    public const int MaxBodyBytes = 2 * 1024;
}

/// <summary>Cuerpo de <c>PUT /tecnicos/me/servicios/{id}</c> (SRV-03, R-13): cambian solo los campos enviados.</summary>
internal sealed record UpdateServiceBody(string? Nombre, string? Detalle, int? PrecioReferencia, string? Oficio)
{
    public const int MaxBodyBytes = 2 * 1024;
}

/// <summary>El servicio de la ruta de <c>PUT</c> y <c>DELETE /tecnicos/me/servicios/{id}</c>.</summary>
internal sealed class ServiceRoute
{
    [FromRoute(Name = "id")]
    public string? Id { get; set; }
}

/// <summary>El técnico de la ruta de <c>GET /tecnicos/{id}/servicios</c>.</summary>
internal sealed class TechnicianServicesRoute
{
    [FromRoute(Name = "id")]
    public string? Id { get; set; }
}
