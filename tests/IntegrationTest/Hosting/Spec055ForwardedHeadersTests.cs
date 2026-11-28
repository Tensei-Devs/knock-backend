using System.Net;
using AwesomeAssertions;
using Xunit;

namespace Knock.IntegrationTest.Hosting;

/// <summary>Spec 055, CA-04: cada proyecto arrancado usa los encabezados reenviados de la biblioteca compartida. Sin contenedores; solo datos ficticios.</summary>
public sealed class Spec055ForwardedHeadersTests
{
    private const string _networkKey = "ForwardedHeaders:KnownNetworks:0";
    private const string _trustedNetwork = "10.20.0.0/23";
    private static readonly IPAddress _proxy = IPAddress.Parse("10.20.0.5");
    private static readonly IPAddress _client = IPAddress.Parse("198.51.100.7");

    public static TheoryData<string> Projects => [.. Spec055Hosts.Projects];

    [Theory]
    [MemberData(nameof(Projects))]
    public async Task Cada_Proyecto_Usa_La_Implementacion_Compartida_CA_055_04(string project)
    {
        await using var host = Spec055Hosts.Start(project, new Dictionary<string, string> { [_networkKey] = _trustedNetwork }, _proxy);

        await GetHealthAsync(host);

        host.ResolvedIp.Should().Be(_client, "el proxy de la red de confianza puede reenviar la IP real");
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public async Task Sin_Configuracion_La_Ip_Es_La_De_La_Conexion_CA_055_04(string project)
    {
        await using var host = Spec055Hosts.Start(project, new Dictionary<string, string>(), _proxy);

        await GetHealthAsync(host);

        host.ResolvedIp.Should().Be(_proxy, "sin redes de confianza nadie puede reenviar una IP");
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void Con_Una_Red_Mal_Escrita_El_Arranque_Falla_Nombrando_La_Clave_CA_055_04(string project)
    {
        var start = () =>
        {
            var host = Spec055Hosts.Start(project, new Dictionary<string, string> { [_networkKey] = "no-es-un-cidr" });
            using var client = host.CreateClient();
        };

        start.Should().Throw<InvalidOperationException>().WithMessage("*ForwardedHeaders:KnownNetworks*");
    }

    private static async Task GetHealthAsync(Spec055Host host)
    {
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Forwarded-For", _client.ToString());
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
