using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Chat;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using StackExchange.Redis;
using Xunit;

namespace Knock.IntegrationTest.Conversaciones;

/// <summary>
/// Spec 030, T-06 (KNOCK-73): enviar un mensaje a una conversación (R-08 a R-14). MongoDB y Redis reales, Mercado y moderación de mentira, dos copias del servicio donde importa.
/// Solo datos ficticios.
/// </summary>
public sealed class Spec030EnvioTests
{
    private const string _hidden = "[dato de contacto oculto]";

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task Entrega_En_Vivo_O_Por_Buzon_CA_030_01()
    {
        await using var scenario = await ConversacionesScenario.StartAsync();
        var copy = await scenario.CopyAsync();
        var client = await scenario.ConnectAsync(copy, scenario.Client);
        var live = ConversacionesScenario.Listen(client, "mensajeConversacion");
        var counters = ConversacionesScenario.Listen(client, "noLeidos");
        (await ConversacionesScenario.InvokeAsync(client, "UnirseAConversacion", scenario.ConversationId)).GetProperty("ok").GetBoolean().Should().BeTrue();

        using var created = await scenario.SendAsync(copy, scenario.Technician, "Puedo ir mañana a las 10");

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("autorRol").GetString().Should().Be("tecnico");
        body.RootElement.GetProperty("texto").GetString().Should().Be("Puedo ir mañana a las 10");
        body.RootElement.GetProperty("contactoOculto").GetBoolean().Should().BeFalse();
        Guid.Parse(body.RootElement.GetProperty("mensajeId").GetString()!).Version.Should().Be(7);
        body.RootElement.TryGetProperty("autorId", out _).Should().BeFalse("el sub de quien escribe nunca sale");

        var message = await ConversacionesScenario.NextAsync(live);
        message.GetProperty("conversacionId").GetString().Should().Be(scenario.ConversationId);
        message.GetProperty("texto").GetString().Should().Be("Puedo ir mañana a las 10");
        var unread = await ConversacionesScenario.NextAsync(counters);
        unread.GetProperty("conversacionId").GetString().Should().Be(scenario.ConversationId);
        unread.GetProperty("noLeidos").GetInt32().Should().Be(1);
        unread.GetProperty("total").GetInt32().Should().Be(1);

        // El cliente no tiene conexión: solo queda el aviso del buzón, sin texto.
        using var second = await scenario.SendAsync(copy, scenario.Technician, "¿Le queda bien?");
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var saved = await scenario.MessagesAsync();
        saved.Should().HaveCount(2);
        var outbox = await scenario.OutboxAsync();
        outbox.Should().HaveCount(2).And.OnlyContain(o => o["tipo"].AsString == "MensajeNuevo");
        outbox.Should().OnlyContain(o => o["datos"].AsBsonDocument["destinatarioUsuarioId"].AsString == scenario.Client);
        outbox.Should().OnlyContain(o => o["datos"].AsBsonDocument["conversacionId"].AsString == scenario.ConversationId);
        outbox.Select(o => o.ToJson()).Should().NotContain(json => json.Contains("mañana", StringComparison.Ordinal) || json.Contains("Le queda", StringComparison.Ordinal), "el buzón no lleva texto");

        var conversation = await scenario.ConversationAsync();
        conversation["noLeidosCliente"].ToInt32().Should().Be(2);
        conversation["noLeidosTecnico"].ToInt32().Should().Be(0);
        conversation["ultimoMensaje"]["texto"].AsString.Should().Be("¿Le queda bien?");
        conversation["ultimoMensaje"]["autorRol"].AsString.Should().Be("tecnico");
    }

    [Fact]
    public async Task Valida_Longitud_Del_Texto_CA_030_02()
    {
        await using var scenario = await ConversacionesScenario.StartAsync();
        var copy = await scenario.CopyAsync();

        using var one = await scenario.SendAsync(copy, scenario.Client, "a");
        using var thousand = await scenario.SendAsync(copy, scenario.Technician, new string('b', 1_000));
        one.StatusCode.Should().Be(HttpStatusCode.Created);
        thousand.StatusCode.Should().Be(HttpStatusCode.Created);
        (await scenario.MessagesAsync()).Should().HaveCount(2);

        foreach (var invalid in new[] { string.Empty, "   ", new string('c', 1_001), "hola\u0007mundo", "hola\u0000" })
        {
            using var rejected = await scenario.SendAsync(copy, scenario.Client, invalid);
            rejected.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            var raw = await rejected.Content.ReadAsStringAsync();
            using var problem = JsonDocument.Parse(raw);
            problem.RootElement.GetProperty("code").GetString().Should().Be("validation_failed");
            problem.RootElement.GetProperty("errors").TryGetProperty("texto", out _).Should().BeTrue();
            if (invalid.Length > 3)
            {
                ProblemBody.Reasons(raw).Should().NotContain(invalid, "el texto enviado nunca vuelve en el error");
            }
        }

        (await scenario.MessagesAsync()).Should().HaveCount(2, "lo inválido no se guarda");
        scenario.Moderator.Calls.Should().Be(2, "lo inválido ni siquiera llega a la moderación");
    }

    [Fact]
    public async Task Un_Salto_De_Linea_Es_Valido_Y_Se_Recorta_CA_030_02()
    {
        await using var scenario = await ConversacionesScenario.StartAsync();
        var copy = await scenario.CopyAsync();

        using var response = await scenario.SendAsync(copy, scenario.Client, "  línea uno\nlínea dos  ");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await scenario.MessagesAsync()).Single()["texto"].AsString.Should().Be("línea uno\nlínea dos");
    }

    [Fact]
    public async Task Ajeno_Recibe_404_Y_Nada_Se_Guarda_CA_030_03()
    {
        await using var scenario = await ConversacionesScenario.StartAsync();
        var copy = await scenario.CopyAsync();

        using var outsider = await scenario.SendAsync(copy, scenario.Outsider, "hola");
        using var missing = await scenario.SendAsync(copy, scenario.Client, "hola", ObjectId.GenerateNewId().ToString());
        using var malformed = await scenario.SendAsync(copy, scenario.Client, "hola", "no-es-un-id");

        foreach (var response in new[] { outsider, missing, malformed })
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await ProblemAsync(response)).GetProperty("code").GetString().Should().Be("not_found");
        }

        (await scenario.MessagesAsync()).Should().BeEmpty();
        scenario.Market.PermissionCalls.Should().Be(0, "a un ajeno se le niega con lo guardado, sin preguntar a Mercado");
        scenario.Moderator.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Oculta_Contacto_Antes_De_Elegir_CA_030_04()
    {
        await using var scenario = await ConversacionesScenario.StartAsync(chosen: false);
        var copy = await scenario.CopyAsync();

        using var phone = await scenario.SendAsync(copy, scenario.Technician, "llámame al 771 123 4567");
        using var mail = await scenario.SendAsync(copy, scenario.Client, "escríbeme a correo@ejemplo.com");

        phone.StatusCode.Should().Be(HttpStatusCode.Created);
        using var phoneBody = JsonDocument.Parse(await phone.Content.ReadAsStringAsync());
        phoneBody.RootElement.GetProperty("texto").GetString().Should().Be($"llámame al {_hidden}");
        phoneBody.RootElement.GetProperty("contactoOculto").GetBoolean().Should().BeTrue();
        using var mailBody = JsonDocument.Parse(await mail.Content.ReadAsStringAsync());
        mailBody.RootElement.GetProperty("texto").GetString().Should().Be($"escríbeme a {_hidden}");

        var saved = await scenario.MessagesAsync();
        saved.Should().HaveCount(2).And.OnlyContain(m => m["contactoOculto"].AsBoolean);
        saved.Select(m => m.ToJson()).Should().NotContain(json => json.Contains("123 4567", StringComparison.Ordinal) || json.Contains("correo@", StringComparison.Ordinal), "el texto original nunca se guarda");
        scenario.Moderator.LastText.Should().Be($"escríbeme a {_hidden}", "la moderación revisa el texto ya ocultado");
    }

    [Fact]
    public async Task No_Oculta_Con_El_Tecnico_Elegido_CA_030_04()
    {
        await using var scenario = await ConversacionesScenario.StartAsync(chosen: true);
        var copy = await scenario.CopyAsync();

        using var response = await scenario.SendAsync(copy, scenario.Client, "llámame al 771 123 4567");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("texto").GetString().Should().Be("llámame al 771 123 4567");
        body.RootElement.GetProperty("contactoOculto").GetBoolean().Should().BeFalse();
        (await scenario.ConversationAsync())["elegido"].AsBoolean.Should().BeTrue("el permiso vigente se guarda en la conversación");
    }

    [Theory]
    [InlineData("request_cancelled")]
    [InlineData("request_expired")]
    [InlineData("other_quote_chosen")]
    [InlineData("dismissed")]
    public async Task Conversacion_Cerrada_Solo_Lectura_CA_030_06(string closure)
    {
        await using var scenario = await ConversacionesScenario.StartAsync(open: false, closure: closure);
        var copy = await scenario.CopyAsync();

        using var response = await scenario.SendAsync(copy, scenario.Technician, "¿sigue en pie?");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await ProblemAsync(response);
        problem.GetProperty("code").GetString().Should().Be("conversation_closed");
        problem.GetProperty("motivoCierre").GetString().Should().Be(closure);
        (await scenario.MessagesAsync()).Should().BeEmpty();
        scenario.Moderator.Calls.Should().Be(0, "una conversación cerrada no se modera ni se guarda");
        var conversation = await scenario.ConversationAsync();
        conversation["estado"].AsString.Should().Be("cerrada");
        conversation["motivoCierre"].AsString.Should().Be(closure);
    }

    [Fact]
    public async Task Mercado_No_Responde_CA_030_09()
    {
        await using var scenario = await ConversacionesScenario.StartAsync();
        var copy = await scenario.CopyAsync();
        var deadMarket = await scenario.CopyAsync(new Dictionary<string, string?> { ["Services:Market:BaseUrl"] = "http://127.0.0.1:1/" });

        using var unreachable = await scenario.SendAsync(deadMarket, scenario.Client, "hola");
        scenario.Market.Permission = (_, _, _) => Task.FromResult(new PermissionAnswer(500));
        using var failing = await scenario.SendAsync(copy, scenario.Client, "hola");

        foreach (var response in new[] { unreachable, failing })
        {
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            (await ProblemAsync(response)).GetProperty("code").GetString().Should().Be("market_unavailable");
        }

        (await scenario.MessagesAsync()).Should().BeEmpty("sin permiso nunca se asume que la conversación está abierta");

        scenario.Permit(open: true, chosen: false);
        using var granted = await scenario.SendAsync(copy, scenario.Client, "hola");
        granted.StatusCode.Should().Be(HttpStatusCode.Created);
        var calls = scenario.Market.PermissionCalls;

        scenario.Market.Permission = (_, _, _) => Task.FromResult(new PermissionAnswer(500));
        using var cached = await scenario.SendAsync(copy, scenario.Client, "otra vez");

        cached.StatusCode.Should().Be(HttpStatusCode.Created, "con el permiso en caché de menos de 60 segundos se envía");
        scenario.Market.PermissionCalls.Should().Be(calls, "no volvió a preguntar a Mercado");
        (await scenario.MessagesAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task El_Permiso_En_Cache_Vence_A_Los_60_Segundos_CA_030_09()
    {
        await using var scenario = await ConversacionesScenario.StartAsync();
        var copy = await scenario.CopyAsync();
        using var first = await scenario.SendAsync(copy, scenario.Client, "hola");
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var redis = await RedisTestFixture.SharedAsync();
        await using var connection = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var server = connection.GetServers()[0];
        var key = (await server.KeysAsync(pattern: "conv:permiso:" + scenario.ConversationId + ":*").ToListAsync()).Single();

        var ttl = await connection.GetDatabase().KeyTimeToLiveAsync(key);
        var value = (string?)await connection.GetDatabase().StringGetAsync(key);

        ttl.Should().NotBeNull();
        ttl.Value.Should().BeGreaterThan(TimeSpan.FromSeconds(50)).And.BeLessThanOrEqualTo(TimeSpan.FromSeconds(60));
        value.Should().NotContain("hola", "el permiso en Redis no lleva texto de mensajes");
    }

    [Fact]
    public async Task Cada_Rol_Consulta_A_Mercado_Con_Lo_Suyo_CA_030_09()
    {
        await using var scenario = await ConversacionesScenario.StartAsync();
        var copy = await scenario.CopyAsync();
        var asked = new List<(string Sub, string? Technician)>();
        scenario.Market.Permission = (sub, _, technician) =>
        {
            asked.Add((sub, technician));
            return Task.FromResult(new PermissionAnswer(500));
        };

        using var fromClient = await scenario.SendAsync(copy, scenario.Client, "hola");
        using var fromTechnician = await scenario.SendAsync(copy, scenario.Technician, "hola");

        asked.Should().HaveCount(2);
        asked[0].Should().Be((scenario.Client, "tec-1"), "el cliente manda el tecnicoId del par");
        asked[1].Should().Be((scenario.Technician, (string?)null), "el técnico no lo manda: Mercado lo resuelve por su sub");
    }
}
