using System.Net;
using Knock.SecurityTest.Support;
using Xunit;

namespace Knock.SecurityTest.Identity;

/// <summary>Spec 025: las rutas de verificación de identidad no responden sin una credencial válida. No cubre la subida real de fotos.</summary>
public class Spec025VerificacionSecurityTests
{
    private const string _route = "/api/v1/me/verificacion";

    [Theory]
    [InlineData("GET", "")]
    [InlineData("POST", "")]
    [InlineData("POST", "/665f1c2a9b3e4d0012345678/enviar")]
    [InlineData("POST", "/665f1c2a9b3e4d0012345678/fotos/selfie/confirmar")]
    public async Task Sin_Token_Vencido_O_Sin_Firma_Responde_401_CA_025_17(string method, string suffix)
    {
        var hosts = await SecurityHosts.SharedAsync();

        foreach (var token in new[] { null, TokenForge.Expired(), TokenForge.AlgNone() })
        {
            using var response = await SecureHttp.SendAsync(hosts.IdentityClient, new HttpMethod(method), _route + suffix, bearer: token);
            await SecureHttp.AssertProblemAsync(response, HttpStatusCode.Unauthorized);
        }
    }
}
