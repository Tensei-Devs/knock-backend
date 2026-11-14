using Knock.Market.Api.Modules.Media.Data;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Knock.Market.Api.Modules.Media;

/// <summary>Registro del módulo Media (spec 013). Solo esta clase es pública; el resto del módulo es internal.</summary>
public static class MediaModuleExtensions
{
    public static IServiceCollection AddMediaModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<MediaOptions>()
            .BindConfiguration(MediaOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return services;
    }
}
