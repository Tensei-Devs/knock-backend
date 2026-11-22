namespace Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;

/// <summary>
/// Catálogo cerrado de tipos de tarea con su plazo (spec 054, R-08). Un tipo que no está aquí no se ejecuta. Los manejadores de negocio llegan con la spec de cada funcionalidad.
/// </summary>
internal static class CatalogoDeTareas
{
    public const string VencerPropuestaDeReprogramacion = "VencerPropuestaDeReprogramacion";

    private static readonly TimeSpan _dosHoras = TimeSpan.FromHours(2);
    private static readonly TimeSpan _veinticuatroHoras = TimeSpan.FromHours(24);

    private static readonly Dictionary<string, TimeSpan> _plazos = new(StringComparer.Ordinal)
    {
        ["RepartirOleada"] = TimeSpan.FromMinutes(15),
        ["RevisarOleada"] = TimeSpan.FromMinutes(15),
        ["VencerSolicitud"] = TimeSpan.FromHours(48),
        ["AvisarAlVerificador"] = _veinticuatroHoras,
        ["BorrarFotosDeVerificacion"] = TimeSpan.FromDays(7),
        ["EliminarCuenta"] = TimeSpan.FromDays(7),
        ["PrepararExportacion"] = TimeSpan.Zero,
        ["VencerRecuperacion"] = TimeSpan.FromDays(7),
        ["VencerCotizacionFinal"] = TimeSpan.FromHours(48),
        ["VencerAjuste"] = _veinticuatroHoras,
        [VencerPropuestaDeReprogramacion] = _veinticuatroHoras,
        ["CerrarPeriodoDeOpinion"] = TimeSpan.FromDays(30),
        ["CerrarReporteDeNoTerminado"] = TimeSpan.FromDays(7),
    };

    /// <summary>Los tipos del catálogo.</summary>
    public static IReadOnlyCollection<string> Tipos => _plazos.Keys;

    public static bool Contiene(string tipo) => _plazos.ContainsKey(tipo);

    /// <summary>
    /// Cuándo debe ejecutarse una tarea del <paramref name="tipo"/> programada en <paramref name="ahora"/> (UTC). Para la propuesta de reprogramación es lo que ocurra primero entre 24 horas y
    /// 2 horas antes de la cita (<paramref name="cita"/>).
    /// </summary>
    /// <exception cref="ArgumentException">El tipo no está en el catálogo.</exception>
    public static DateTime ProgramadaPara(string tipo, DateTime ahora, DateTime? cita = null)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        if (!_plazos.TryGetValue(tipo, out var plazo))
        {
            throw new ArgumentException("unknown_task_type", nameof(tipo));
        }

        var due = ahora + plazo;
        if (tipo == VencerPropuestaDeReprogramacion && cita is { } appointment)
        {
            var beforeAppointment = appointment - _dosHoras;
            due = beforeAppointment < due ? beforeAppointment : due;
        }

        return due < ahora ? ahora : due;
    }
}
