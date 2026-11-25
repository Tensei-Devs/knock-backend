using Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;
using Knock.Identity.Api.Modules.Account.Data;
using Knock.Identity.Api.Modules.Account.Domain;
using Knock.Identity.Api.Modules.Account.Services;
using Knock.Identity.Api.Modules.HomeAndCar.Services;
using Knock.Identity.Api.Modules.Moderation.Services;
using Knock.Identity.Api.Modules.Usuarios.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Knock.Identity.Api.Modules.Account;

/// <summary>Registro del módulo Account (spec 039: cuenta y datos personales). Solo esta clase es pública; el resto del módulo es internal.</summary>
public static class AccountModuleExtensions
{
    public static IServiceCollection AddAccountModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AccountStore>();
        services.AddSingleton<ISessionCanceller, RedisSessionCanceller>();
        services.AddSingleton<ProfileCacheInvalidator>();
        services.AddSingleton<IAccountStore>(provider => provider.GetRequiredService<AccountStore>());
        services.AddSingleton<IAccountExportSource>(provider => provider.GetRequiredService<AccountStore>());
        services.AddSingleton<IAccountOutbox, AccountOutbox>();
        services.AddSingleton<PhoneChangeService>();
        services.AddSingleton<IRecoveryAttemptLimiter, RecoveryAttemptLimiter>();
        services.AddSingleton<AccessRecoveryService>();
        // Lo que sigue de aprobar una recuperacion en el panel (spec 053, D-07): aviso a los dispositivos y sesiones canceladas.
        services.Replace(ServiceDescriptor.Singleton<IRecoverySessionClosures, RecoverySessionClosures>());
        services.AddSingleton<DataExportService>();
        services.AddOptions<OptionalConsentOptions>().BindConfiguration(OptionalConsentOptions.SectionName);
        services.AddSingleton<ConsentService>();
        // Rutas internas de Mercado (spec 039, §8): comparten dirección y clave con el historial (Services:Market). Sin ellas fallan cerrado con 503.
        services.AddOptions<MarketHistoryOptions>().BindConfiguration(MarketHistoryOptions.SectionName).ValidateDataAnnotations();
        services.AddHttpClient<MarketAccountClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<MarketHistoryOptions>>().Value;
            if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseAddress) && baseAddress.Scheme is "http" or "https")
            {
                client.BaseAddress = baseAddress;
            }

            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
        services.TryAddSingleton<IMarketAccountClient>(provider => provider.GetRequiredService<MarketAccountClient>());
        services.TryAddSingleton<IMarketExport>(provider => provider.GetRequiredService<MarketAccountClient>());
        services.TryAddSingleton<IExportStorage, UnavailableExportStorage>();
        services.AddSingleton<IExportNotice, OutboxExportNotice>();
        services.AddSingleton<LatestExportService>();
        services.AddSingleton<IManejadorDeTarea, PrepararExportacionHandler>();
        services.AddSingleton<PendingService>();
        services.AddSingleton<AccountDeletionService>();
        services.AddSingleton<IManejadorDeTarea, EliminarCuentaHandler>();
        services.AddSingleton<IManejadorDeTarea, VencerRecuperacionHandler>();
        return services;
    }
}
