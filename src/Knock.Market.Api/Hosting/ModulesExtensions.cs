using Knock.Market.Api.Common.TareasProgramadas;
using Knock.Market.Api.Modules.Catalog;
using Knock.Market.Api.Modules.Media;
using Knock.Market.Api.Modules.Moderation;
using Knock.Market.Api.Modules.Requests;
using Knock.Market.Api.Modules.Reviews;
using Knock.Market.Api.Modules.Tecnicos;
using Knock.Market.Api.Modules.Trust;

namespace Knock.Market.Api.Hosting;

internal static class ModulesExtensions
{
    public static IServiceCollection AddKnockModules(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        services.AddModerationModule(configuration);
        services.AddCatalogModule(configuration).AddMediaModule(isDevelopment).AddRequestsModule().AddTrustModule().AddTecnicosModule().AddReviewsModule();
        services.AddOptions<TareasProgramadasOptions>().BindConfiguration("TareasProgramadas")
            .Validate(x => x.IntervaloSegundos > 0, "TareasProgramadas:IntervaloSegundos must be positive.").ValidateOnStart();
        services.AddScoped<EjecutorDeTareasProgramadas>();
        services.AddHostedService<TareasProgramadasHostedService>();
        return services;
    }
}
