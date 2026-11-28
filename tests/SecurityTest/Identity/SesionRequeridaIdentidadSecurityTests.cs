extern alias IdentityApi;

using System.Net;
using AwesomeAssertions;
using IdentityApi::Knock.Identity.Api.Modules.Panel.Data;
using Knock.IntegrationTest.Support;
using Knock.SecurityTest.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Knock.SecurityTest.Identity;

/// <summary>
/// Specs 033 y 060: Identidad exige por sí misma (sin depender del gateway) el token del panel para cerrar su sesión, y la consulta de la apelación de una suspensión no se
/// entrega sin su credencial. Solo datos ficticios.
/// </summary>
public sealed class SesionRequeridaIdentidadSecurityTests
{
    [Fact]
    public async Task ClosingThePanelSession_RequiresThePanelToken_CA_033_15()
    {
        await using var factory = await SecurityIdentityFactory.CreateAsync(PanelReportKit.HostSettings);
        using var client = factory.CreateClient();
        await factory.Services.GetRequiredService<PanelAuditoriaStore>().EnsureIndexesAsync(CancellationToken.None);

        using var anonymous = await CloseAsync(client, null);
        using var appToken = await CloseAsync(client, TokenForge.Valid());
        using var foreignKey = await CloseAsync(client, PanelReportKit.Token(TokenForge.Sub(), key: TokenForge.SigningKey));

        await SecureHttp.AssertProblemAsync(anonymous, HttpStatusCode.Unauthorized);
        await SecureHttp.AssertProblemAsync(appToken, HttpStatusCode.Unauthorized);
        await SecureHttp.AssertProblemAsync(foreignKey, HttpStatusCode.Unauthorized);

        // Control: con el token del panel la misma petición sí cierra.
        using var panel = await CloseAsync(client, PanelReportKit.Token(TokenForge.Sub()));

        panel.StatusCode.Should().Be(HttpStatusCode.NoContent, await panel.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ReadingASuspensionAppeal_WithoutItsCredential_Returns401_CA_060_02()
    {
        var hosts = await SecurityHosts.SharedAsync();
        const string path = "/api/v1/suspension/apelacion";

        using var anonymous = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Get, path);
        using var appToken = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Get, path, bearer: TokenForge.Valid());

        await SecureHttp.AssertProblemAsync(anonymous, HttpStatusCode.Unauthorized);
        await SecureHttp.AssertProblemAsync(appToken, HttpStatusCode.Unauthorized);
    }

    private static async Task<HttpResponseMessage> CloseAsync(HttpClient client, string? bearer)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/panel/sesion/cerrar", UriKind.Relative));
        request.Headers.Add("X-Knock-Panel", "1");
        if (bearer is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearer);
        }

        return await client.SendAsync(request);
    }
}
