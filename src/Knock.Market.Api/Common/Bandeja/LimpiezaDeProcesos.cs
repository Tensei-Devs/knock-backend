namespace Knock.Market.Api.Common.Bandeja;

/// <summary>
/// Limpieza de <c>outbox</c> y <c>tareas_programadas</c> de Mercado (spec 054, R-11 y D-02): al quedar en un estado terminal el documento lleva <c>limpiarEn</c> y el índice TTL de <c>db/054</c> lo borra,
/// a los 14 días lo <c>entregado</c> o <c>hecha</c> y a los 90 lo <c>fallido</c> o <c>fallida</c>.
/// </summary>
internal static class LimpiezaDeProcesos
{
    public const string Field = "limpiarEn";

    private const int _finishedDays = 14;
    private const int _failedDays = 90;

    public static DateTime LimpiarEn(DateTime now, bool failed) => now.AddDays(failed ? _failedDays : _finishedDays);
}
