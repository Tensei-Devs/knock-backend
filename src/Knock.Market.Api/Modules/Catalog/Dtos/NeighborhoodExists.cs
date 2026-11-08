using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Knock.Market.Api.Modules.Catalog.ModelBinders;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Knock.Market.Api.Modules.Catalog.Dtos;

/// <summary>
/// Query de <c>GET /colonias/existe</c>. Igual que <see cref="NeighborhoodAtPointQuery"/>: un solo valor como <c>string</c> y sin que MVC lo valide
/// (<c>[ValidateNever]</c>); el 422 lo da <c>NeighborhoodExistsQueryValidator</c> y nunca repite el valor recibido.
/// </summary>
internal sealed class NeighborhoodExistsQuery
{
    public const int MaxLength = 80;

    [SingleValueFromQuery("nombre")]
    [BindRequired]
    [MaxLength(MaxLength)]
    [ValidateNever]
    [Description("Name of the colonia. Letters, digits, spaces and . , ' ’ ( ) / # - ; up to 80 characters. Exactly one value. Case and accents are ignored.")]
    public string? Nombre { get; set; }
}

internal sealed record NeighborhoodExistsResponse(bool Existe);
