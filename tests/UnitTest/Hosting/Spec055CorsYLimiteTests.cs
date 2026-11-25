using System.Text.Json;
using AwesomeAssertions;
using Knock.Hosting.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Knock.UnitTest.Hosting;

/// <summary>Spec 055 (KNOCK-266, T-17, R-05 y R-08): CORS y el límite de cuerpo son una sola pieza de <c>Knock.Hosting</c>; cada servicio solo pone sus opciones.</summary>
public sealed class Spec055CorsYLimiteTests
{
    private const string _origin = "https://panel.example.test";

    private static readonly KnockCorsOptions _options = new([_origin], TimeSpan.FromMinutes(10), ["Authorization", "Content-Type", "If-Match"]);

    [Theory]
    [InlineData("authorization, if-match", true)]
    [InlineData("Authorization, If-None-Match", false)]
    public async Task Preflight_GrantsOnlyTheHeadersOfTheService_CA_055_12(string requested, bool granted)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Options;
        context.Request.Headers.Origin = _origin;
        context.Request.Headers.AccessControlRequestMethod = "POST";
        context.Request.Headers.AccessControlRequestHeaders = requested;

        await new CorsMiddleware(_ => Task.CompletedTask, _options).InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        context.Response.Headers.ContainsKey("Access-Control-Allow-Origin").Should().Be(granted);
        if (granted)
        {
            context.Response.Headers.AccessControlAllowHeaders.ToString().Should().Be("Authorization, Content-Type, If-Match");
            context.Response.Headers.AccessControlMaxAge.ToString().Should().Be("600");
        }
    }

    [Fact]
    public async Task UnknownOrigin_GetsNoAllowHeaders_AndNoOriginsMeansOff_CA_055_12()
    {
        var nextCalled = false;
        var unknown = new DefaultHttpContext();
        unknown.Request.Headers.Origin = "https://otro.example.test";
        await new CorsMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, _options).InvokeAsync(unknown);
        await unknown.Response.StartAsync();

        nextCalled.Should().BeTrue();
        unknown.Response.Headers.ContainsKey("Access-Control-Allow-Origin").Should().BeFalse();

        var off = new DefaultHttpContext();
        off.Request.Headers.Origin = _origin;
        await new CorsMiddleware(_ => Task.CompletedTask, new KnockCorsOptions([], TimeSpan.Zero, [])).InvokeAsync(off);
        off.Response.Headers.ContainsKey("Vary").Should().BeFalse();
    }

    [Theory]
    [InlineData("payload_too_large")]
    [InlineData(null)]
    public async Task BodyLimit_WritesTheCodeOnlyWhenTheServiceAsksForIt_CA_055_12(string? code)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var environment = Host.CreateEmptyApplicationBuilder(null).Environment;
        environment.EnvironmentName = "Production";
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddKnockProblemDetails();
        using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Response.Body = new MemoryStream();
        context.Request.ContentLength = 2048;
        context.SetEndpoint(new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/x"), 0) { Metadata = { new LimitMetadata(1024) } }.Build());

        await new RequestBodyLimitMiddleware(_ => Task.CompletedTask, new RequestBodyLimitOptions { PayloadTooLargeCode = code }).InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var body = document.RootElement;
        body.TryGetProperty("code", out var value).Should().Be(code is not null);
        if (code is not null)
        {
            value.GetString().Should().Be(code);
        }
    }

    private sealed class LimitMetadata(long max) : Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata
    {
        public long? MaxRequestBodySize { get; } = max;
    }
}
