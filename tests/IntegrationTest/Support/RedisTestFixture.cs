using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Knock.IntegrationTest.Support;

/// <summary>Redis real con Testcontainers, un contenedor por ejecución; el reaper de Testcontainers lo borra al terminar. Solo datos ficticios.</summary>
internal sealed class RedisTestFixture
{
    public const string Image = "redis:7.4-alpine";
    public const ushort Port = 6379;

    private static readonly Lazy<Task<RedisTestFixture>> _shared = new(StartAsync);

    private readonly IContainer _container;

    private RedisTestFixture()
    {
        _container = new ContainerBuilder(Image)
            .WithPortBinding(Port, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(Port))
            .Build();
    }

    public string ConnectionString => $"{_container.Hostname}:{_container.GetMappedPublicPort(Port)}";

    public static Task<RedisTestFixture> SharedAsync() => _shared.Value;

    private static async Task<RedisTestFixture> StartAsync()
    {
        var fixture = new RedisTestFixture();
        await fixture._container.StartAsync();
        return fixture;
    }
}
