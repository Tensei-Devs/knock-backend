namespace Knock.Market.Api.Modules.Requests.Dtos;

/// <summary>Consulta de la referencia de mercado (spec 048, COT-03). <c>tipoTrabajo</c> es el tipo de precio de la cotización (<c>fijo</c>, <c>rango</c> o <c>visita</c>).</summary>
internal sealed class MarketReferenceQuery
{
    public string? Oficio { get; set; }
    public string? TipoTrabajo { get; set; }
}

/// <summary>
/// Rango de lo cobrado por otros técnicos (spec 048, R-12). <c>origen</c> es <c>mercado</c> (mismo oficio y tipo), <c>categoria</c> (el oficio completo) o <c>sin_datos</c>
/// (sin las 10 cotizaciones necesarias, con los tres montos vacíos). Nunca trae precios individuales ni identidades.
/// </summary>
internal sealed record MarketReference(string Origen, int? P25, int? Mediana, int? P75);
