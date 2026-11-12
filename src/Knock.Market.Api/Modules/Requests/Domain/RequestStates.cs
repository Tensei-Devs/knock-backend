namespace Knock.Market.Api.Modules.Requests.Domain;

/// <summary>Estados de la solicitud (spec 020, §6). El nombre guardado en Mongo es el de <see cref="RequestStates.ToStored(RequestState)"/>.</summary>
internal enum RequestState
{
    Enviada,
    Cotizando,
    CitaConfirmada,
    Terminada,
    Calificada,
    Cancelada,
    Vencida,
}

/// <summary>Estados de la cotización (spec 020, §6).</summary>
internal enum QuoteState
{
    Enviada,
    Aceptada,
    Rechazada,
    Retirada,
}

/// <summary>Tipo de precio de una cotización.</summary>
internal enum PriceType
{
    Fijo,
    Rango,
    Visita,
}

/// <summary>Conversión entre los estados del dominio y los nombres guardados en las colecciones.</summary>
internal static class RequestStates
{
    public static string ToStored(RequestState state) => state switch
    {
        RequestState.Enviada => "enviada",
        RequestState.Cotizando => "cotizando",
        RequestState.CitaConfirmada => "cita_confirmada",
        RequestState.Terminada => "terminada",
        RequestState.Calificada => "calificada",
        RequestState.Cancelada => "cancelada",
        RequestState.Vencida => "vencida",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown request state."),
    };

    public static string ToStored(QuoteState state) => state switch
    {
        QuoteState.Enviada => "enviada",
        QuoteState.Aceptada => "aceptada",
        QuoteState.Rechazada => "rechazada",
        QuoteState.Retirada => "retirada",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown quote state."),
    };

    public static RequestState ParseRequest(string stored) => stored switch
    {
        "enviada" => RequestState.Enviada,
        "cotizando" => RequestState.Cotizando,
        "cita_confirmada" => RequestState.CitaConfirmada,
        "terminada" => RequestState.Terminada,
        "calificada" => RequestState.Calificada,
        "cancelada" => RequestState.Cancelada,
        "vencida" => RequestState.Vencida,
        _ => throw new ArgumentOutOfRangeException(nameof(stored), "Unknown stored request state."),
    };

    public static QuoteState ParseQuote(string stored) => stored switch
    {
        "enviada" => QuoteState.Enviada,
        "aceptada" => QuoteState.Aceptada,
        "rechazada" => QuoteState.Rechazada,
        "retirada" => QuoteState.Retirada,
        _ => throw new ArgumentOutOfRangeException(nameof(stored), "Unknown stored quote state."),
    };
}
