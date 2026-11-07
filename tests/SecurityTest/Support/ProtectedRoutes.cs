using System.Net.Http.Json;

namespace Knock.SecurityTest.Support;

/// <summary>Las cuatro rutas protegidas de los endpoints de negocio (tres de Identidad y una de Mercado) para recorrerlas con la misma credencial.</summary>
internal static class ProtectedRoutes
{
    public const string Logout = "POST /auth/logout";
    public const string MeGet = "GET /me";
    public const string MePut = "PUT /me";
    public const string Colonias = "GET /colonias/en-punto";

    public static readonly string[] All = [Logout, MeGet, MePut, Colonias];

    /// <summary>Todas las combinaciones ruta × ataque, como filas de <c>MemberData</c>.</summary>
    public static TheoryData<string, string> RoutesTimesAttacks()
    {
        var data = new TheoryData<string, string>();
        foreach (var route in All)
        {
            foreach (var attack in TokenForge.Attacks.Keys)
            {
                data.Add(route, attack);
            }
        }

        return data;
    }

    public static TheoryData<string> Routes()
    {
        var data = new TheoryData<string>();
        foreach (var route in All)
        {
            data.Add(route);
        }

        return data;
    }

    /// <summary>Envía la petición de la ruta con el valor exacto de <c>Authorization</c> (o sin el encabezado si es <c>null</c>).</summary>
    public static async Task<HttpResponseMessage> SendAsync(SecurityHosts hosts, string route, string? authorization)
    {
        ArgumentNullException.ThrowIfNull(hosts);
        return route switch
        {
            Logout => await SecureHttp.SendWithRawAuthorizationAsync(hosts.IdentityClient, HttpMethod.Post, "/api/v1/auth/logout", authorization),
            MeGet => await SecureHttp.SendWithRawAuthorizationAsync(hosts.IdentityClient, HttpMethod.Get, "/api/v1/me", authorization),
            MePut => await SecureHttp.SendWithRawAuthorizationAsync(hosts.IdentityClient, HttpMethod.Put, "/api/v1/me", authorization, JsonContent.Create(new { nombre = "Ana Pérez" })),
            Colonias => await SecureHttp.SendWithRawAuthorizationAsync(hosts.MarketClient, HttpMethod.Get, "/api/v1/colonias/en-punto?lat=19.4326&lon=-99.1332", authorization),
            _ => throw new ArgumentOutOfRangeException(nameof(route), route, "Unknown route."),
        };
    }

    public static Task<HttpResponseMessage> SendBearerAsync(SecurityHosts hosts, string route, string token) => SendAsync(hosts, route, "Bearer " + token);
}
