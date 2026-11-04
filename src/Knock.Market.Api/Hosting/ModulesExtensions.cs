using Knock.Market.Api.Modules.Catalog;

namespace Knock.Market.Api.Hosting;

internal static class ModulesExtensions
{
    public static IServiceCollection AddKnockModules(this IServiceCollection services)
    {
        return services.AddCatalogModule();
    }
}
