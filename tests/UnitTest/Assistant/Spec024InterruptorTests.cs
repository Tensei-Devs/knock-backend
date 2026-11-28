using AwesomeAssertions;
using Azure.Core;
using Knock.Market.Api.Modules.Assistant;
using Knock.Market.Api.Modules.Assistant.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Knock.UnitTest.Assistant;

/// <summary>Spec 024, R-09 y R-15: el cableado de Azure OpenAI (endpoint, despliegue y credencial administrada) solo existe con <c>Asistente:Habilitado=true</c>; apagado es el comportamiento de siempre.</summary>
public sealed class Spec024InterruptorTests
{
    private static ServiceCollection Build(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddAssistantModule(configuration);
        return services;
    }

    [Fact]
    public async Task Apagado_Por_Omision_CA_024_15()
    {
        var services = Build([]);

        services.Should().NotContain(d => d.ServiceType == typeof(TokenCredential));
        services.Should().NotContain(d => d.ServiceType == typeof(IHttpClientFactory));
        using var provider = services.BuildServiceProvider();
        var response = await provider.GetRequiredService<IAiClient>()
            .GenerarAsync<AsistenteKnockSalida>(AiHarness.Request(), CancellationToken.None);

        (response.Origen, response.MotivoRespaldo).Should().Be((AiOrigin.Template, "disabled"));
        response.Valor.Should().NotBeNull();
    }

    [Fact]
    public void Switch_On_Registers_Managed_Identity_Credential_And_Provider_CA_024_09()
    {
        var services = Build(new()
        {
            ["Asistente:Habilitado"] = "true",
            ["Asistente:AzureOpenAI:Endpoint"] = "https://oai.example.test/",
            ["Asistente:AzureOpenAI:ModeloDespliegue"] = "asistente",
        });

        services.Should().Contain(d => d.ServiceType == typeof(TokenCredential));
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TokenCredential>().Should().NotBeNull();
        provider.GetRequiredService<IProveedorIa>().Should().BeOfType<ProveedorIa>();
    }

    [Fact]
    public void Switch_On_Without_Endpoint_Or_Deployment_Fails_Validation_CA_024_09()
    {
        var result = new AssistantOptionsValidator().Validate(null, new AssistantOptions { Habilitado = true });

        result.Failed.Should().BeTrue();
    }
}
