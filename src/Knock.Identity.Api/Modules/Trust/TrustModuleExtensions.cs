using Knock.Identity.Api.Hosting.Procesos.TareasProgramadas;
using Knock.Identity.Api.Modules.Moderation.Services;
using Knock.Identity.Api.Modules.Trust.Crypto;
using Knock.Identity.Api.Modules.Trust.Data;
using Knock.Identity.Api.Modules.Trust.Services;
using Knock.Identity.Api.Modules.Trust.Storage;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Knock.Identity.Api.Modules.Trust;

/// <summary>Registro del módulo Trust (spec 025: verificación de identidad). Solo esta clase es pública; el resto del módulo es internal.</summary>
public static class TrustModuleExtensions
{
    public static IServiceCollection AddTrustModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<VerificationOptions>().BindConfiguration(VerificationOptions.SectionName)
            .Validate(o => !string.IsNullOrWhiteSpace(o.AvisoVersion) && !string.IsNullOrWhiteSpace(o.AvisoTexto), "Verificacion:AvisoVersion and AvisoTexto are required.")
            .ValidateOnStart();
        services.AddSingleton<ITrustApi, TrustApi>();
        services.AddSingleton<VerificationStore>();
        services.AddSingleton<IVerificationStore>(provider => provider.GetRequiredService<VerificationStore>());
        services.AddScoped<VerificationService>();
        services.AddOptions<CurpHmacOptions>().BindConfiguration(CurpHmacOptions.SectionName);
        services.AddSingleton(provider => CurpHmacOptions.Create(provider.GetRequiredService<IOptions<CurpHmacOptions>>()));
        services.AddScoped<CurpDuplicateGuard>();
        services.AddSingleton<IVerificationPurgeStore, VerificationPurgeStore>();
        services.AddScoped<PurgaDeVerificacion>();
        services.AddSingleton<VerificationPurgeScheduler>();
        services.AddSingleton<DemoVerificationStore>();
        services.AddSingleton<DemoVerificationSeeder>();
        services.AddHostedService<DemoVerificationStartup>();
        services.AddScoped<IManejadorDeTarea, AvisarAlVerificadorHandler>();
        services.AddScoped<IManejadorDeTarea, BorrarFotosDeVerificacionHandler>();
        // Dependencias de infraestructura con valor por omisión que FALLA CERRADO (503): el cliente real de Blob y de Key Vault se registra antes de AddTrustModule() y las reemplaza.
        services.TryAddSingleton<IVerificationBlobStore, UnavailableVerificationBlobStore>();
        services.TryAddSingleton<IVerificationKeyWrapper, UnavailableVerificationKeyWrapper>();
        services.AddSingleton<CifradoDeVerificacion>();
        services.AddSingleton<FotoVerificacionStore>();
        services.TryAddSingleton<IVerificationEvents, PendingVerificationEvents>();
        // Reemplazan a los valores por omisión del panel (spec 053) sin importar el orden de registro.
        services.Replace(ServiceDescriptor.Singleton<IVerificationPhotoLinks, TrustVerificationPhotoLinks>());
        services.Replace(ServiceDescriptor.Singleton<IVerificationBlobPurger, TrustVerificationBlobPurger>());
        return services;
    }
}
