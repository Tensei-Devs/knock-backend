using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Media.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Knock.Market.Api.Modules.Media.Data;

/// <summary>Contador compartido entre réplicas: incremento y vencimiento en una sola operación atómica de Redis (patrón del gateway).</summary>
internal sealed class ContadorVentanaRedis(IConnectionMultiplexer redis) : IContadorVentana
{
    private readonly RedisRequestCounter _contador = new(redis);

    public Task<(long Cuenta, TimeSpan Restante)> GolpearAsync(string llave, TimeSpan ventana, CancellationToken cancellationToken) =>
        _contador.HitAsync(llave, ventana, cancellationToken);
}

internal static class LimitesImagenRegistro
{
    /// <summary>Registra los límites por persona sobre Redis compartido, con el contador en memoria como respaldo (spec 032, R-14).</summary>
    public static IServiceCollection AddLimitesImagen(this IServiceCollection services)
    {
        services.TryAddSingleton(provider => new LimitesImagen(
            new ContadorVentanaRedis(provider.GetRequiredService<IConnectionMultiplexer>()),
            new ContadorVentanaMemoria(provider.GetRequiredService<TimeProvider>())));
        return services;
    }
}
