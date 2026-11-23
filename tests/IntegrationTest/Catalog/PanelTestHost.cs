extern alias MarketApi;

using Knock.IntegrationTest.Requests;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Knock.IntegrationTest.Catalog;

/// <summary>Arranque y token compartidos por las pruebas del panel de Mercado: MongoDB real, puerta de permisos reemplazada y token firmado con la llave del panel de la prueba.</summary>
internal static class PanelTestHost
{
    public static async Task<(Spec020Host Host, FakePanelGate Gate)> StartAsync(byte[] panelKey)
    {
        var gate = new FakePanelGate();
        var host = await Spec020Host.StartAsync(
            new Dictionary<string, string?> { [PanelAuthentication.KeyConfigurationKey] = Convert.ToBase64String(panelKey) },
            services => services.AddSingleton<IPanelPermissionGate>(gate));
        return (host, gate);
    }

    public static string Token(byte[] key, string? audience = PanelAuthentication.DefaultAudience) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = GatewayTestSupport.Issuer,
            Audience = audience,
            Claims = new Dictionary<string, object> { ["sub"] = Guid.NewGuid().ToString("D"), ["sid"] = "sesion-de-prueba", ["pv"] = "1" },
            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256),
        });
}
