namespace Knock.Market.Api.Modules.Requests.Domain;

/// <summary>Decisiones puras para los cierres de la spec 029.</summary>
internal static class TransicionesDeSolicitud
{
    /// <summary>Motivos que solo escribe el sistema cuando la visita no ocurre (spec 029, enmienda 2026-11-26); la cancelación del cliente no los acepta.</summary>
    public const string VisitCancelledReason = "visit_cancelled";
    public const string VisitNoShowReason = "visit_no_show";

    /// <summary>Motivos que el cliente puede dar al cancelar (spec 029, R-02).</summary>
    public static readonly IReadOnlyList<string> CancellationReasons = ["no_longer_needed", "solved_elsewhere", "takes_too_long", "other"];

    /// <summary>Solicitudes cerradas: las ven su dueño y, si lo hubo, el técnico elegido (spec 029, R-13 y CA-10).</summary>
    public static bool IsClosedWithoutAppointment(RequestState state) => state is RequestState.Cancelada or RequestState.Vencida;

    public static bool CanClose(RequestState current, RequestState destination) =>
        RequestRules.AcceptsQuotes(current) && destination is RequestState.Cancelada or RequestState.Vencida;

    public static string? CancellationError(RequestState current) => current switch
    {
        RequestState.CitaConfirmada or RequestState.Terminada or RequestState.Calificada => "request_with_appointment",
        RequestState.Vencida => RequestErrorCodes.RequestExpiredCode,
        RequestState.Enviada or RequestState.Cotizando or RequestState.Cancelada => null,
        _ => throw new ArgumentOutOfRangeException(nameof(current), current, "Unknown request state."),
    };
}
