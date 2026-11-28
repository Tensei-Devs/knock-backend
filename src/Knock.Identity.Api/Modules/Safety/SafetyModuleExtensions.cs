using Knock.Contracts.Moderation;
using Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;
using Knock.Identity.Api.Modules.HomeAndCar.Services;
using Knock.Identity.Api.Modules.Safety.Data;
using Knock.Identity.Api.Modules.Safety.Domain;
using Knock.Identity.Api.Modules.Safety.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Knock.Identity.Api.Modules.Safety;

/// <summary>Módulo de seguridad entre personas (spec 043): bloqueos y, después, reportes con apelación.</summary>
public static class SafetyModuleExtensions
{
    public static IServiceCollection AddSafetyModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<BloqueosStore>();
        services.AddSingleton<BlockService>();
        services.AddSingleton<ReportesStore>();
        services.AddSingleton<IWarningCounter>(provider => provider.GetRequiredService<ReportesStore>());
        // Hasta que exista la moderación de la spec 031 en Identidad, un reporte no se guarda sin revisión (falla cerrado). Las pruebas la sustituyen.
        services.TryAddSingleton<IContentModerator, UnavailableContentModerator>();
        services.AddSingleton<ReportService>();
        // Reporte de «no terminado» (spec 053): Identidad lee el texto por la ruta interna de Mercado (misma dirección y clave que el historial); sin ellas Mercado «no responde» y el evento se reintenta.
        services.AddOptions<MarketHistoryOptions>().BindConfiguration(MarketHistoryOptions.SectionName).ValidateDataAnnotations();
        services.AddHttpClient<IMarketNotFinishedReports, MarketNotFinishedReportClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<MarketHistoryOptions>>().Value;
            if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseAddress) && baseAddress.Scheme is "http" or "https")
            {
                client.BaseAddress = baseAddress;
            }

            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
        services.AddScoped<NotFinishedReportConsumer>();
        services.AddOptions<SuspensionOptions>().BindConfiguration(SuspensionOptions.SectionName).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SuspensionOptions>, SuspensionOptionsValidator>());
        services.AddSingleton<ISuspensionStore, SuspensionStore>();
        services.AddSingleton<AccountSuspension>();
        services.AddSingleton<ISafetyApi, SafetyApi>();
        // Apelación de una persona suspendida (spec 060) y vencimiento de la suspensión gradual.
        services.AddSingleton<SuspensionAppealStore>();
        services.AddSingleton<SuspensionAppealLimiter>();
        services.AddSingleton<SuspensionAppealService>();
        services.AddSingleton<SuspensionAppealResolutionService>();
        services.AddSingleton<IManejadorDeTarea, VencerSuspensionHandler>();
        return services;
    }
}
