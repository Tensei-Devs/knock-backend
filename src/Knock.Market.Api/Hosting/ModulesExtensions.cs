using Knock.Market.Api.Common.Bandeja;
using Knock.Market.Api.Common.TareasProgramadas;
using Knock.Market.Api.Modules.Assistant;
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
        services.AddAssistantModule(configuration).AddCatalogModule(configuration).AddMediaModule(isDevelopment).AddRequestsModule().AddTrustModule().AddTecnicosModule().AddReviewsModule();
        services.AddOptions<TareasProgramadasOptions>().BindConfiguration("TareasProgramadas")
            .Validate(x => x.IntervaloSegundos > 0, "TareasProgramadas:IntervaloSegundos must be positive.").ValidateOnStart();
        services.AddScoped<EjecutorDeTareasProgramadas>();
        services.AddHostedService<TareasProgramadasHostedService>();
        services.AddOptions<BandejaOptions>().BindConfiguration(BandejaOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddHttpClient(EntregaReporteNoTerminado.ClientName).RemoveAllLoggers();
        services.AddScoped<IEntregaDeBandeja, EntregaReporteNoTerminado>();
        services.AddHttpClient(EntregaOpinionModerada.IdentityClientName).RemoveAllLoggers();
        services.AddHttpClient(EntregaOpinionModerada.MessagingClientName).RemoveAllLoggers();
        services.AddScoped<IEntregaDeBandeja, EntregaOpinionModerada>();
        services.AddScoped<IEntregaDeBandeja, EntregaTecnicoNoSePresento>();
        services.AddScoped<DespachadorDeBandeja>();
        services.AddHostedService<BandejaHostedService>();
        return services;
    }
}
