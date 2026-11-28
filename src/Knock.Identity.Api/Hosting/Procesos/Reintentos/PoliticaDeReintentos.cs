namespace Knock.Identity.Api.Hosting.Procesos.Reintentos;

/// <summary>Reintentos de la bandeja de salida y de las tareas programadas (spec 054, R-04 y R-07): cinco intentos con 5 s, 30 s, 2 min y 10 min de espera.</summary>
internal static class PoliticaDeReintentos
{
    public const int MaxIntentos = 5;
    public const int ArrendamientoMinutos = 2;
    public const int RetencionTerminadoDias = 14;
    public const int RetencionFallidoDias = 90;

    private static readonly int[] _esperasSegundos = [5, 30, 120, 600];

    /// <summary>Espera antes del siguiente intento tras el fallo número <paramref name="intentos"/> (1 a 4), o <see langword="null"/> si ya no quedan intentos.</summary>
    public static TimeSpan? Espera(int intentos) =>
        intentos is >= 1 and < MaxIntentos ? TimeSpan.FromSeconds(_esperasSegundos[intentos - 1]) : null;

    /// <summary>Instante en que la limpieza TTL elimina un documento terminado: 14 días si salió bien, 90 si falló.</summary>
    public static DateTime LimpiarEn(DateTime ahora, bool fallido) =>
        ahora.AddDays(fallido ? RetencionFallidoDias : RetencionTerminadoDias);
}
