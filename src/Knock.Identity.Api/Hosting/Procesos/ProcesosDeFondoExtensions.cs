using Knock.Identity.Api.Hosting.Procesos.Outbox;
using Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;

namespace Knock.Identity.Api.Hosting.Procesos;

/// <summary>Registra la bandeja de salida y el ejecutor de tareas programadas de Identidad (spec 054). Sin rutas: son procesos de fondo y su salud no entra en <c>/health/ready</c> (R-14).</summary>
internal static class ProcesosDeFondoExtensions
{
    public static IServiceCollection AddProcesosDeFondo(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<TareasProgramadasOptions>().BindConfiguration("TareasProgramadas")
            .Validate(x => x.IntervaloSegundos > 0, "TareasProgramadas:IntervaloSegundos must be positive.").ValidateOnStart();
        services.AddOptions<OutboxOptions>().BindConfiguration("Outbox")
            .Validate(x => x.IntervaloSegundos > 0, "Outbox:IntervaloSegundos must be positive.").ValidateOnStart();
        services.AddSingleton<ITareasProgramadasStore>(provider => new TareasProgramadasStore(provider.GetRequiredKeyedService<IMongoDatabase>(DataExtensions.DatabaseKey)));
        services.AddSingleton<IOutboxStore>(provider => new OutboxStore(provider.GetRequiredKeyedService<IMongoDatabase>(DataExtensions.DatabaseKey)));
        // Sin dirección o sin clave del destino la entrega deja el evento para reintento, igual que EntregaInternaNoDisponible.
        services.AddOptions<EntregaInternaOptions>().BindConfiguration(EntregaInternaOptions.SectionName).ValidateDataAnnotations();
        services.AddHttpClient(EntregaInternaHttp.ClientName).RemoveAllLoggers();
        services.TryAddScoped<IEntregaInterna, EntregaInternaHttp>();
        services.AddScoped<EjecutorDeTareasProgramadas>();
        services.AddScoped<ProcesadorDeOutbox>();
        services.AddHostedService<TareasProgramadasHostedService>();
        services.AddHostedService<OutboxHostedService>();
        return services;
    }
}
