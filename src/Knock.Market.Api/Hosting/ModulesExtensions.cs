using Knock.Market.Api.Modules.Catalog;
using Knock.Market.Api.Modules.Requests;

namespace Knock.Market.Api.Hosting;

internal static class ModulesExtensions
{
    public static IServiceCollection AddKnockModules(this IServiceCollection services)
    {
        return services.AddCatalogModule().AddRequestsModule();
    }
}
