using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using MongoDB.Bson;
using Xunit;

namespace Knock.IntegrationTest.Conversaciones;

/// <summary>Spec 030, R-22 y CA-18: crear, enviar, ocultar y marcar leído no dejan texto de mensajes, texto ocultado, <c>sub</c> ni tokens en registros ni en el buzón. Solo datos ficticios.</summary>
public sealed class Spec030RegistrosTests
{
    private const string _plain = "hola, ¿a qué hora llega mañana?";
    private const string _withContact = "llámame al 771 123 4567 o a correo.secreto@ejemplo.com";

    [Fact]
    public async Task Sin_Datos_Sensibles_En_Registros_Ni_Buzon_CA_030_18()
    {
        // Serilog escribe en la consola: se captura antes de que la copia cree su host (el sumidero toma Console.Out al construirse).
        using var console = new StringWriter();
        var original = Console.Out;
        Console.SetOut(TextWriter.Synchronized(console));
        try
        {
            await RunAsync(console);
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static string ErrorHead(string text)
    {
        var at = text.IndexOf("UnhandledException", StringComparison.Ordinal);
        var from = text.LastIndexOf("{\"@t\"", at < 0 ? 0 : at, StringComparison.Ordinal);
        return from < 0 ? "sin error registrado" : text[from..Math.Min(text.Length, from + 900)];
    }

    private static async Task RunAsync(StringWriter console)
    {
        await using var scenario = await ConversacionesScenario.StartAsync();
        var copy = await scenario.CopyAsync();

        using var plain = await scenario.SendAsync(copy, scenario.Client, _plain);
        using var hidden = await scenario.SendAsync(copy, scenario.Client, _withContact);
        using var client = ConversacionesScenario.ClientFor(copy, scenario.Technician);
        using var read = await client.PostAsJsonAsync(new Uri($"/api/v1/conversaciones/{scenario.ConversationId}/leido", UriKind.Relative), new { hasta = DateTime.UtcNow.AddMinutes(1) });

        plain.StatusCode.Should().Be(HttpStatusCode.Created);
        hidden.StatusCode.Should().Be(HttpStatusCode.Created);
        read.StatusCode.Should().BeOneOf([HttpStatusCode.NoContent, HttpStatusCode.OK], "{0}", ErrorHead(console.ToString()));

        var logs = console.ToString();
        logs.Should().NotBeEmpty("el servicio sí registró peticiones; sin eso la prueba no comprobaría nada");
        var outbox = string.Join("\n", (await scenario.OutboxAsync()).Select(document => document.ToJson()));
        (await scenario.OutboxAsync()).Should().NotBeEmpty("el envío deja su aviso en el buzón");
        foreach (var forbidden in new[] { _plain, "771 123 4567", "correo.secreto", "a qué hora llega" })
        {
            logs.Should().NotContain(forbidden, "los registros no llevan texto ni contacto");
            outbox.Should().NotContain(forbidden, "el aviso del buzón no lleva texto");
        }

        logs.Should().NotContain(scenario.Client).And.NotContain(scenario.Technician, "los registros no llevan sub");

        logs.Should().NotContain("Bearer").And.NotContain("eyJ");
    }
}
