using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Services;

namespace Knock.Market.Api.Modules.Requests;

/// <summary>Registro del módulo Requests. Solo esta clase y <see cref="IRequestsApi"/> son públicas; el resto del módulo es internal.</summary>
public static class RequestsModuleExtensions
{
    /// <summary>Nombre del módulo y clave de su base (<c>Mongo:Databases:Requests</c>).</summary>
    public const string ModuleKey = "Requests";

    public static IServiceCollection AddRequestsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<QuotePricingOptions>().BindConfiguration(QuotePricingOptions.SectionName);
        services.AddScoped<IRequestsApi, RequestsApi>();
        return services;
    }
}
