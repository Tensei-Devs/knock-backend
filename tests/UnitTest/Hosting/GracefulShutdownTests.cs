using Knock.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Knock.UnitTest.Hosting;

/// <summary>Spec 034 T-02 (R-03): el apagado drena 25 s y la disponibilidad pasa a 503 al empezar a apagarse.</summary>
public sealed class GracefulShutdownTests
{
    [Fact]
    public void Shutdown_Timeout_Is_25_Seconds_CA_034_03()
    {
        var services = new ServiceCollection().AddLogging().AddKnockGracefulShutdown().BuildServiceProvider();

        Assert.Equal(TimeSpan.FromSeconds(25), services.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout);
    }

    [Fact]
    public async Task Ready_Turns_Unhealthy_When_Host_Is_Stopping_CA_034_03()
    {
        using var stopping = new CancellationTokenSource();
        var services = new ServiceCollection().AddLogging().AddSingleton<IHostApplicationLifetime>(new FakeLifetime(stopping.Token)).AddKnockGracefulShutdown().BuildServiceProvider();
        var health = services.GetRequiredService<HealthCheckService>();

        var before = await health.CheckHealthAsync(check => check.Tags.Contains(GracefulShutdownExtensions.ReadyTag), CancellationToken.None);
        await stopping.CancelAsync();
        var after = await health.CheckHealthAsync(check => check.Tags.Contains(GracefulShutdownExtensions.ReadyTag), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, before.Status);
        Assert.Equal(HealthStatus.Unhealthy, after.Status);
    }

    private sealed class FakeLifetime(CancellationToken stopping) : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => stopping;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }
}
