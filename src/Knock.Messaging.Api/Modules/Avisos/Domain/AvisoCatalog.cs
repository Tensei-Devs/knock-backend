using System.Collections.Frozen;

namespace Knock.Messaging.Api.Modules.Avisos.Domain;

/// <summary>Un aviso dentro de la app tal como lo describe el catálogo (spec 037 R-14): título, texto fijo y la pantalla que la app abre. Nunca lleva datos de la otra parte.</summary>
internal sealed record AvisoPlantilla(string Title, string Text, string Screen);

/// <summary>
/// Textos fijos de los avisos dentro de la app (spec 037 R-14 y spec 044 §6: título hasta 80 caracteres, texto hasta 200). Quien llama a la ruta interna nunca aporta texto (R-11): el texto
/// sale de aquí según el <c>tipo</c>. <c>Screen</c> es el nombre de la pantalla que abre la app; el identificador del recurso es el de la propia persona, no de la otra parte.
/// </summary>
internal static class AvisoCatalog
{
    public const int MaxTitleLength = 80;

    public const int MaxTextLength = 200;

    /// <summary>Conservación del aviso dentro de la app en días (spec 044 R-14 y D-04); igual que <c>NoticeRules.RetentionDays</c>, que este módulo no puede referenciar (CA-037-10).</summary>
    public const int RetentionDays = 30;

    private static readonly FrozenDictionary<string, AvisoPlantilla> _byEvent = new Dictionary<string, AvisoPlantilla>(StringComparer.Ordinal)
    {
        ["solicitud_nueva"] = new("Nueva solicitud", "Hay una solicitud nueva cerca de ti. Ábrela para cotizar.", "solicitud"),
        ["solicitud_sin_tecnicos"] = new("Sin técnicos disponibles", "Por ahora no encontramos técnicos disponibles para tu solicitud. Puedes intentarlo más tarde.", "solicitud"),
        ["cotizacion_nueva"] = new("Nueva cotización", "Recibiste una cotización para tu solicitud.", "cotizacion"),
        ["cotizacion_aceptada"] = new("Cotización aceptada", "Aceptaron tu cotización. Revisa los detalles de la visita.", "visita"),
        ["visita_afectada"] = new("Tu visita cambió", "Una visita próxima se vio afectada. Revísala en la app.", "visita"),
        [AvisoRules.ReportDecidedEvent] = new("Reporte revisado", "Revisamos un reporte en el que participas. Consulta tu cuenta para saber más.", "reporte"),
        [AvisoRules.ReviewHiddenEvent] = new("Opinión oculta", "Una opinión sobre tu trabajo se ocultó tras una revisión.", "opinion"),
        [AvisoRules.TechnicianAbsentEvent] = new("Visita no realizada", AvisoRules.TechnicianAbsentText, "visita"),
        ["solicitud_cancelada"] = new("Solicitud cancelada", "Una solicitud a la que respondiste fue cancelada.", "solicitud"),
        ["solicitud_vencida"] = new("Solicitud vencida", "Tu solicitud venció sin que eligieras una cotización.", "solicitud"),
        ["cotizacion_vencida"] = new("Cotización vencida", "Una de tus cotizaciones venció.", "cotizacion"),
        [AvisoRules.NewMessageEvent] = new("Mensaje nuevo", "Tienes un mensaje nuevo.", "conversacion"),
        [AvisoRules.ExportReadyEvent] = new("Tu exportación está lista", "Ya está lista la exportación de tus datos. Ábrela desde la app para descargarla.", "exportacion"),
        ["reprogramacion_propuesta"] = new("Propuesta de nueva fecha", "Te proponen cambiar la fecha de una visita. Responde antes de que venza.", "visita"),
        ["visita_reprogramada"] = new("Visita reprogramada", "Cambió la fecha de una visita. Revísala en la app.", "visita"),
        ["reprogramacion_sin_cambio"] = new("La fecha no cambió", "Tu propuesta de nueva fecha no se aceptó. La visita sigue como estaba.", "visita"),
        ["visita_cancelada"] = new("Visita cancelada", "Se canceló una visita próxima. Revísala en la app.", "visita"),
        ["cliente_ausente"] = new("Visita no realizada", "El técnico registró que no te encontró en la visita. Si crees que es un error, contacta a soporte.", "visita"),
        ["visita_terminada"] = new("Visita terminada", "Tu visita quedó terminada. Ya puedes dejar tu opinión.", "visita"),
        ["cotizacion_final_nueva"] = new("Cotización final", "Tienes una cotización final por revisar. Responde antes de que venza.", "visita"),
        ["cotizacion_final_respondida"] = new("Cotización final respondida", "Tu cotización final ya tiene respuesta. Revisa la visita.", "visita"),
        ["ajuste_precio_nuevo"] = new("Cambio de precio propuesto", "Te proponen un cambio de precio. Revísalo y responde antes de que venza.", "visita"),
        ["ajuste_precio_respondido"] = new("Cambio de precio respondido", "Tu propuesta de cambio de precio ya tiene respuesta. Revisa la visita.", "visita"),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>La plantilla del <paramref name="evento"/>, o <see langword="null"/> si no es del catálogo.</summary>
    public static AvisoPlantilla? Find(string evento) => _byEvent.GetValueOrDefault(evento);

    /// <summary>Los eventos que tienen plantilla (todos los del catálogo salvo <c>sesion_revocada</c>).</summary>
    public static IReadOnlyCollection<string> Events => _byEvent.Keys;
}
