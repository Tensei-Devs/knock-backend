using System.Diagnostics;
using System.Threading.RateLimiting;
using AwesomeAssertions;
using Knock.Hosting.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Xunit;

namespace Knock.UnitTest.Hosting;

/// <summary>
/// Spec 006, R-05 y spec 055 (KNOCK-280): el limitador compartido (ahora en <c>Knock.Hosting</c>) cae de inmediato al límite por copia si Redis no está
/// conectado o no existe, sin esperar el tiempo de espera del cliente, y deja el aviso 247 en ambos casos. Sin contenedores: apunta a un puerto sin servicio.
/// </summary>
public sealed class Spec006SharedRateLimiterTests
{
    private static readonly RateLimitPolicySettings _policy = new() { PermitLimit = 2, Window = TimeSpan.FromMinutes(1) };

    [Theory]
    [InlineData("identity")]
    [InlineData("market")]
    [InlineData("messaging")]
    public async Task RedisNotConnected_FallsBackToPerInstanceLimitWithoutWaiting_R_006_05(string service)
    {
        using var logs = new CapturingProvider();
        using var provider = await CreateProviderAsync(logs, withRedis: true);
        using var limiter = new SharedRateLimiter(provider, service, "global:test", _policy);

        var (results, elapsed) = await AcquireThreeAsync(limiter);

        results.Should().Equal(true, true, false);
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RedisNotConnected_LogsTheFallbackEvent_CA_055_11()
    {
        using var logs = new CapturingProvider();
        using var provider = await CreateProviderAsync(logs, withRedis: true);
        using var limiter = new SharedRateLimiter(provider, "market", "global:test", _policy);

        await AcquireThreeAsync(limiter);

        logs.Events.Should().Contain(SharedRateLimiter.FallbackEventId);
        logs.Messages.Should().NotContain(message => message.Contains("global:test", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RedisNotRegistered_FallsBackAndLogsTheFallbackEvent_CA_055_11()
    {
        using var logs = new CapturingProvider();
        using var provider = await CreateProviderAsync(logs, withRedis: false);
        using var limiter = new SharedRateLimiter(provider, "market", "global:test", _policy);

        var (results, _) = await AcquireThreeAsync(limiter);

        results.Should().Equal(true, true, false);
        logs.Events.Should().Contain(SharedRateLimiter.FallbackEventId);
    }

    private static async Task<ServiceProvider> CreateProviderAsync(CapturingProvider logs, bool withRedis)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(logs));
        if (withRedis)
        {
            var options = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                ConnectTimeout = 500,
                AsyncTimeout = 5000,
                SyncTimeout = 5000,
            };
            options.EndPoints.Add("127.0.0.1", 9);
            var connection = await ConnectionMultiplexer.ConnectAsync(options);
            connection.IsConnected.Should().BeFalse();
            services.AddSingleton<IConnectionMultiplexer>(connection);
        }

        return services.BuildServiceProvider();
    }

    private static async Task<(bool[] Results, TimeSpan Elapsed)> AcquireThreeAsync(RateLimiter limiter)
    {
        var clock = Stopwatch.StartNew();
        var results = new bool[3];
        for (var i = 0; i < results.Length; i++)
        {
            using var lease = await limiter.AcquireAsync();
            results[i] = lease.IsAcquired;
        }

        return (results, clock.Elapsed);
    }

    private sealed class CapturingProvider : ILoggerProvider
    {
        private readonly List<(int Event, string Message)> _entries = [];

        public IReadOnlyList<int> Events => [.. _entries.Select(e => e.Event)];

        public IReadOnlyList<string> Messages => [.. _entries.Select(e => e.Message)];

        public ILogger CreateLogger(string categoryName) => new Capture(_entries);

        public void Dispose()
        {
        }

        private sealed class Capture(List<(int Event, string Message)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (entries)
                {
                    entries.Add((eventId.Id, formatter(state, exception)));
                }
            }
        }
    }
}
