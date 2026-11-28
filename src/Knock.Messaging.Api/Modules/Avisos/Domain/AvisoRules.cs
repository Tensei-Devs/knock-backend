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
        ["visita_afectada"] = "visitas",
        [ReportDecidedEvent] = "sistema",
        [ReviewHiddenEvent] = "sistema",
        [TechnicianAbsentEvent] = "visitas",
        ["solicitud_cancelada"] = "cotizaciones",
        ["solicitud_vencida"] = "cotizaciones",
        ["cotizacion_vencida"] = "cotizaciones",
        [NewMessageEvent] = "mensajes",
        [ExportReadyEvent] = "sistema",
        ["reprogramacion_propuesta"] = "visitas",
        ["visita_reprogramada"] = "visitas",
        ["reprogramacion_sin_cambio"] = "visitas",
        ["visita_cancelada"] = "visitas",
        ["cliente_ausente"] = "visitas",
        ["visita_terminada"] = "visitas",
        ["cotizacion_final_nueva"] = "visitas",
        ["cotizacion_final_respondida"] = "visitas",
        ["ajuste_precio_nuevo"] = "visitas",
        ["ajuste_precio_respondido"] = "visitas",
    };

    public const string SessionRevokedEvent = "sesion_revocada";

    /// <summary>Aviso de texto fijo a quien es parte de un reporte decidido (spec 053 R-20, enmienda de la 037 del 2026-11-23): nunca lleva gravedad, motivo ni datos del reporte; no distingue rol.</summary>
    public const string ReportDecidedEvent = "reporte_decidido";

    /// <summary>Aviso de texto fijo al técnico cuando una opinión suya se oculta (spec 061, R-07a): sin motivo ni datos de la opinión.</summary>
    public const string ReviewHiddenEvent = "opinion_oculta";

    /// <summary>Aviso de texto fijo al técnico cuando el cliente reporta que no llegó a su visita (spec 064, R-07, R-08 y D-01): sin nombre ni datos del cliente ni de la visita, sin acusar y sin penalización.</summary>
    public const string TechnicianAbsentEvent = "tecnico_no_se_presento";

    /// <summary>Texto fijo que la app muestra para <see cref="TechnicianAbsentEvent"/> (spec 064, D-01). La carga v1 no lo transporta: la app lo elige por el campo <c>evento</c> (spec 037, R-05).</summary>
    public const string TechnicianAbsentText = "El cliente reportó que no llegaste a tu visita. Si crees que es un error, contacta a soporte.";

    /// <summary>Aviso de texto fijo y sin enlace de que la exportación de los datos propios está lista (spec 039, R-22, enmienda del 2026-11-23): no distingue rol; la app pide el enlace al abrirlo.</summary>
    public const string ExportReadyEvent = "exportacion_lista";

    public const string ClientRole = "cliente";

    public const string TechnicianRole = "tecnico";

    /// <summary>Rol que recibe cada evento; <c>null</c> si lo reciben ambos (<c>mensaje_nuevo</c>). Un rol que no corresponde al tipo es 422 (R-11, CA-16).</summary>
    private static readonly Dictionary<string, string?> _roleByEvent = new(StringComparer.Ordinal)
    {
        ["solicitud_nueva"] = TechnicianRole,
        ["solicitud_sin_tecnicos"] = ClientRole,
        ["cotizacion_nueva"] = ClientRole,
        ["cotizacion_aceptada"] = TechnicianRole,
        ["visita_afectada"] = ClientRole,
        [ReportDecidedEvent] = null,
        [ReviewHiddenEvent] = TechnicianRole,
        [TechnicianAbsentEvent] = TechnicianRole,
        ["solicitud_cancelada"] = TechnicianRole,
        ["solicitud_vencida"] = ClientRole,
        ["cotizacion_vencida"] = TechnicianRole,
        [NewMessageEvent] = null,
        [ExportReadyEvent] = null,
        ["reprogramacion_propuesta"] = null,
        ["visita_reprogramada"] = null,
        ["reprogramacion_sin_cambio"] = null,
        ["visita_cancelada"] = null,
        ["cliente_ausente"] = ClientRole,
        ["visita_terminada"] = ClientRole,
        ["cotizacion_final_nueva"] = ClientRole,
        ["cotizacion_final_respondida"] = TechnicianRole,
        ["ajuste_precio_nuevo"] = ClientRole,
        ["ajuste_precio_respondido"] = TechnicianRole,
    };

    /// <summary>Si el evento es del catálogo cerrado (los de la carga v1 o <c>sesion_revocada</c>).</summary>
    public static bool IsCatalogType(string? tipo) =>
        tipo is not null && (tipo == SessionRevokedEvent || _typeByEvent.ContainsKey(tipo));

    /// <summary>Si <paramref name="rol"/> corresponde a <paramref name="tipo"/> (R-11).</summary>
    public static bool RoleMatches(string tipo, string? rol) =>
        _roleByEvent.TryGetValue(tipo, out var expected)
        && (rol is null ? tipo is ReportDecidedEvent or ExportReadyEvent : rol is ClientRole or TechnicianRole && (expected is null || expected == rol));

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

    /// <summary>Separa la clave de <see cref="DeduplicationKey"/> en evento y persona (el <c>eventoId</c> es opaco y no lleva <c>|</c>: el validador de la ruta interna lo exige).</summary>
    public static (string EventId, string Person) SplitDeduplicationKey(string key)
    {
        var separator = key.LastIndexOf('|', StringComparison.Ordinal);
        return separator < 1 ? throw new ArgumentException("Invalid deduplication key.", nameof(key)) : (key[..separator], key[(separator + 1)..]);
    }

    /// <summary>Primer aviso de la ventana se envía; los siguientes dentro de los 5 minutos se agrupan (R-07).</summary>
    public static WindowDecision DecideWindow(DateTime? windowStartUtc, DateTime nowUtc) =>
        windowStartUtc is null || nowUtc - windowStartUtc.Value >= MessageWindow ? WindowDecision.Send : WindowDecision.Suppress;

    /// <summary>Al cerrar la ventana se envía un único aviso más si hubo mensajes que se agruparon (R-07).</summary>
    public static bool ShouldFlushWindow(DateTime windowStartUtc, int suppressedCount, DateTime nowUtc) =>
        suppressedCount > 0 && nowUtc - windowStartUtc >= MessageWindow;

    [GeneratedRegex("^[0-9a-f]{24}$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ResourcePattern();
}
