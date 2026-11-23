using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>Spec 023 T-04: el límite de 5 registros por hora por persona se comparte entre réplicas (contador en Redis real).</summary>
public sealed class Spec023LimiteCompartidoTests
{
    [Fact]
    public async Task Limite_De_Lista_De_Espera_Se_Comparte_Entre_Replicas_CA_023_11()
    {
        var (replicaA, _) = await WaitlistHttp.CreateAsync();
        var (replicaB, _) = await WaitlistHttp.CreateAsync();
        using var hostA = replicaA;
        using var hostB = replicaB;
        var subject = Guid.NewGuid().ToString("D");
        using var clientA = WaitlistHttp.SignedIn(replicaA, subject);
        using var clientB = WaitlistHttp.SignedIn(replicaB, subject);

        for (var i = 0; i < 3; i++)
        {
            using var accepted = await clientA.PostAsJsonAsync(WaitlistHttp.Route, new { cp = $"9200{i}" });
            accepted.StatusCode.Should().Be(HttpStatusCode.Created);
        }
        for (var i = 3; i < 5; i++)
        {
            using var accepted = await clientB.PostAsJsonAsync(WaitlistHttp.Route, new { cp = $"9200{i}" });
            accepted.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        using var limitedA = await clientA.PostAsJsonAsync(WaitlistHttp.Route, new { cp = "92009" });
        using var limitedB = await clientB.PostAsJsonAsync(WaitlistHttp.Route, new { cp = "92009" });
        limitedA.StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "las 5 peticiones se contaron entre las dos réplicas");
        limitedB.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limitedB.Headers.RetryAfter.Should().NotBeNull();

        using var otherPerson = WaitlistHttp.SignedIn(replicaB);
        using var other = await otherPerson.PostAsJsonAsync(WaitlistHttp.Route, new { cp = "92009" });
        other.StatusCode.Should().Be(HttpStatusCode.Created, "el límite es por persona");
    }
}
