using Knock.Contracts.Catalog;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Catalog.Data;
using Knock.Market.Api.Modules.Catalog.Services;

namespace Knock.Market.Api.Modules.Catalog;

/// <summary>Registro del módulo Catalog. Solo esta clase y <see cref="ICatalogApi"/> son públicas; el resto del módulo es internal.</summary>
public static class CatalogModuleExtensions
{
    /// <summary>Nombre del módulo y clave de su base (<c>Mongo:Databases:Catalog</c>).</summary>
    public const string ModuleKey = "Catalog";

    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddKnockMongoDatabase(ModuleKey);
        services.AddSingleton<CatalogStore>();
        services.AddSingleton<NeighborhoodLocator>();
        services.AddSingleton<INeighborhoodLocator>(sp => sp.GetRequiredService<NeighborhoodLocator>());
        services.AddSingleton<INeighborhoodNames, NeighborhoodNames>();
        services.AddScoped<ICatalogApi, CatalogApi>();
        services.AddScoped<INeighborhoodCenterCatalog, CatalogApi>();
        services.AddScoped<CatalogSeeder>();
        services.AddScoped<OficioQueries>();
        return services;
    }
}
