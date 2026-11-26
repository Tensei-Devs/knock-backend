using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace Knock.Hosting.ForwardedHeaders;

/// <summary>
/// Reglas de arranque de las redes de confianza de <c>ForwardedHeaders</c> (spec 035, R-09 y R-10): fuera de <c>Development</c> no se arranca sin redes ni proxies,
/// y cada red debe ser privada y no más amplia que /16 (IPv4) o /48 (IPv6). Los mensajes nombran la llave, nunca los valores.
/// </summary>
public static class ForwardedHeadersValidator
{
    private const int _minimumPrefixV4 = 16;
    private const int _minimumPrefixV6 = 48;

    private static readonly System.Net.IPNetwork[] _privateV4 =
    [
        System.Net.IPNetwork.Parse("10.0.0.0/8"),
        System.Net.IPNetwork.Parse("172.16.0.0/12"),
        System.Net.IPNetwork.Parse("192.168.0.0/16"),
    ];

    private static readonly System.Net.IPNetwork _privateV6 = System.Net.IPNetwork.Parse("fc00::/7");

    public static void Validate(ForwardedHeadersOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        if (environment.IsDevelopment())
        {
            return;
        }

        if (options.KnownProxies.Count == 0 && options.KnownIPNetworks.Count == 0)
        {
            throw new InvalidOperationException(
                $"{ForwardedHeadersExtensions.SectionName}:KnownNetworks and {ForwardedHeadersExtensions.SectionName}:KnownProxies are both empty; outside Development at least one must be set.");
        }

        if (options.KnownIPNetworks.Any(network => !IsAcceptable(network)))
        {
            throw new InvalidOperationException(
                $"{ForwardedHeadersExtensions.SectionName}:KnownNetworks has an entry that is not private or is wider than /{_minimumPrefixV4} (IPv4) or /{_minimumPrefixV6} (IPv6).");
        }
    }

    public static bool IsAcceptable(System.Net.IPNetwork network)
    {
        var address = network.BaseAddress;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return network.PrefixLength >= _minimumPrefixV4 && Array.Exists(_privateV4, parent => parent.Contains(address));
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6
            && network.PrefixLength >= _minimumPrefixV6 && _privateV6.Contains(address);
    }
}
