namespace Knock.SecurityTest.Support;

/// <summary>
/// Un host de Identidad y uno de Mercado compartidos por todas las clases de pruebas (límite de peticiones práctico inexistente, en <c>Production</c>),
/// creados una sola vez por ejecución. Las pruebas que miden el limitador o necesitan otra configuración crean su propio host.
/// </summary>
internal sealed class SecurityHosts
{
    private static readonly Lazy<Task<SecurityHosts>> _shared = new(StartAsync);

    private SecurityHosts(SecurityIdentityFactory identity, SecurityMarketFactory market)
    {
        Identity = identity;
        Market = market;
        IdentityClient = identity.CreateClient();
        MarketClient = market.CreateClient();
    }

    public SecurityIdentityFactory Identity { get; }

    public SecurityMarketFactory Market { get; }

    public HttpClient IdentityClient { get; }

    public HttpClient MarketClient { get; }

    public static Task<SecurityHosts> SharedAsync() => _shared.Value;

    private static async Task<SecurityHosts> StartAsync()
    {
        var identity = await SecurityIdentityFactory.CreateAsync();
        var market = await SecurityMarketFactory.CreateAsync();
        await market.AddOficioAsync("MEC", "Mecánica", "taller", activo: true);
        await market.AddOficioAsync("PLO", "Plomería", "domicilio", activo: true);
        await market.AddOficioAsync("OLD", "Oficio retirado", "taller", activo: false);
        return new SecurityHosts(identity, market);
    }
}
