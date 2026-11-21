using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Modules.Catalog.Dtos;

/// <summary>Un oficio pedido en «Otro oficio», agrupado por su forma normalizada, con cuántas personas lo pidieron (spec 045, OFP-02). Nunca lleva datos de quien lo escribió.</summary>
internal sealed record TradeRequestGroup(string Texto, long Cantidad);

/// <summary>Una página de oficios pedidos; <c>NextCursor</c> falta en la última.</summary>
internal sealed record TradeRequestPage(
    IReadOnlyList<TradeRequestGroup> Items,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? NextCursor);

/// <summary>Cuántas personas esperan en un código postal (spec 045, LIS-02). Solo conteos: nunca un celular, completo o enmascarado (R-22).</summary>
internal sealed record WaitlistZone(string CodigoPostal, long Cantidad);

/// <summary>Una página de la lista de espera por zona; <c>NextCursor</c> falta en la última.</summary>
internal sealed record WaitlistZonePage(
    IReadOnlyList<WaitlistZone> Items,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? NextCursor);

/// <summary>Parámetros de las listas del panel: <c>limite</c> de 1 a 50 (20 por omisión) y el cursor de la página anterior.</summary>
internal sealed class AdminPageQuery
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;

    [FromQuery(Name = "cursor")]
    [Description("Opaque cursor returned as nextCursor by the previous page.")]
    public string? Cursor { get; set; }

    [FromQuery(Name = "limite")]
    [Description("Page size, 1 to 50. Defaults to 20.")]
    public int Limite { get; set; } = DefaultLimit;
}
