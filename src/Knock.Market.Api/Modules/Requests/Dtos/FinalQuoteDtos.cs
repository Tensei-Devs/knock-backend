using System.Text.Json.Serialization;

namespace Knock.Market.Api.Modules.Requests.Dtos;

/// <summary>Cuerpo de <c>POST /visitas/{id}/cotizacion-final</c>: precio entero en pesos y descripción. No acepta identificadores de personas (R-18).</summary>
internal sealed record FinalQuoteBody(int? Precio, string? Descripcion)
{
    /// <summary>Tamaño máximo del cuerpo de las rutas de la spec 049: 8 KB (spec 009).</summary>
    public const int MaxBodyBytes = 8192;
}

/// <summary>Cuerpo de las decisiones del cliente (<c>aprueba</c> o <c>rechaza</c>).</summary>
internal sealed record DecisionBody(string? Decision);

/// <summary>Cuerpo de <c>POST /cotizaciones/{id}/ajuste-precio</c>.</summary>
internal sealed record AdjustmentBody(int? MontoNuevo, string? Motivo);

/// <summary>Cotización final creada (201): sin el texto que escribió el técnico.</summary>
internal sealed record FinalQuoteResponse(string VisitaId, int Precio, string Estado, DateTimeOffset VenceEn);

/// <summary>Resultado de decidir la cotización final: <c>resultado</c> solo aparece al rechazar (<c>solo_visita</c>).</summary>
internal sealed record FinalQuoteDecisionResponse(string VisitaId, string Estado, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Resultado);

/// <summary>Ajuste de precio propuesto (201).</summary>
internal sealed record AdjustmentResponse(string Id, string CotizacionId, string VisitaId, int MontoNuevo, string Estado, DateTimeOffset VenceEn);

/// <summary>Resultado de decidir un ajuste: <c>topeNuevo</c> solo si se aprobó.</summary>
internal sealed record AdjustmentDecisionResponse(string Id, string Estado, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? TopeNuevo);

/// <summary>Ruta de las lecturas de la visita: el identificador (spec 063, R-06). Un valor que no es <c>ObjectId</c> lo rechaza el controller con 400 <c>invalid_id</c> sin repetirlo (spec 002, R-10; enmienda 2026-11-24).</summary>
internal sealed class VisitRoute
{
    [Microsoft.AspNetCore.Mvc.FromRoute(Name = "id")]
    public string? Id { get; set; }
}

/// <summary>Cotización final de la visita tal como la lee el cliente o el técnico (spec 063, R-02): sin identificadores de personas. <c>decididaEn</c> solo si ya se decidió.</summary>
internal sealed record FinalQuoteReadResponse(string VisitaId, int Precio, string Descripcion, string Estado, DateTimeOffset VenceEn, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? DecididaEn);

/// <summary>Un ajuste de precio pendiente y vigente (spec 063, R-04).</summary>
internal sealed record PendingAdjustmentItem(string Id, string CotizacionId, int MontoNuevo, string Motivo, string Estado, DateTimeOffset VenceEn);

/// <summary>Ajustes pendientes de la visita; vacío si no hay (spec 063, R-04).</summary>
internal sealed record PendingAdjustmentsResponse(IReadOnlyList<PendingAdjustmentItem> Items);
