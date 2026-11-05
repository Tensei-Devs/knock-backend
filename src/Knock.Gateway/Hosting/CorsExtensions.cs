// jscpd:ignore-start -- EXC-016
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Knock.Gateway.Hosting;

/// <summary>CORS del gateway (spec 009, R-03): solo los orígenes de <c>Security:Cors:AllowedOrigins</c>; sin ninguno configurado no se permite ninguno.</summary>
internal static class CorsExtensions
{
    public const string PolicyName = "knock";

    public static IServiceCollection AddKnockGatewayCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Security:Cors:AllowedOrigins").Get<string[]>() ?? [];
        foreach (var origin in origins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.AbsolutePath != "/" || origin[^1] == '/' || uri.Scheme is not ("http" or "https"))
            {
                throw new InvalidOperationException("Security:Cors:AllowedOrigins has an entry that is not an origin (scheme://host[:port]).");
            }
        }

        services.AddCors(options => options.AddPolicy(PolicyName, policy => policy
            .WithOrigins(origins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders("Authorization", "Content-Type", "X-Correlation-Id")
            .WithExposedHeaders("X-Correlation-Id", "Retry-After")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));
        return services;
    }

    /// <summary>
    /// IP real y esquema detrás del proxy: solo se aceptan <c>X-Forwarded-For</c> y <c>X-Forwarded-Proto</c> de los proxies y redes de
    /// <c>ForwardedHeaders:KnownProxies</c> y <c>ForwardedHeaders:KnownNetworks</c>. Sin ninguno configurado no se confía en nadie.
    /// </summary>
    public static IServiceCollection AddKnockGatewayForwardedHeaders(this IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            options.ForwardLimit = 1;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
            {
                options.KnownProxies.Add(IPAddress.TryParse(proxy, out var address)
                    ? address
                    : throw new InvalidOperationException("ForwardedHeaders:KnownProxies has an entry that is not an IP address."));
            }

            foreach (var network in configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.TryParse(network, out var parsed)
                    ? parsed
                    : throw new InvalidOperationException("ForwardedHeaders:KnownNetworks has an entry that is not a CIDR network."));
            }

            options.ForwardedHeaders = options.KnownProxies.Count > 0 || options.KnownIPNetworks.Count > 0
                ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                : ForwardedHeaders.None;
        });
        return services;
    }
}
// jscpd:ignore-end
