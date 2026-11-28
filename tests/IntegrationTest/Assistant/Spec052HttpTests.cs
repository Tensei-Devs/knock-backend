extern alias MarketApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Modules.Assistant.Data;
using MarketApi::Knock.Market.Api.Modules.Assistant.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Knock.IntegrationTest.Assistant;

/// <summary>
/// Spec 052 sobre el canal HTTP de ASI-01 a ASI-06: sesión, rol, forma del cuerpo, 413, 429, 404 y registros sin datos personales. Host sin contenedores: la IA, las cuentas, la conversación, la
/// referencia de mercado y las bitácoras son dobles (la IA real queda detrás del interruptor <c>Asistente:Habilitado</c>, apagado); la prueba con MongoDB real está en <c>Spec052BitacoraMongoTests</c>.
/// </summary>
public sealed class Spec052HttpTests
{
    private const string _solicitud = "665f1f77bcf86cd799439012";
    private const string _bitacoraRoute = "/api/v1/asistente/bitacora/" + _solicitud;
    private const string _validLog = """{"sabe":["a"],"porConfirmar":["b"],"queLlevar":["c"]}""";
    private const string _conversationBody = """{"conversacionId":"665f1f77bcf86cd799439011"}""";

    public static TheoryData<string, string, string?> Rutas => new()
    {
        { "POST", "/api/v1/asistente/conversaciones", """{"mensaje":"Gotea una llave"}""" },
        { "POST", "/api/v1/asistente/resumen-chat", _conversationBody },
        { "POST", "/api/v1/asistente/borrador-cotizacion", _conversationBody },
        { "GET", "/api/v1/asistente/rango-precio?oficio=plomeria&tipoTrabajo=reparacion", null },
        { "PUT", _bitacoraRoute, _validLog },
        { "GET", _bitacoraRoute, null },
    };

    public static TheoryData<string, string, string?> RutasDeTecnico => new()
    {
        { "POST", "/api/v1/asistente/resumen-chat", _conversationBody },
        { "POST", "/api/v1/asistente/borrador-cotizacion", _conversationBody },
        { "GET", "/api/v1/asistente/rango-precio?oficio=plomeria&tipoTrabajo=reparacion", null },
        { "PUT", _bitacoraRoute, _validLog },
        { "GET", _bitacoraRoute, null },
    };

    private static MarketApiFactory Crear(bool technician = true, DobleIa? ia = null, CapturingLoggerProvider? logs = null, bool realAi = false) => MarketApiFactory.CreateOffline(
        services =>
        {
            if (!realAi)
            {
                services.AddSingleton<IAiClient>(ia ?? new DobleIa());
            }

            services.AddSingleton<IAsistenteCuentas>(new DobleCuentas(technician));
            services.AddSingleton<IConversacionesAsistente>(new DobleConversaciones());
            services.AddSingleton<ISolicitudesAsistente>(new DobleSolicitudes());
            services.AddSingleton<IReferenciaMercado>(new DobleReferencia());
            services.AddSingleton<IBitacoraStore>(new DobleBitacoras());
            if (logs is not null)
            {
                services.AddLogging(builder => builder.AddProvider(logs));
            }
        },
        new Dictionary<string, string?> { ["Mongo:Databases:Assistant"] = "market_offline" });

    private static HttpClient Autenticado(MarketApiFactory factory, string? sub = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", GatewayTestSupport.IssueToken(sub: sub ?? Guid.NewGuid().ToString("D"), key: Convert.FromBase64String(MarketApiFactory.SigningKeyBase64)));
        return client;
    }

    private static async Task<HttpResponseMessage> Enviar(HttpClient client, HttpMethod method, string route, string? json = null)
    {
        using var request = new HttpRequestMessage(method, route);
        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(request);
    }

    [Theory]
    [MemberData(nameof(Rutas))]
    public async Task Sin_Token_401_CA_052_02_CA_052_06_CA_052_10_CA_052_14_CA_052_19_CA_052_25(string method, string route, string? json)
    {
        using var factory = Crear();
        using var anonimo = factory.CreateClient();

        using var response = await Enviar(anonimo, new HttpMethod(method), route, json);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(RutasDeTecnico))]
    public async Task Sin_Perfil_De_Tecnico_403_CA_052_07_CA_052_11_CA_052_15_CA_052_20_CA_052_26(string method, string route, string? json)
    {
        using var factory = Crear(technician: false);
        using var client = Autenticado(factory);

        using var response = await Enviar(client, new HttpMethod(method), route, json);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("not_a_technician");
    }

    [Fact]
    public async Task Turno_Valido_Responde_200_Como_Sugerencia_CA_052_01()
    {
        using var factory = Crear();
        using var client = Autenticado(factory);

        using var response = await Enviar(client, HttpMethod.Post, "/api/v1/asistente/conversaciones", """{"mensaje":"Gotea una llave"}""");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("generadoPorIa").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Resumen_Y_Borrador_Validos_Responden_200_CA_052_05_CA_052_09()
    {
        using var factory = Crear();
        using var client = Autenticado(factory);

        using var summary = await Enviar(client, HttpMethod.Post, "/api/v1/asistente/resumen-chat", _conversationBody);
        using var draft = await Enviar(client, HttpMethod.Post, "/api/v1/asistente/borrador-cotizacion", _conversationBody);

        summary.StatusCode.Should().Be(HttpStatusCode.OK);
        draft.StatusCode.Should().Be(HttpStatusCode.OK);
        (await draft.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("generadoPorIa").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Rango_De_Precio_Valido_200_CA_052_13()
    {
        using var factory = Crear();
        using var client = Autenticado(factory);

        using var response = await Enviar(client, HttpMethod.Get, "/api/v1/asistente/rango-precio?oficio=plomeria&tipoTrabajo=reparacion");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("minimo").GetDecimal().Should().Be(300);
    }

    [Fact]
    public async Task Bitacora_Se_Crea_201_Se_Reemplaza_200_Y_Se_Lee_CA_052_18_CA_052_24()
    {
        using var factory = Crear();
        using var client = Autenticado(factory);

        using var first = await Enviar(client, HttpMethod.Put, _bitacoraRoute, _validLog);
        using var second = await Enviar(client, HttpMethod.Put, _bitacoraRoute, _validLog);
        using var read = await Enviar(client, HttpMethod.Get, _bitacoraRoute);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sabe")[0].GetString().Should().Be("a");
    }

    [Fact]
    public async Task Cuerpo_Ilegible_400_Sin_Guardar_CA_052_04_CA_052_22()
    {
        using var factory = Crear();
        using var client = Autenticado(factory);

        using var turn = await Enviar(client, HttpMethod.Post, "/api/v1/asistente/conversaciones", "{no es json");
        using var save = await Enviar(client, HttpMethod.Put, _bitacoraRoute, "{no es json");
        using var read = await Enviar(client, HttpMethod.Get, _bitacoraRoute);

        turn.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        save.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        read.StatusCode.Should().Be(HttpStatusCode.NotFound, "no se guardó nada");
    }

    [Fact]
    public async Task Valores_Invalidos_422_Sin_Repetir_El_Valor_CA_052_03_CA_052_08_CA_052_12_CA_052_16_CA_052_21_CA_052_27()
    {
        using var factory = Crear();
        using var client = Autenticado(factory);
        const string secret = "55-5555-1234-secreto";

        var responses = new[]
        {
            await Enviar(client, HttpMethod.Post, "/api/v1/asistente/conversaciones", $$"""{"mensaje":"{{new string('x', 1001)}}{{secret}}"}"""),
            await Enviar(client, HttpMethod.Post, "/api/v1/asistente/resumen-chat", $$"""{"conversacionId":"{{secret}}"}"""),
            await Enviar(client, HttpMethod.Post, "/api/v1/asistente/borrador-cotizacion", $$"""{"conversacionId":"{{secret}}"}"""),
            await Enviar(client, HttpMethod.Get, "/api/v1/asistente/rango-precio?oficio=" + Uri.EscapeDataString(secret + "<>") + "&tipoTrabajo=x"),
            await Enviar(client, HttpMethod.Put, _bitacoraRoute, $$"""{"sabe":["{{secret}}\u0001"],"porConfirmar":["b"],"queLlevar":["c"]}"""),
            await Enviar(client, HttpMethod.Get, "/api/v1/asistente/bitacora/no-es-un-id"),
        };

        foreach (var response in responses)
        {
            using (response)
            {
                response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
                var text = await response.Content.ReadAsStringAsync();
                text.Should().Contain("errors");
                text.Should().NotContain("5555");
            }
        }
    }

    [Fact]
    public async Task Bitacora_Inexistente_404_Log_Not_Found_CA_052_28()
    {
        using var factory = Crear();
        using var client = Autenticado(factory);

        using var response = await Enviar(client, HttpMethod.Get, _bitacoraRoute);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("log_not_found");
    }

    [Fact]
    public async Task Sin_IA_Configurada_503_Y_No_Se_Envia_Nada_CA_052_29()
    {
        using var factory = Crear(realAi: true); // cliente real con Asistente:Habilitado apagado: plantilla «disabled», sin red
        using var client = Autenticado(factory);

        using var response = await Enviar(client, HttpMethod.Post, "/api/v1/asistente/conversaciones", """{"mensaje":"Gotea una llave"}""");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("ai_unavailable");
    }

    [Fact]
    public async Task Tope_Diario_429_Con_Retry_After_CA_052_30()
    {
        using var factory = Crear(ia: new DobleIa { Reason = FallbackReasons.BudgetExhausted });
        using var client = Autenticado(factory);

        using var response = await Enviar(client, HttpMethod.Post, "/api/v1/asistente/conversaciones", """{"mensaje":"Gotea una llave"}""");

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter.Should().NotBeNull();
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("ai_daily_limit_reached");
    }

    [Fact]
    public async Task Persona_Del_Token_No_Del_Cuerpo_CA_052_36()
    {
        var ia = new DobleIa();
        using var factory = Crear(ia: ia);
        using var client = Autenticado(factory, "persona-del-token");

        using var response = await Enviar(client, HttpMethod.Post, "/api/v1/asistente/conversaciones", """{"mensaje":"Gotea","usuarioId":"otra-persona","sub":"otra-persona"}""");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ia.Requests.Should().ContainSingle().Which.UsuarioId.Should().Be("persona-del-token");
    }

    [Fact]
    public async Task Registros_Sin_Datos_Personales_CA_052_37()
    {
        using var logs = new CapturingLoggerProvider();
        using var factory = Crear(logs: logs);
        using var client = Autenticado(factory);

        using var ok = await Enviar(client, HttpMethod.Post, "/api/v1/asistente/conversaciones", """{"mensaje":"Llámame al 55 5555 1234 en Calle Hidalgo 123"}""");
        using var invalid = await Enviar(client, HttpMethod.Put, _bitacoraRoute, """{"sabe":["Hidalgo 123 55 5555 1234\u0001"],"porConfirmar":["b"],"queLlevar":["c"]}""");

        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        logs.AllMessages().Should().NotContain("5555").And.NotContain("Hidalgo");
        (await invalid.Content.ReadAsStringAsync()).Should().NotContain("Hidalgo");
    }

    [Fact]
    public async Task Cuerpo_Excesivo_413_Y_Limite_429_Con_Retry_After_CA_052_38()
    {
        using var factory = Crear();
        using var client = Autenticado(factory);

        using var large = await Enviar(client, HttpMethod.Post, "/api/v1/asistente/conversaciones", $$"""{"mensaje":"a","contexto":"{{new string('x', 9000)}}"}""");
        large.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);

        HttpResponseMessage? last = null;
        for (var i = 0; i < 12; i++)
        {
            last?.Dispose();
            last = await Enviar(client, HttpMethod.Post, "/api/v1/asistente/resumen-chat", _conversationBody);
        }

        using (last)
        {
            last!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            last.Headers.RetryAfter.Should().NotBeNull();
        }
    }
}
