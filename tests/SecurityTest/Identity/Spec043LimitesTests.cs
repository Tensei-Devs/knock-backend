using System.Globalization;
using System.Net;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using Xunit;

namespace Knock.SecurityTest.Identity;

/// <summary>Spec 043, R-26 y CA-36: bloqueos y reportes responden 429 con <c>Retry-After</c> por encima del límite y 413 con un cuerpo excesivo. Host real en Production. Solo datos ficticios.</summary>
public class Spec043LimitesTests
{
    [Fact]
    public async Task BlockingOverTheHourlyLimit_Returns429WithRetryAfter_CA_043_36()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var person = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        var path = $"/api/v1/usuarios/{Guid.NewGuid():D}/bloqueo";
        for (var i = 0; i < 30; i++)
        {
            (await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Delete, path, bearer: person.AccessToken, remoteIp: "10.43.0.1")).Dispose();
        }

        using var limited = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Delete, path, bearer: person.AccessToken, remoteIp: "10.43.0.1");
        using var anotherPerson = await SecureHttp.SendAsync(hosts.IdentityClient, HttpMethod.Delete, path, bearer: (await hosts.Identity.SignInAsync(hosts.IdentityClient)).AccessToken, remoteIp: "10.43.0.1");

        await SecureHttp.AssertProblemAsync(limited, HttpStatusCode.TooManyRequests);
        int.Parse(limited.Headers.GetValues("Retry-After").Single(), CultureInfo.InvariantCulture).Should().BeInRange(1, 3600);
        anotherPerson.StatusCode.Should().Be(HttpStatusCode.NotFound, "el límite es por persona: otra persona no se ve afectada");
    }

    [Fact]
    public async Task ReportingOverTheHourlyLimit_Returns429WithRetryAfter_CA_043_36()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var person = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        var body = new { reportadoId = person.Sub, motivo = "otro" };
        for (var i = 0; i < 10; i++)
        {
            (await SecureHttp.PostJsonAsync(hosts.IdentityClient, "/api/v1/reportes", body, person.AccessToken, remoteIp: "10.43.0.2")).Dispose();
        }

        using var limited = await SecureHttp.PostJsonAsync(hosts.IdentityClient, "/api/v1/reportes", body, person.AccessToken, remoteIp: "10.43.0.2");

        await SecureHttp.AssertProblemAsync(limited, HttpStatusCode.TooManyRequests);
        int.Parse(limited.Headers.GetValues("Retry-After").Single(), CultureInfo.InvariantCulture).Should().BeInRange(1, 3600);
    }

    [Theory]
    [InlineData("/api/v1/reportes")]
    [InlineData("/api/v1/reportes/507f1f77bcf86cd799439011/apelacion")]
    public async Task OversizedBody_Returns413_CA_043_36(string path)
    {
        var hosts = await SecurityHosts.SharedAsync();
        var person = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        var json = $$"""{"campo":"{{new string('a', 20000)}}"}""";

        using var response = await SecureHttp.SendRawJsonAsync(hosts.IdentityClient, HttpMethod.Post, path, json, person.AccessToken, remoteIp: "10.9.9.9");

        await SecureHttp.AssertProblemAsync(response, HttpStatusCode.RequestEntityTooLarge);
    }
}
