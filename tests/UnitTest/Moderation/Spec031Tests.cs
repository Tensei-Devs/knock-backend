using System.Net;
using AwesomeAssertions;
using Knock.Contracts.Moderation;
using Knock.Market.Api.Modules.Moderation.Data;
using Knock.Market.Api.Modules.Moderation.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Knock.UnitTest.Moderation;

/// <summary>Spec 031: moderación con Azure. Datos sintéticos; el servidor de Azure es un doble y ninguna prueba llama a la red.</summary>
public sealed class Spec031TextoTests
{
    [Fact]
    public async Task Rechaza_Por_Umbral_CA_031_01()
    {
        var h = new ModerationHarness();
        h.Azure.Enqueue(() => FakeAzureHandler.Ok(hate: 4));
        var verdict = await h.Moderator.ModerateTextAsync(ModerationHarness.Text("texto de prueba"), default);
        verdict.Decision.Should().Be(ModerationDecision.Rejected);
        verdict.Source.Should().Be(ModerationSource.Azure);
        verdict.Categories.Should().Equal("Hate");
        ModerationProblemMapper.Map(verdict, false, h.Clock.Now)!.Status.Should().Be(422);
    }

    [Fact]
    public async Task Aprueba_Texto_Limpio_CA_031_03()
    {
        var h = new ModerationHarness();
        h.Azure.Enqueue(() => FakeAzureHandler.Ok());
        (await h.Moderator.ModerateTextAsync(ModerationHarness.Text("Se me rompió la tubería del baño"), default)).Decision.Should().Be(ModerationDecision.Approved);
    }

    [Fact]
    public void Severidad_Igual_Al_Umbral_Se_Rechaza_CA_031_01()
    {
        var h = new ModerationHarness();
        h.Moderator.Evaluate(new SafetyAnalysis(new Dictionary<string, int> { ["Hate"] = 2, ["Violence"] = 2, ["Sexual"] = 0, ["SelfHarm"] = 0 })).Decision.Should().Be(ModerationDecision.Approved);
        var sexual = h.Moderator.Evaluate(new SafetyAnalysis(new Dictionary<string, int> { ["Hate"] = 0, ["Violence"] = 0, ["Sexual"] = 2, ["SelfHarm"] = 0 }));
        sexual.Decision.Should().Be(ModerationDecision.Rejected);
        sexual.Categories.Should().Equal("Sexual");
        h.Moderator.Evaluate(new SafetyAnalysis(new Dictionary<string, int> { ["Hate"] = 0, ["Violence"] = 4, ["Sexual"] = 0, ["SelfHarm"] = 0 })).Categories.Should().Equal("Violence");
    }

    [Fact]
    public async Task Texto_Demasiado_Largo_CA_031_12()
    {
        var h = new ModerationHarness();
        var verdict = await h.Moderator.ModerateTextAsync(ModerationHarness.Text(new string('a', 4001)), default);
        verdict.Decision.Should().Be(ModerationDecision.Rejected);
        verdict.ReasonCode.Should().Be("not_moderable");
        h.Azure.Calls.Should().Be(0);
        h.Azure.Enqueue(() => FakeAzureHandler.Ok());
        (await h.Moderator.ModerateTextAsync(ModerationHarness.Text(new string('a', 4000)), default)).Decision.Should().Be(ModerationDecision.Approved);
    }
}

public sealed class Spec031ListaLocalTests
{
    [Theory]
    [InlineData("eres un PENDEJO")]
    [InlineData("eres un p3nd3j0")]
    [InlineData("eres un pendeeeejo")]
    [InlineData("eres un p u t o")]
    [InlineData("qué pinche mierda es esto")]
    public async Task Rechaza_Sin_Llamar_A_Azure_CA_031_02(string text)
    {
        var h = new ModerationHarness();
        var verdict = await h.Moderator.ModerateTextAsync(ModerationHarness.Text(text), default);
        verdict.Decision.Should().Be(ModerationDecision.Rejected);
        verdict.Source.Should().Be(ModerationSource.LocalList);
        h.Azure.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData("el computador está en la reputación")]
    [InlineData("necesito un plomero, el tubo gotea")]
    public void Coincide_Solo_Por_Palabra_Completa_CA_031_02(string text) => ListaLocal.LoadEmbedded().Contiene(text).Should().BeFalse();

    [Fact]
    public void Listas_Identicas_Y_Validas_CA_031_18()
    {
        var list = ListaLocal.LoadEmbedded();
        list.Fingerprint.Should().HaveLength(64);
        list.Terms.Should().OnlyHaveUniqueItems();
        ListaLocal.Parse("hola\nadios\n").Terms.Should().HaveCount(2);
        foreach (var bad in new[] { "hola\n\nadios\n", "hola\nhola\n", "a\n", "Hola\n", new string('a', 41) + "\n" })
        {
            var act = () => ListaLocal.Parse(bad);
            act.Should().Throw<InvalidOperationException>().Which.Message.Should().NotContain("hola");
        }
    }
}

public sealed class Spec031PrivacidadTests
{
    [Fact]
    public async Task No_Envia_Datos_Personales_CA_031_04()
    {
        var h = new ModerationHarness();
        h.Azure.Enqueue(() => FakeAzureHandler.Ok());
        await h.Moderator.ModerateTextAsync(ModerationHarness.Text("Llámame al 55 5555 1234, mi CURP ABCD900101HDFXYZ01, persona.prueba@ejemplo.com, vivo en Calle Hidalgo 123, Col. Centro. Se rompió el boiler."), default);
        h.Azure.Bodies.Should().ContainSingle().Which.Should().NotContainAny("5555 1234", "ABCD900101HDFXYZ01", "persona.prueba@ejemplo.com", "Hidalgo").And.Contain("boiler");
    }
}

public sealed class Spec031FallaCerradaTests
{
    [Fact]
    public async Task No_Disponible_Falla_Cerrado_CA_031_05()
    {
        foreach (var setup in new Action<FakeAzureHandler>[]
        {
            a => a.Enqueue(() => FakeAzureHandler.Status(HttpStatusCode.InternalServerError)).Enqueue(() => FakeAzureHandler.Status(HttpStatusCode.InternalServerError)),
            a => a.Enqueue(() => FakeAzureHandler.TooMany(0)).Enqueue(() => FakeAzureHandler.TooMany(0)),
            a => a.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("no es json") }),
            a => a.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"categoriesAnalysis\":[]}") }),
        })
        {
            var h = new ModerationHarness();
            setup(h.Azure);
            var verdict = await h.Moderator.ModerateTextAsync(ModerationHarness.Text("hola"), default);
            verdict.Decision.Should().Be(ModerationDecision.Unavailable);
            verdict.ReasonCode.Should().Be("unavailable");
            h.Azure.Calls.Should().BeLessThanOrEqualTo(2);
            var problem = ModerationProblemMapper.Map(verdict, false, h.Clock.Now)!;
            (problem.Status, problem.Code, problem.RetryAfterSeconds).Should().Be((503, "moderation_unavailable", 30));
        }
    }

    [Fact]
    public async Task Sin_Reintento_En_401_CA_031_06()
    {
        var h = new ModerationHarness();
        h.Azure.Enqueue(() => FakeAzureHandler.Status(HttpStatusCode.Unauthorized));
        (await h.Moderator.ModerateTextAsync(ModerationHarness.Text("hola"), default)).Decision.Should().Be(ModerationDecision.Unavailable);
        h.Azure.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Reintenta_Una_Vez_Con_Retry_After_Corto_Y_No_Con_Largo_CA_031_05()
    {
        var h = new ModerationHarness();
        h.Azure.Enqueue(() => FakeAzureHandler.TooMany(1)).Enqueue(() => FakeAzureHandler.Ok());
        (await h.Moderator.ModerateTextAsync(ModerationHarness.Text("hola"), default)).Decision.Should().Be(ModerationDecision.Approved);
        h.Azure.Calls.Should().Be(2);

        var slow = new ModerationHarness();
        slow.Azure.Enqueue(() => FakeAzureHandler.TooMany(10));
        (await slow.Moderator.ModerateTextAsync(ModerationHarness.Text("hola"), default)).Decision.Should().Be(ModerationDecision.Unavailable);
        slow.Azure.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Cortacircuito_CA_031_15()
    {
        var h = new ModerationHarness();
        for (var i = 0; i < 5; i++)
        {
            h.Azure.Enqueue(() => FakeAzureHandler.Status(HttpStatusCode.Unauthorized));
            await h.Moderator.ModerateTextAsync(ModerationHarness.Text($"texto {i}"), default);
        }

        h.Azure.Calls.Should().Be(5);
        h.Azure.Enqueue(() => FakeAzureHandler.Ok());
        (await h.Moderator.ModerateTextAsync(ModerationHarness.Text("sexta"), default)).Decision.Should().Be(ModerationDecision.Unavailable);
        h.Azure.Calls.Should().Be(5);
        h.Clock.Now += TimeSpan.FromSeconds(31);
        (await h.Moderator.ModerateTextAsync(ModerationHarness.Text("séptima"), default)).Decision.Should().Be(ModerationDecision.Approved);
    }

    [Fact]
    public async Task Sin_Credencial_No_Llama_Y_Falla_Cerrado_CA_031_06()
    {
        var h = new ModerationHarness(o => o.ContentSafety.Clave = null);
        (await h.Moderator.ModerateTextAsync(ModerationHarness.Text("hola"), default)).Decision.Should().Be(ModerationDecision.Unavailable);
        h.Azure.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Interruptor_Apagado_No_Llama_A_Azure_Y_Falla_Cerrado_CA_031_13()
    {
        var h = new ModerationHarness(o => o.Habilitado = false);
        (await h.Moderator.ModerateTextAsync(ModerationHarness.Text("hola"), default)).Decision.Should().Be(ModerationDecision.Unavailable);
        (await h.Moderator.ModerateImageAsync(ModerationHarness.Image(), default)).Decision.Should().Be(ModerationDecision.Unavailable);
        h.Azure.Calls.Should().Be(0);
    }
}

public sealed class Spec031CuotaTests
{
    [Fact]
    public async Task Topes_Mensual_Y_Por_Persona_CA_031_07()
    {
        var h = new ModerationHarness();
        h.Consumption.Quota = QuotaResult.MonthlyExhausted;
        (await h.Moderator.ModerateTextAsync(ModerationHarness.Text("hola"), default)).ReasonCode.Should().Be("quota_exhausted");
        h.Consumption.Quota = QuotaResult.PersonExhausted;
        var person = await h.Moderator.ModerateTextAsync(ModerationHarness.Text("hola"), default);
        person.ReasonCode.Should().Be("per_person_limit");
        var problem = ModerationProblemMapper.Map(person, false, new DateTimeOffset(2026, 11, 21, 18, 0, 0, TimeSpan.Zero))!;
        (problem.Status, problem.Code).Should().Be((429, "moderation_limit"));
        problem.RetryAfterSeconds.Should().Be(12 * 3600);
        h.Azure.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Redis_Caido_No_Llama_CA_031_09()
    {
        var h = new ModerationHarness();
        h.Consumption.Quota = QuotaResult.Unavailable;
        (await h.Moderator.ModerateTextAsync(ModerationHarness.Text("hola"), default)).ReasonCode.Should().Be("limit_unavailable");
        h.Consumption.CacheFailure = CacheLookup.Unavailable;
        (await h.Moderator.ModerateTextAsync(ModerationHarness.Text("hola"), default)).ReasonCode.Should().Be("limit_unavailable");
        h.Azure.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Alerta_80_CA_031_07()
    {
        var h = new ModerationHarness(o => o.Limites.LlamadasPorMes = 100);
        h.Consumption.MonthlyCount = 81;
        h.Azure.Enqueue(() => FakeAzureHandler.Ok());
        await h.Moderator.ModerateTextAsync(ModerationHarness.Text("hola"), default);
        h.Consumption.Alerts.Should().Be(1);
    }
}

public sealed class Spec031CacheTests
{
    [Fact]
    public async Task Decision_En_Cache_CA_031_10()
    {
        var h = new ModerationHarness();
        h.Azure.Enqueue(() => FakeAzureHandler.Ok(hate: 6));
        var first = await h.Moderator.ModerateTextAsync(ModerationHarness.Text("Mismo texto"), default);
        var second = await h.Moderator.ModerateTextAsync(ModerationHarness.Text("mismo  texto"), default);
        first.Source.Should().Be(ModerationSource.Azure);
        second.Source.Should().Be(ModerationSource.Cache);
        second.Decision.Should().Be(ModerationDecision.Rejected);
        h.Azure.Calls.Should().Be(1);
        h.Consumption.Reservations.Should().Be(1);
    }

    [Fact]
    public async Task Unavailable_No_Se_Guarda_Y_Otra_Version_Vuelve_A_Llamar_CA_031_10()
    {
        var h = new ModerationHarness();
        h.Azure.Enqueue(() => FakeAzureHandler.Status(HttpStatusCode.Unauthorized)).Enqueue(() => FakeAzureHandler.Ok());
        await h.Moderator.ModerateTextAsync(ModerationHarness.Text("hola"), default);
        (await h.Moderator.ModerateTextAsync(ModerationHarness.Text("hola"), default)).Source.Should().Be(ModerationSource.Azure);

        var other = new ModerationHarness();
        other.Azure.Enqueue(() => FakeAzureHandler.Ok()).Enqueue(() => FakeAzureHandler.Ok());
        await other.Moderator.ModerateTextAsync(ModerationHarness.Text("hola"), default);
        other.Options.VersionUmbrales = "2-otra";
        await other.Moderator.ModerateTextAsync(ModerationHarness.Text("hola"), default);
        other.Azure.Calls.Should().Be(2);
    }
}

public sealed class Spec031ImagenTests
{
    [Fact]
    public async Task Modera_Imagenes_CA_031_11()
    {
        var h = new ModerationHarness();
        h.Azure.Enqueue(() => FakeAzureHandler.Ok(sexual: 2)).Enqueue(() => FakeAzureHandler.Ok());
        (await h.Moderator.ModerateImageAsync(ModerationHarness.Image(), default)).Decision.Should().Be(ModerationDecision.Rejected);
        (await h.Moderator.ModerateImageAsync(ModerationHarness.Image(2000), default)).Decision.Should().Be(ModerationDecision.Approved);
        foreach (var bad in new[] { ModerationHarness.Image((4 * 1024 * 1024) + 1), ModerationHarness.Image(10, "image/gif"), ModerationHarness.Image(0) })
        {
            var verdict = await h.Moderator.ModerateImageAsync(bad, default);
            (verdict.Decision, verdict.ReasonCode).Should().Be((ModerationDecision.Rejected, "not_moderable"));
        }

        h.Azure.Calls.Should().Be(2);
        ModerationProblemMapper.Map(new ModerationVerdict(ModerationDecision.Rejected, []), true, h.Clock.Now)!.Code.Should().Be("image_rejected");
    }
}

public sealed class Spec031ConfiguracionTests
{
    private static ValidateOptionsResult Validate(Action<ModerationOptions> configure, string environment)
    {
        var options = new ModerationOptions();
        configure(options);
        return new ModerationOptionsValidator(new TestEnvironment(environment)).Validate(null, options);
    }

    [Fact]
    public async Task Solo_Local_Solo_En_Desarrollo_CA_031_13()
    {
        Validate(o => { o.Habilitado = true; o.Modo = ModerationModes.LocalOnly; }, Environments.Production).Failed.Should().BeTrue();
        Validate(o => { o.Habilitado = true; o.Modo = ModerationModes.LocalOnly; }, Environments.Development).Succeeded.Should().BeTrue();
        var h = new ModerationHarness(o => o.Modo = ModerationModes.LocalOnly);
        (await h.Moderator.ModerateTextAsync(ModerationHarness.Text("hola, buenas tardes"), default)).Decision.Should().Be(ModerationDecision.Approved);
        h.Azure.Calls.Should().Be(0);
    }

    [Fact]
    public void Azure_Habilitado_Sin_Endpoint_No_Arranca_CA_031_13()
    {
        Validate(o => o.Habilitado = true, Environments.Production).Failed.Should().BeTrue();
        Validate(o => { o.Habilitado = true; o.ContentSafety.Endpoint = "https://cs.ejemplo.test"; }, Environments.Production).Succeeded.Should().BeTrue();
        Validate(_ => { }, Environments.Production).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Umbrales_Validos_Y_Versionados_CA_031_14()
    {
        Validate(o => o.Umbrales.Hate = 5, Environments.Production).Failed.Should().BeTrue();
        Validate(o => o.Umbrales.Violence = 2, Environments.Production).Failed.Should().BeTrue();
        Validate(o => { o.Umbrales.Violence = 2; o.VersionUmbrales = "2-" + o.Umbrales.Fingerprint(); }, Environments.Production).Succeeded.Should().BeTrue();
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "test";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

public sealed class Spec031RegistrosTests
{
    [Fact]
    public async Task Uso_Sin_Contenido_CA_031_16()
    {
        var h = new ModerationHarness();
        h.Azure.Enqueue(() => FakeAzureHandler.Ok()).Enqueue(() => FakeAzureHandler.Ok());
        await h.Moderator.ModerateTextAsync(ModerationHarness.Text("texto secreto sintetico"), default);
        await h.Moderator.ModerateImageAsync(ModerationHarness.Image(), default);
        h.Usage.Saved.Should().HaveCount(2);
        var text = h.Usage.Saved[0];
        (text.Type, text.Decision, text.Source, text.Service).Should().Be(("texto", ModerationDecision.Approved, ModerationSource.Azure, "market"));
        typeof(ModerationUsage).GetProperties().Select(p => p.Name).Should().NotContain(["Text", "Content", "Fingerprint", "UserId", "Sub"]);
    }
}

public sealed class Spec031PlantillasTests
{
    [Fact]
    public void Mensaje_Sin_Detalle_CA_031_19()
    {
        foreach (var code in new[] { "content_rejected", "image_rejected", "moderation_unavailable", "moderation_limit" })
        {
            PlantillasDeModeracion.Detail(code).Should().NotContainAny("Hate", "Sexual", "Violence", "SelfHarm", "severidad", "categor");
        }

        PlantillasDeModeracion.Detail("content_rejected").Should().Be("Tu texto no cumple las reglas de la comunidad de Knock. Revísalo y vuelve a intentarlo.");
        ModerationProblemMapper.Map(new ModerationVerdict(ModerationDecision.Approved, []), false, DateTimeOffset.UtcNow).Should().BeNull();
    }
}
