using System.Net;
using AwesomeAssertions;
using Knock.IntegrationTest.Requests;
using Xunit;

namespace Knock.IntegrationTest.Tecnicos;

/// <summary>Spec 046, T-16 (R-52, CA-68): Mapa y Explorar son públicos y GET, así que su abuso se frena con 429 y <c>Retry-After</c> (60 por minuto por IP cada uno); no reciben cuerpo, de modo que no hay 413.</summary>
public sealed class Spec046MapaExplorarLimitesHttpTests
{
    private const int _perMinute = 60;

    [Theory]
    [InlineData("/api/v1/mapa/tecnicos?norte=19.5&sur=19.3&este=-99.0&oeste=-99.3&zoom=12")]
    [InlineData("/api/v1/explorar?filtro=todo")]
    public async Task Supera_60_Por_Minuto_Y_Responde_429_Con_RetryAfter_CA_046_68(string path)
    {
        await using var host = await Spec020Host.StartAsync();
        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? last = null;

        for (var i = 0; i <= _perMinute; i++)
        {
            last?.Dispose();
            last = await host.SendAsync(HttpMethod.Get, path);
            statuses.Add(last.StatusCode);
        }

        using (last)
        {
            statuses.Take(_perMinute).Should().OnlyContain(s => s == HttpStatusCode.OK);
            statuses[_perMinute].Should().Be(HttpStatusCode.TooManyRequests);
            last!.Headers.RetryAfter.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Cada_Ruta_Tiene_Su_Propio_Contador_CA_046_68()
    {
        await using var host = await Spec020Host.StartAsync();
        for (var i = 0; i < _perMinute; i++)
        {
            (await host.SendAsync(HttpMethod.Get, "/api/v1/mapa/tecnicos?norte=19.5&sur=19.3&este=-99.0&oeste=-99.3&zoom=12")).Dispose();
        }

        using var explore = await host.SendAsync(HttpMethod.Get, "/api/v1/explorar?filtro=todo");

        explore.StatusCode.Should().Be(HttpStatusCode.OK, "el agotamiento del mapa no gasta el cupo de Explorar");
    }
}
