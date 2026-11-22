namespace Knock.Market.Api.Modules.Requests.Domain;

/// <summary>Decisiones puras para los cierres de la spec 029.</summary>
internal static class TransicionesDeSolicitud
{
    /// <summary>Motivos que el cliente puede dar al cancelar (spec 029, R-02).</summary>
    public static readonly IReadOnlyList<string> CancellationReasons = ["no_longer_needed", "solved_elsewhere", "takes_too_long", "other"];

    /// <summary>Solicitudes cerradas sin cita: solo su dueño las ve (spec 029, R-13).</summary>
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
