using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace Knock.IntegrationTest.Conversaciones;

/// <summary>Spec 030, R-11 y CA-05: 30 mensajes por minuto por persona, con el contador compartido entre réplicas. Solo datos ficticios.</summary>
public sealed class Spec030LimiteTests
{
    [Fact]
    public async Task Corta_En_El_Mensaje_31_Con_Dos_Replicas_CA_030_05()
    {
        await using var scenario = await ConversacionesScenario.StartAsync();
        var replicaA = await scenario.CopyAsync();
        var replicaB = await scenario.CopyAsync();

        for (var i = 0; i < 30; i++)
        {
            using var allowed = await scenario.SendAsync(i % 2 == 0 ? replicaA : replicaB, scenario.Client, $"mensaje {i}");
            allowed.StatusCode.Should().Be(HttpStatusCode.Created, $"el mensaje {i + 1} cabe en el límite");
        }

        using var limitedA = await scenario.SendAsync(replicaA, scenario.Client, "el 31");
        using var limitedB = await scenario.SendAsync(replicaB, scenario.Client, "el 31 por la otra réplica");
        using var otherPerson = await scenario.SendAsync(replicaA, scenario.Technician, "otra persona, otro contador");

        foreach (var response in new[] { limitedA, limitedB })
        {
            response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            response.Headers.GetValues("Retry-After").Should().ContainSingle().Which.Should().MatchRegex("^[1-9][0-9]*$");
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            problem.RootElement.GetProperty("code").GetString().Should().Be("message_limit");
        }

        otherPerson.StatusCode.Should().Be(HttpStatusCode.Created, "el límite es por persona");
        (await scenario.MessagesAsync()).Should().HaveCount(31, "30 del cliente y 1 del técnico; ninguno de los rechazados se guardó");
        scenario.Moderator.Calls.Should().Be(31);
    }
}
