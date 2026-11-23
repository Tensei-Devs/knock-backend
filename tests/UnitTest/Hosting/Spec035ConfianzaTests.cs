using System.Net;
using AwesomeAssertions;
using Knock.Hosting.ForwardedHeaders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Knock.UnitTest.Hosting;

/// <summary>Spec 035 (KNOCK-180), R-09, R-10 y R-12: validación al arrancar de las redes de confianza y encabezados reenviados. Solo datos ficticios.</summary>
public sealed class Spec035ConfianzaTests
{
    [Fact]
    public void No_Arranca_Sin_Redes_CA_035_09()
    {
        var act = () => OptionsOf("Production", []);

        act.Should().Throw<InvalidOperationException>().WithMessage("*ForwardedHeaders:KnownNetworks*").And.Message.Should().NotContain("10.");
    }

    [Fact]
    public void En_Development_Sin_Redes_No_Confia_En_Nadie_CA_035_09()
    {
        OptionsOf("Development", []).ForwardedHeaders.Should().Be(ForwardedHeaders.None);
    }

    [Theory]
    [InlineData("203.0.113.0/24")]
    [InlineData("0.0.0.0/0")]
    [InlineData("::/0")]
    [InlineData("10.0.0.0/8")]
    [InlineData("172.16.0.0/12")]
    [InlineData("192.168.0.0/15")]
    [InlineData("fc00::/7")]
    [InlineData("2001:db8::/48")]
    public void Rechaza_Redes_Amplias_O_Publicas_CA_035_10(string network)
    {
        var act = () => OptionsOf("Production", [network]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*ForwardedHeaders:KnownNetworks*").And.Message.Should().NotContain(network);
    }

    [Theory]
    [InlineData("10.20.0.0/23")]
    [InlineData("10.0.0.0/16")]
    [InlineData("172.16.0.0/16")]
    [InlineData("192.168.1.0/24")]
    [InlineData("fd00:1234:5678::/48")]
    public void Acepta_Redes_Privadas_Acotadas_CA_035_10(string network)
    {
        OptionsOf("Production", [network]).KnownIPNetworks.Should().ContainSingle();
    }

    [Fact]
    public async Task Encabezado_Falso_No_Mueve_La_Ip_CA_035_12()
    {
        var options = OptionsOf("Production", ["10.20.0.0/23"]);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.20.0.5");
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.1, 198.51.100.7";
        context.Request.Headers["X-Forwarded-Host"] = "evil.example";
        context.Request.Headers["Forwarded"] = "for=192.0.2.9;host=evil.example";
        var originalHost = context.Request.Host;

        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));
        await middleware.Invoke(context);

        options.ForwardLimit.Should().Be(1);
        options.ForwardedHeaders.Should().Be(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        context.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("198.51.100.7"), "con ForwardLimit = 1 solo vale el valor que agregó el proxy de confianza");
        context.Request.Host.Should().Be(originalHost);
    }

    private static ForwardedHeadersOptions OptionsOf(string environment, IReadOnlyList<string> networks)
    {
        var values = new Dictionary<string, string?>();
        for (var i = 0; i < networks.Count; i++)
        {
            values[$"ForwardedHeaders:KnownNetworks:{i}"] = networks[i];
        }

        using var provider = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(values).Build())
            .AddSingleton<IHostEnvironment>(new HostingEnvironment { EnvironmentName = environment })
            .AddKnockForwardedHeaders()
            .BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }
}
