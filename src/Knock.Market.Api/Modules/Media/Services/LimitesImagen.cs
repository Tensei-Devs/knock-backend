using System.Collections.Concurrent;
using Knock.Market.Api.Hosting;
using StackExchange.Redis;

namespace Knock.Market.Api.Modules.Media.Services;

/// <summary>Qué límite horario por persona se consume (spec 032, R-14).</summary>
internal enum LimiteImagen { Subidas, Confirmaciones }

/// <summary>Resultado de consumir un permiso: si se permite y, si no, cuánto falta para que se reinicie la ventana.</summary>
internal sealed record ResultadoLimite(bool Permitido, TimeSpan ReintentarEn);

/// <summary>Contador de ventana fija: cuenta un golpe sobre una llave y dice cuánto queda de la ventana.</summary>
internal interface IContadorVentana
{
    Task<(long Cuenta, TimeSpan Restante)> GolpearAsync(string llave, TimeSpan ventana, CancellationToken cancellationToken);
}

/// <summary>Contador compartido entre réplicas: incremento y vencimiento en una sola operación atómica de Redis (patrón del gateway).</summary>
internal sealed class ContadorVentanaRedis(IConnectionMultiplexer redis) : IContadorVentana
{
    private readonly RedisRequestCounter _contador = new(redis);

    public Task<(long Cuenta, TimeSpan Restante)> GolpearAsync(string llave, TimeSpan ventana, CancellationToken cancellationToken) =>
        _contador.HitAsync(llave, ventana, cancellationToken);
}

/// <summary>Contador en memoria de una sola instancia: respaldo cuando Redis no está disponible y doble de pruebas.</summary>
internal sealed class ContadorVentanaMemoria(TimeProvider reloj) : IContadorVentana
{
    private readonly ConcurrentDictionary<string, (long Cuenta, DateTimeOffset Fin)> _ventanas = new();

    public Task<(long Cuenta, TimeSpan Restante)> GolpearAsync(string llave, TimeSpan ventana, CancellationToken cancellationToken)
    {
        var ahora = reloj.GetUtcNow();
        var estado = _ventanas.AddOrUpdate(
            llave,
            _ => (1, ahora + ventana),
            (_, actual) => actual.Fin <= ahora ? (1, ahora + ventana) : (actual.Cuenta + 1, actual.Fin));
        return Task.FromResult((estado.Cuenta, estado.Fin - ahora));
    }
}

/// <summary>
/// Límites por persona de las imágenes públicas (spec 032, R-14): 20 enlaces de subida y 30 confirmaciones por hora, en contadores de
/// ventana fija de Redis (<c>img:subidas:{sub}</c> e <c>img:confirmaciones:{sub}</c>) compartidos entre réplicas, y como máximo
/// 10 subidas pendientes vigentes. Si Redis falla se usa el límite por instancia (mismo patrón que el gateway); no se abre el paso sin límite.
/// </summary>
internal sealed class LimitesImagen(IContadorVentana principal, IContadorVentana respaldo)
{
    internal const int MaximoSubidasPorHora = 20;
    internal const int MaximoConfirmacionesPorHora = 30;
    internal const int MaximoPendientesVigentes = 10;
    internal static readonly TimeSpan Ventana = TimeSpan.FromHours(1);

    /// <summary>Llave de Redis del límite (R-14): el <c>sub</c> solo viaja a Redis, nunca a los registros.</summary>
    internal static string Llave(LimiteImagen limite, string sub) =>
        (limite == LimiteImagen.Subidas ? "img:subidas:" : "img:confirmaciones:") + sub;

    /// <summary>Consume un permiso del límite; el que pasa del máximo recibe <c>Permitido = false</c> y el tiempo para reintentar.</summary>
    public async Task<ResultadoLimite> ConsumirAsync(LimiteImagen limite, string sub, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);
        var maximo = limite == LimiteImagen.Subidas ? MaximoSubidasPorHora : MaximoConfirmacionesPorHora;
        var llave = Llave(limite, sub);
        (long Cuenta, TimeSpan Restante) golpe;
        try
        {
            golpe = await principal.GolpearAsync(llave, Ventana, cancellationToken);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or InvalidOperationException)
        {
            golpe = await respaldo.GolpearAsync(llave, Ventana, cancellationToken);
        }

        return new ResultadoLimite(golpe.Cuenta <= maximo, golpe.Restante);
    }

    /// <summary>Una subida nueva solo se acepta si la persona tiene menos de 10 pendientes vigentes (R-14).</summary>
    public static bool PermiteNuevaSubida(int pendientesVigentes) => pendientesVigentes < MaximoPendientesVigentes;
}
