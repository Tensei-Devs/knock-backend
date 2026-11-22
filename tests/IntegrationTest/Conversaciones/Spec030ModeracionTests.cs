using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Knock.Contracts.Moderation;
using Xunit;

namespace Knock.IntegrationTest.Conversaciones;

/// <summary>Spec 030, R-13 y CA-07: la moderación previa al guardado falla cerrada. Solo datos ficticios.</summary>
public sealed class Spec030ModeracionTests
{
    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    [Fact]
    public async Task Texto_Rechazado_Responde_422_Y_No_Se_Guarda_CA_030_07()
    {
        await using var scenario = await ConversacionesScenario.StartAsync();
        var copy = await scenario.CopyAsync();
        scenario.Moderator.Outcome = ModerationOutcome.Rejected;

        using var response = await scenario.SendAsync(copy, scenario.Client, "un texto que la moderación rechaza");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeAsync(response)).Should().Be("content_rejected");
        scenario.Moderator.Calls.Should().Be(1);
        (await scenario.MessagesAsync()).Should().BeEmpty();
        (await scenario.OutboxAsync()).Should().BeEmpty("sin mensaje no hay aviso en el buzón");
        (await scenario.ConversationAsync())["noLeidosTecnico"].ToInt32().Should().Be(0);
    }

    [Fact]
    public async Task Moderacion_Caida_Responde_503_Y_No_Se_Guarda_CA_030_07()
    {
        await using var scenario = await ConversacionesScenario.StartAsync();
        var copy = await scenario.CopyAsync();
        scenario.Moderator.Outcome = ModerationOutcome.Unavailable;

        using var unavailable = await scenario.SendAsync(copy, scenario.Client, "hola");
        scenario.Moderator.Throws = true;
        using var throws = await scenario.SendAsync(copy, scenario.Client, "hola");

        foreach (var response in new[] { unavailable, throws })
        {
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            (await CodeAsync(response)).Should().Be("moderation_unavailable");
        }

        (await scenario.MessagesAsync()).Should().BeEmpty();
        (await scenario.OutboxAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_Moderacion_Configurada_Nada_Se_Guarda_CA_030_07()
    {
        await using var scenario = await ConversacionesScenario.StartAsync();
        var copy = await scenario.CopyAsync(defaultModerator: true);

        using var response = await scenario.SendAsync(copy, scenario.Client, "hola");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, "mientras no exista la moderación de la spec 031, el servicio falla cerrado");
        (await CodeAsync(response)).Should().Be("moderation_unavailable");
        (await scenario.MessagesAsync()).Should().BeEmpty();
    }
}
