using Knock.Messaging.Api.Modules.Notices.Data;
using Knock.Messaging.Api.Modules.Notices.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Knock.Messaging.Api.Modules.Notices;

/// <summary>Registro del módulo Notices (spec 044: avisos dentro de la app). Solo esta clase es pública; el resto del módulo es internal.</summary>
public static class NoticesModuleExtensions
{
    public static IServiceCollection AddNoticesModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<NoticesStore>();
        services.AddSingleton<INoticesStore>(provider => provider.GetRequiredService<NoticesStore>());
        services.AddScoped<NoticesService>();
        return services;
    }
}
