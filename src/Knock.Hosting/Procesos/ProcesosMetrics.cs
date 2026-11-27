using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Knock.Hosting.Procesos;

/// <summary>
/// Métricas y registros de la bandeja de salida y de las tareas programadas (spec 054, R-13), comunes a Identidad y Mercado. Solo llevan
/// proceso, tipo, identificador, intento y resultado: nunca la carga, la clave de negocio ni datos personales. El identificador sale
/// siempre por <see cref="Identificador"/>, porque varios <c>_id</c> de la bandeja llevan el <c>sub</c>, el <c>sid</c> o el id de la persona.
/// </summary>
public static partial class ProcesosMetrics
{
    public const string MeterName = "Knock.Procesos";
    public const string AttemptsInstrument = "knock.procesos.intentos";
    public const string AlertsInstrument = "knock.procesos.alertas";

    public const string Outbox = "outbox";
    public const string Tareas = "tareas_programadas";

    public const string Entregado = "entregado";
    public const string Hecha = "hecha";
    public const string Reintento = "reintento";
    public const string Fallido = "fallido";
    public const string Fallida = "fallida";
    public const string ReservaPerdida = "reserva_perdida";
    public const string Atrasado = "atrasado";

    private static readonly Meter _meter = new(MeterName);
    private static readonly Counter<long> _attempts = _meter.CreateCounter<long>(AttemptsInstrument);
    private static readonly Counter<long> _alerts = _meter.CreateCounter<long>(AlertsInstrument);

    /// <summary>
    /// El identificador que se puede registrar y etiquetar. Un ObjectId (24 hexadecimales, el <c>_id</c> de una tarea) sale igual; cualquier
    /// otro <c>_id</c> sale como los primeros 32 hexadecimales de su SHA-256, que permite buscarlo en la base sin publicar lo que lleva.
    /// </summary>
    public static string Identificador(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.Length == 24 && id.All(char.IsAsciiHexDigit)
            ? id
            : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(id)))[..32];
    }

    /// <summary>Resultado de un intento: sin la reserva no cuenta el desenlace; sin éxito, es reintento salvo que ya no queden intentos.</summary>
    public static string Outcome(bool owned, bool succeeded, bool lastFailure, string succeededAs, string failedAs)
    {
        if (!owned)
        {
            return ReservaPerdida;
        }

        if (succeeded)
        {
            return succeededAs;
        }

        return lastFailure ? failedAs : Reintento;
    }

    /// <summary>Cuenta y registra un intento de entrega o de ejecución.</summary>
    public static void RecordAttempt(ILogger logger, string proceso, string tipo, string id, int intento, string resultado)
    {
        ArgumentNullException.ThrowIfNull(logger);
        var published = Identificador(id);
        _attempts.Add(1, Tags(proceso, tipo, published, intento, resultado));
        Attempt(logger, proceso, tipo, published, intento, resultado);
    }

    /// <summary>Cuenta una observación de un evento o tarea en alerta (<c>fallido</c>, <c>fallida</c> o <c>atrasado</c>).</summary>
    internal static void RecordAlert(AlertaDeProceso alerta) =>
        _alerts.Add(1, Tags(alerta.Proceso, alerta.Tipo, Identificador(alerta.Id), alerta.Intentos, alerta.Resultado));

    private static TagList Tags(string proceso, string tipo, string id, int intento, string resultado) =>
        new() { { "proceso", proceso }, { "tipo", tipo }, { "id", id }, { "intento", intento }, { "resultado", resultado } };

    [LoggerMessage(Level = LogLevel.Information, Message = "Background attempt: {Process} {Type} {Id} attempt {Attempt} result {Result}")]
    private static partial void Attempt(ILogger logger, string process, string type, string id, int attempt, string result);
}
