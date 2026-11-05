using System.Diagnostics;

namespace Knock.UnitTest.Hosting;

/// <summary>
/// Medición de las pruebas de tiempo de <c>TelemetryRedactor</c> (TC-TT-OBS-01-12), compartida por Market y por sus copias (spec 011).
/// Cada prueba conserva su propio límite. Esas pruebas llevan <c>[Trait("Category", "Performance")]</c>: el CI las corre aparte, sin cobertura.
/// </summary>
internal static class RedactTiming
{
    /// <summary>Entrada de 4 KB que repite <paramref name="unit"/>.</summary>
    public static string Input4KB(string unit) => string.Concat(Enumerable.Repeat(unit, 4096 / unit.Length + 1))[..4096];

    /// <summary>Mejor tiempo medio por llamada (ms) de 7 lotes de 50, después de calentar 300 ms.</summary>
    public static double BestMillisecondsPerCall(Func<string, string> redact, string input)
    {
        // Calentamiento hasta el JIT optimizado (Tier-1) y el mejor de varios lotes: el planificador y las otras pruebas en paralelo
        // solo suman tiempo; un retroceso catastrófico seguiría lento en todos los lotes.
        var warmUp = Stopwatch.StartNew();
        while (warmUp.ElapsedMilliseconds < 300)
        {
            redact(input);
        }

        var best = double.MaxValue;
        for (var batch = 0; batch < 7; batch++)
        {
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < 50; i++)
            {
                redact(input);
            }

            best = Math.Min(best, watch.Elapsed.TotalMilliseconds / 50);
        }

        return best;
    }
}
