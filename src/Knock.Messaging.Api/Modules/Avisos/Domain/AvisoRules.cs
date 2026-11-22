using System.Globalization;
using System.Text.RegularExpressions;

namespace Knock.Messaging.Api.Modules.Avisos.Domain;

internal enum WindowDecision
{
    Send,
    Suppress,
}

/// <summary>Reglas puras de la spec 037: carga v1 cerrada (R-04), solo datos (R-05), clave de deduplicación (R-06) y ventana de mensaje nuevo (R-07).</summary>
internal static partial class AvisoRules
{
    public static readonly TimeSpan MessageWindow = TimeSpan.FromMinutes(5);

    public const string NewMessageEvent = "mensaje_nuevo";

    private static readonly Dictionary<string, string> _typeByEvent = new(StringComparer.Ordinal)
    {
        ["solicitud_nueva"] = "cotizaciones",
        ["solicitud_sin_tecnicos"] = "cotizaciones",
        ["cotizacion_nueva"] = "cotizaciones",
        ["cotizacion_aceptada"] = "visitas",
        ["solicitud_cancelada"] = "cotizaciones",
        ["solicitud_vencida"] = "cotizaciones",
        ["cotizacion_vencida"] = "cotizaciones",
        [NewMessageEvent] = "mensajes",
    };

    /// <summary>Los campos que la carga v1 puede llevar y ningún otro (R-04).</summary>
    public static readonly IReadOnlyList<string> PayloadFields = ["v", "tipo", "evento", "recurso", "aviso", "conversacion"];

    public static bool TryGetType(string evento, out string tipo) => _typeByEvent.TryGetValue(evento ?? "", out tipo!);

    public static bool IsValidResource(string? recurso) => recurso is not null && ResourcePattern().IsMatch(recurso);

    /// <summary>Arma la carga v1 como datos planos de FCM (valores de texto). Devuelve null si el evento o el recurso no son válidos; nunca agrega otros campos (R-04, R-05).</summary>
    public static IReadOnlyDictionary<string, string>? BuildPayload(string evento, string? recurso, string? conversacion, Guid aviso)
    {
        if (!TryGetType(evento, out var tipo) || !IsValidResource(recurso))
        {
            return null;
        }

        var isMessage = evento == NewMessageEvent;
        if (isMessage && !IsValidResource(conversacion))
        {
            return null;
        }

        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["v"] = "1",
            ["tipo"] = tipo,
            ["evento"] = evento,
            ["recurso"] = recurso!,
            ["aviso"] = aviso.ToString("D", CultureInfo.InvariantCulture),
        };
        if (isMessage)
        {
            payload["conversacion"] = conversacion!;
        }

        return payload;
    }

    /// <summary>Clave única de envío: el mismo evento para la misma persona se envía una vez; eventos distintos sobre el mismo recurso, ambos (R-06).</summary>
    public static string DeduplicationKey(string eventoId, string destinatario) => $"{eventoId}|{destinatario}";

    /// <summary>Primer aviso de la ventana se envía; los siguientes dentro de los 5 minutos se agrupan (R-07).</summary>
    public static WindowDecision DecideWindow(DateTime? windowStartUtc, DateTime nowUtc) =>
        windowStartUtc is null || nowUtc - windowStartUtc.Value >= MessageWindow ? WindowDecision.Send : WindowDecision.Suppress;

    /// <summary>Al cerrar la ventana se envía un único aviso más si hubo mensajes que se agruparon (R-07).</summary>
    public static bool ShouldFlushWindow(DateTime windowStartUtc, int suppressedCount, DateTime nowUtc) =>
        suppressedCount > 0 && nowUtc - windowStartUtc >= MessageWindow;

    [GeneratedRegex("^[0-9a-f]{24}$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ResourcePattern();
}
