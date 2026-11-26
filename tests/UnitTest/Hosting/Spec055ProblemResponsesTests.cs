using System.Text.Json;
using AwesomeAssertions;
using Knock.Contracts.Correlation;
using Knock.Hosting.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Knock.UnitTest.Hosting;

/// <summary>Spec 055 (KNOCK-266, T-17, R-05 y R-08): el cuerpo de error es uno solo para el gateway y los tres servicios; solo cambian las opciones.</summary>
public class Spec055ProblemResponsesTests
{
    [Fact]
    public void CorrelationItemKey_EqualsTheContractsKey_CA_055_12() =>
        ProblemResponses.CorrelationItemKey.Should().Be(CorrelationId.ItemKey);

    [Fact]
    public async Task Write_WithCodeAndExtensions_ProducesTheUniformBody_CA_055_12()
    {
        using var provider = Provider(new KnockProblemDetailsOptions());
        var context = Context(provider, "corr-1");

        await ProblemResponses.WriteAsync(context, StatusCodes.Status429TooManyRequests, "Too Many Requests", "rate_limited", new Dictionary<string, string> { ["retryAfter"] = "5" });

        var body = Body(context);
        context.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        body.GetProperty("title").GetString().Should().Be("Too Many Requests");
        body.GetProperty("type").GetString().Should().Be("about:blank");
        body.GetProperty("code").GetString().Should().Be("rate_limited");
        body.GetProperty("retryAfter").GetString().Should().Be("5");
        body.GetProperty("correlationId").GetString().Should().Be("corr-1");
        body.TryGetProperty("traceId", out _).Should().BeTrue();
        body.TryGetProperty("stackTrace", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Write_401_AddsTheBearerChallenge_CA_055_12()
    {
        using var provider = Provider(new KnockProblemDetailsOptions());
        var context = Context(provider, null);

        await ProblemResponses.WriteAsync(context, StatusCodes.Status401Unauthorized, "Unauthorized");

        context.Response.Headers.WWWAuthenticate.ToString().Should().Be("Bearer");
        Body(context).TryGetProperty("correlationId", out _).Should().BeFalse();
        Body(context).TryGetProperty("code", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Write_TraceIdFollowsTheW3COption_CA_055_12()
    {
        using var activity = new System.Diagnostics.Activity("spec055");
        activity.Start();
        foreach (var w3c in new[] { true, false })
        {
            using var provider = Provider(new KnockProblemDetailsOptions { W3CTraceId = w3c });
            var context = Context(provider, null);

            await ProblemResponses.WriteAsync(context, StatusCodes.Status404NotFound, "Not Found");

            Body(context).GetProperty("traceId").GetString().Should().Be(w3c ? activity.TraceId.ToString() : activity.Id);
        }
    }

    private static ServiceProvider Provider(KnockProblemDetailsOptions options)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var environment = Host.CreateEmptyApplicationBuilder(null).Environment;
        environment.EnvironmentName = "Production";
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddKnockProblemDetails(options);
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext Context(ServiceProvider provider, string? correlationId)
    {
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Response.Body = new MemoryStream();
        if (correlationId is not null)
        {
            context.Items[CorrelationId.ItemKey] = correlationId;
        }

        return context;
    }

    private static JsonElement Body(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return JsonDocument.Parse(context.Response.Body).RootElement.Clone();
    }
}
