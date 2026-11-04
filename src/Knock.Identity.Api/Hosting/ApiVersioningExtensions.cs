// jscpd:ignore-start -- EXC-015
using Asp.Versioning;

namespace Knock.Identity.Api.Hosting;

internal static class ApiVersioningExtensions
{
    public static IServiceCollection AddKnockApiVersioning(this IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = false;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc();
        return services;
    }
}
// jscpd:ignore-end
