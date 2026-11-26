using Knock.Hosting.Data;
using Knock.Market.Api.Common.TareasProgramadas;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Reviews.Data;
using Knock.Market.Api.Modules.Reviews.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Knock.Market.Api.Modules.Reviews;

/// <summary>Registro del módulo Reviews (spec 051). Solo esta clase es pública; el resto del módulo es internal.</summary>
public static class ReviewsModuleExtensions
{
    /// <summary>Nombre del módulo y clave de su base (<c>Mongo:Databases:Reviews</c>).</summary>
    public const string ModuleKey = "Reviews";

    public static IServiceCollection AddReviewsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddKnockMongoDatabase(ModuleKey);
        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddScoped<IReviewStore, ReviewStore>();
        services.AddScoped<ReviewService>();
        services.AddScoped<ReviewModerationService>();
        services.AddScoped<IManejadorDeTarea, CerrarPeriodoDeOpinion>();
        return services;
    }
}
