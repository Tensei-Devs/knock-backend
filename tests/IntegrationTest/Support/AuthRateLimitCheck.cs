extern alias IdentityApi;

using System.Net;
using AwesomeAssertions;

namespace Knock.IntegrationTest.Support;

/// <summary>Comprobación común del límite de peticiones de los endpoints de autenticación (3 por minuto).</summary>
internal static class AuthRateLimitCheck
{
    /// <summary>Con el límite en 3, envía cuatro peticiones inválidas: las tres primeras dan 401 y la cuarta 429.</summary>
    public static async Task AssertFourthRequestIsLimitedAsync(Func<HttpClient, Task<HttpResponseMessage>> sendInvalidRequest)
    {
        ArgumentNullException.ThrowIfNull(sendInvalidRequest);
        using var factory = await IdentityApiFactory.CreateAsync(new Dictionary<string, string?>
        {
            ["RateLimiting:Auth:PermitLimit"] = "3",
            ["RateLimiting:Auth:Window"] = "00:01:00",
        });
        using var client = factory.CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 4; i++)
        {
            using var response = await sendInvalidRequest(client);
            statuses.Add(response.StatusCode);
        }

        statuses.Take(3).Should().OnlyContain(status => status == HttpStatusCode.Unauthorized);
        statuses[3].Should().Be(HttpStatusCode.TooManyRequests);
    }
}
