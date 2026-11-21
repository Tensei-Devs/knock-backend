using System.Net;
using System.Text;
using AwesomeAssertions;
using Knock.Market.Api.Modules.Assistant.Data;
using Knock.Market.Api.Modules.Assistant.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetArchTest.Rules;
using Xunit;

namespace Knock.UnitTest.Assistant;

internal sealed class AiClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class AiHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();

    public List<string> Bodies { get; } = [];

    public int Calls => Bodies.Count;

    public AiHandler Enqueue(Func<HttpResponseMessage> response)
    {
        _responses.Enqueue(response);
        return this;
    }

    public static HttpResponseMessage Ok(string content, int input = 100, int output = 50)
    {
        var escaped = System.Text.Json.JsonSerializer.Serialize(content);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"choices\":[{\"message\":{\"content\":" + escaped + "}}],\"usage\":{\"prompt_tokens\":" + input + ",\"completion_tokens\":" + output + "}}", Encoding.UTF8, "application/json"),
        };
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
        return _responses.Count == 0 ? throw new HttpRequestException("no response queued") : _responses.Dequeue()();
    }
}

internal sealed class FakeConsumo : IConsumoIa
{
    public ConsumptionResult Result { get; set; } = ConsumptionResult.Reserved;

    public int Reserved { get; private set; }

    public int? Adjusted { get; private set; }

    public Task<ReservationResult> ReserveAsync(string user, DateOnly day, int amount, int userLimit, int globalLimit, CancellationToken cancellationToken)
    {
        Reserved = amount;
        return Task.FromResult(Result == ConsumptionResult.Reserved ? new ReservationResult(Result, new ConsumptionReservation(user, day, Guid.NewGuid())) : new ReservationResult(Result, null));
    }

    public Task<AdjustmentResult> AdjustAsync(ConsumptionReservation reservation, int actual, CancellationToken cancellationToken)
    {
        Adjusted = actual;
        return Task.FromResult(AdjustmentResult.Applied);
    }
}

internal sealed class FakeUso : IUsoIa
{
    public List<UsageRecord> Saved { get; } = [];

    public Task SaveAsync(UsageRecord usage, CancellationToken cancellationToken)
    {
        Saved.Add(usage);
        return Task.CompletedTask;
    }
}

internal sealed class FakeImages : IAiImageSource
{
    public HashSet<Guid> Allowed { get; } = [];

    public Task<byte[]?> GetReducedImageAsync(Guid fileId, string ownerId, CancellationToken cancellationToken) =>
        Task.FromResult(Allowed.Contains(fileId) && ownerId == "sub-sintetico" ? new byte[] { 1, 2, 3 } : null);
}

internal sealed class AiHarness
{
    public const string ValidAssistant = """{"categoria":"Plomería","mensaje":"Parece una fuga. Cierra la llave de paso.","pideFoto":true,"seguridad":null}""";

    public AiHarness(Action<AssistantOptions>? configure = null)
    {
        Options = new AssistantOptions { Habilitado = true };
        Options.AzureOpenAI.Endpoint = "https://openai.ejemplo.test";
        Options.AzureOpenAI.ModeloDespliegue = "gpt-5-mini";
        Options.AzureOpenAI.Clave = "clave-sintetica-no-real";
        configure?.Invoke(Options);
        var wrapped = Microsoft.Extensions.Options.Options.Create(Options);
#pragma warning disable CA2000 // el cliente vive lo que la prueba
        var http = new HttpClient(Azure) { BaseAddress = new Uri(Options.AzureOpenAI.Endpoint) };
#pragma warning restore CA2000
        Client = new AiClient(new ProveedorIa(http, wrapped, Clock), Images, Consumo, Uso, PlantillaIa.Load(), wrapped, Clock, NullLogger<AiClient>.Instance);
    }

    public AssistantOptions Options { get; }

    public AiHandler Azure { get; } = new();

    public FakeConsumo Consumo { get; } = new();

    public FakeUso Uso { get; } = new();

    public FakeImages Images { get; } = new();

    public AiClock Clock { get; } = new(new DateTimeOffset(2026, 11, 21, 18, 0, 0, TimeSpan.Zero));

    public AiClient Client { get; }

    public static SolicitudIa Request(string text = "Se me tapó el lavabo", params string[] files) => new(AiFunction.AsistenteKnock, "sub-sintetico", text, files);
}

public sealed class Spec024EsquemaTests
{
    [Theory]
    [InlineData("""{"categoria":"Plomería","mensaje":"Hola"}""")]
    [InlineData("""{"categoria":"Plomería","mensaje":"Hola","pideFoto":true,"seguridad":null,"extra":1}""")]
    [InlineData("""{"categoria":1,"mensaje":"Hola","pideFoto":true,"seguridad":null}""")]
    [InlineData("""{"categoria":"","mensaje":"Hola","pideFoto":true,"seguridad":null}""")]
    [InlineData("no es json")]
    public async Task Rechaza_Salida_Fuera_De_Esquema_CA_024_04(string content)
    {
        var h = new AiHarness();
        h.Azure.Enqueue(() => AiHandler.Ok(content));
        var result = await h.Client.GenerarAsync<AsistenteKnockSalida>(AiHarness.Request(), default);
        (result.Origen, result.MotivoRespaldo).Should().Be((AiOrigin.Template, "invalid_schema"));
        result.Valor.Should().NotBeNull();
    }

    [Fact]
    public async Task Acepta_Salida_Valida_Y_Recorta_Campos_Largos_CA_024_04()
    {
        var h = new AiHarness();
        var long600 = new string('a', 600);
        h.Azure.Enqueue(() => AiHandler.Ok($$"""{"categoria":"Plomería","mensaje":"{{long600}}","pideFoto":false,"seguridad":null}"""));
        var result = await h.Client.GenerarAsync<AsistenteKnockSalida>(AiHarness.Request(), default);
        result.Origen.Should().Be(AiOrigin.Provider);
        result.Recortado.Should().BeTrue();
        result.Valor!.Mensaje.Should().HaveLength(500);
    }

    [Fact]
    public void Plantillas_Y_Prompts_Validos_Al_Arrancar_CA_024_04()
    {
        var templates = PlantillaIa.Load();
        templates.Template<AsistenteKnockSalida>().Should().NotBeNull();
        templates.Template<ResumenChatSalida>().Should().NotBeNull();
        templates.Template<BorradorCotizacionSalida>().Should().NotBeNull();
        templates.Prompt(AiFunction.ResumenChat).Version.Should().Be("resumen_chat.v1");
        templates.Prompt(AiFunction.AsistenteKnock).Text.Should().Contain("gas");
    }
}

public sealed class Spec024LimitesTests
{
    [Fact]
    public async Task Tope_Por_Persona_Y_Global_Responden_Plantilla_CA_024_05()
    {
        var h = new AiHarness();
        h.Consumo.Result = ConsumptionResult.BudgetExhausted;
        var result = await h.Client.GenerarAsync<AsistenteKnockSalida>(AiHarness.Request(), default);
        (result.Origen, result.MotivoRespaldo).Should().Be((AiOrigin.Template, "budget_exhausted"));
        h.Azure.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Redis_Caido_No_Gasta_CA_024_14()
    {
        var h = new AiHarness();
        h.Consumo.Result = ConsumptionResult.LimitUnavailable;
        var result = await h.Client.GenerarAsync<AsistenteKnockSalida>(AiHarness.Request(), default);
        result.MotivoRespaldo.Should().Be("limit_unavailable");
        h.Azure.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Reserva_Maxima_Y_Ajuste_Con_Tokens_Reales_CA_024_07()
    {
        var h = new AiHarness();
        h.Azure.Enqueue(() => AiHandler.Ok(AiHarness.ValidAssistant, 120, 60));
        await h.Client.GenerarAsync<AsistenteKnockSalida>(AiHarness.Request(), default);
        h.Consumo.Reserved.Should().BeGreaterThan(h.Options.Limites.TokensSalidaMaximos);
        h.Consumo.Adjusted.Should().Be(180);
    }
}

public sealed class Spec024RespaldoTests
{
    [Fact]
    public async Task Apagado_Explicito_Y_Por_Omision_CA_024_08()
    {
        new AssistantOptions().Habilitado.Should().BeFalse();
        var h = new AiHarness(o => o.Habilitado = false);
        var result = await h.Client.GenerarAsync<AsistenteKnockSalida>(AiHarness.Request(), default);
        (result.Origen, result.MotivoRespaldo).Should().Be((AiOrigin.Template, "disabled"));
        h.Azure.Calls.Should().Be(0);
        h.Consumo.Reserved.Should().Be(0);
    }

    [Fact]
    public async Task Proveedor_No_Disponible_CA_024_09()
    {
        var h = new AiHarness();
        h.Azure.Enqueue(() => new HttpResponseMessage(HttpStatusCode.InternalServerError)).Enqueue(() => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var result = await h.Client.GenerarAsync<AsistenteKnockSalida>(AiHarness.Request(), default);
        (result.Origen, result.MotivoRespaldo).Should().Be((AiOrigin.Template, "unavailable"));
        result.Valor.Should().NotBeNull();
        h.Azure.Calls.Should().Be(2);
        h.Consumo.Adjusted.Should().Be(0);
    }

    [Fact]
    public async Task Sin_Reintento_En_401_CA_024_10()
    {
        var h = new AiHarness();
        h.Azure.Enqueue(() => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        (await h.Client.GenerarAsync<AsistenteKnockSalida>(AiHarness.Request(), default)).MotivoRespaldo.Should().Be("unavailable");
        h.Azure.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Cortacircuito_Tras_Cinco_Fallos_CA_024_09()
    {
        var h = new AiHarness();
        for (var i = 0; i < 5; i++)
        {
            h.Azure.Enqueue(() => new HttpResponseMessage(HttpStatusCode.Unauthorized));
            await h.Client.GenerarAsync<AsistenteKnockSalida>(AiHarness.Request(), default);
        }

        h.Azure.Enqueue(() => AiHandler.Ok(AiHarness.ValidAssistant));
        (await h.Client.GenerarAsync<AsistenteKnockSalida>(AiHarness.Request(), default)).Origen.Should().Be(AiOrigin.Template);
        h.Azure.Calls.Should().Be(5);
        h.Clock.Now += TimeSpan.FromSeconds(61);
        (await h.Client.GenerarAsync<AsistenteKnockSalida>(AiHarness.Request(), default)).Origen.Should().Be(AiOrigin.Provider);
    }

    [Fact]
    public async Task Sin_Credencial_No_Hay_Red_CA_024_10()
    {
        var h = new AiHarness(o => o.AzureOpenAI.Clave = null);
        (await h.Client.GenerarAsync<AsistenteKnockSalida>(AiHarness.Request(), default)).MotivoRespaldo.Should().Be("unavailable");
        h.Azure.Calls.Should().Be(0);
    }
}

public sealed class Spec024FotosTests
{
    [Theory]
    [InlineData("https://ejemplo.test/foto.jpg")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public async Task Rechaza_Imagen_No_Permitida_CA_024_03(string file)
    {
        var h = new AiHarness();
        var result = await h.Client.GenerarAsync<AsistenteKnockSalida>(AiHarness.Request("hola", file), default);
        (result.Origen, result.MotivoRespaldo).Should().Be((AiOrigin.Template, "image_rejected"));
        h.Azure.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Envia_Hasta_Tres_Imagenes_Permitidas_CA_024_03()
    {
        var h = new AiHarness();
        var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToList();
        ids.ForEach(id => h.Images.Allowed.Add(id));
        h.Azure.Enqueue(() => AiHandler.Ok(AiHarness.ValidAssistant));
        var result = await h.Client.GenerarAsync<AsistenteKnockSalida>(AiHarness.Request("hola", [.. ids.Select(id => id.ToString())]), default);
        result.Origen.Should().Be(AiOrigin.Provider);
        h.Azure.Bodies[0].Split("data:image/jpeg", StringSplitOptions.None).Should().HaveCount(4);
    }
}

public sealed class Spec024UsoTests
{
    [Fact]
    public async Task Registra_Uso_Sin_Contenido_CA_024_11()
    {
        var h = new AiHarness();
        h.Azure.Enqueue(() => AiHandler.Ok(AiHarness.ValidAssistant, 100, 40));
        await h.Client.GenerarAsync<AsistenteKnockSalida>(AiHarness.Request("texto secreto sintetico"), default);
        var usage = h.Uso.Saved.Should().ContainSingle().Which;
        (usage.Function, usage.Model, usage.PromptVersion, usage.InputTokens, usage.OutputTokens, usage.Origin, usage.FallbackReason).Should().Be(("asistente_knock", "gpt-5-mini", "asistente_knock.v1", 100, 40, "proveedor", null));
        typeof(UsageRecord).GetProperties().Select(p => p.Name).Should().NotContain(["Text", "Content", "Output", "Prompt"]);
    }

    [Fact]
    public async Task Registra_Plantilla_Con_Motivo_CA_024_11()
    {
        var h = new AiHarness(o => o.Habilitado = false);
        await h.Client.GenerarAsync<AsistenteKnockSalida>(AiHarness.Request(), default);
        h.Uso.Saved.Single().Should().Match<UsageRecord>(u => u.Origin == "plantilla" && u.Model == "plantilla" && u.FallbackReason == "disabled" && u.InputTokens == 0);
    }
}

public sealed class Spec024PrivacidadClienteTests
{
    [Fact]
    public async Task Sin_Identificadores_En_La_Peticion_CA_024_16()
    {
        var h = new AiHarness();
        h.Azure.Enqueue(() => AiHandler.Ok(AiHarness.ValidAssistant));
        await h.Client.GenerarAsync<AsistenteKnockSalida>(AiHarness.Request("Mi teléfono es 55 5555 1234 y mi correo persona.prueba@ejemplo.com, se tapó el lavabo"), default);
        h.Azure.Bodies[0].Should().NotContainAny("sub-sintetico", "5555 1234", "persona.prueba@ejemplo.com", "sig=", "sv=").And.Contain("lavabo");
    }

    [Fact]
    public async Task Sin_Valores_Censurados_En_El_Resultado_CA_024_13()
    {
        var h = new AiHarness(o => o.Habilitado = false);
        var result = await h.Client.GenerarAsync<AsistenteKnockSalida>(AiHarness.Request("Llama al 55 5555 1234"), default);
        result.TiposCensurados.Should().ContainSingle().Which.Type.Should().Be("phone");
        h.Uso.Saved.Select(u => u.ToString()).Should().NotContainMatch("*5555*");
    }

    [Fact]
    public async Task Texto_Largo_Se_Filtra_Completo_Y_Luego_Se_Recorta_CA_024_16()
    {
        var h = new AiHarness();
        h.Azure.Enqueue(() => AiHandler.Ok(AiHarness.ValidAssistant));
        var result = await h.Client.GenerarAsync<AsistenteKnockSalida>(AiHarness.Request(new string('a', 4001)), default);
        result.Recortado.Should().BeTrue();
        h.Azure.Bodies[0].Should().NotContain(new string('a', 4001));
    }
}

public sealed class Spec024ArquitecturaTests
{
    [Fact]
    public void Un_Solo_Punto_De_Acceso_CA_024_12()
    {
        var assembly = typeof(AiClient).Assembly;
        assembly.GetReferencedAssemblies().Select(name => name.Name).Should().NotContain("Azure.AI.OpenAI");
        Types.InAssembly(assembly).That().ResideInNamespace("Knock.Market.Api").GetTypes().Should().NotBeEmpty();
        var result = Types.InAssembly(assembly).That().DoNotResideInNamespace("Knock.Market.Api.Modules.Assistant").And().DoNotResideInNamespace("Knock.UnitTest")
            .ShouldNot().HaveDependencyOnAny("Knock.Market.Api.Modules.Assistant.Services.IProveedorIa", "Knock.Market.Api.Modules.Assistant.Services.ProveedorIa").GetResult();
        result.FailingTypes.Should().BeNullOrEmpty();
    }
}
