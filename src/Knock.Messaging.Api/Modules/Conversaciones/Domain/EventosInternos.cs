namespace Knock.Messaging.Api.Modules.Conversaciones.Domain;

/// <summary>Catálogo cerrado de eventos que Mercado puede entregar a <c>POST /api/v1/interno/eventos</c> de Mensajería (spec 054, R-03).</summary>
internal static class EventosInternos
{
    /// <summary>Se eligió una cotización (spec 048, CA-21): las demás conversaciones de la solicitud se cierran.</summary>
    public const string QuoteChosen = "CotizacionElegida";

    /// <summary>Motivo de cierre de una conversación cuando la solicitud eligió a otro técnico (spec 030, R-03).</summary>
    public const string OtherQuoteChosenReason = "other_quote_chosen";

    public static bool IsKnownType(string? tipo) => tipo == QuoteChosen;
}
