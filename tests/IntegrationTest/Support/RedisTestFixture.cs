using System.Security.Cryptography;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Knock.IntegrationTest.Support;

/// <summary>
/// Redis real con Testcontainers y contraseña (spec 003, R-07), un contenedor por ejecución; el reaper de Testcontainers lo borra al terminar.
/// La contraseña es de un solo uso y solo vive en memoria. Solo datos ficticios.
/// </summary>
internal sealed class RedisTestFixture
{
    public const string Image = "redis:7.4-alpine";
    public const ushort Port = 6379;

    private static readonly Lazy<Task<RedisTestFixture>> _shared = new(StartAsync);

    private readonly string _password = "Kn0ck" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
    private readonly IContainer _container;

    private RedisTestFixture()
    {
        _container = new ContainerBuilder(Image)
            .WithPortBinding(Port, assignRandomHostPort: true)
            .WithCommand("redis-server", "--requirepass", _password)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(Port))
            .Build();
    }

    /// <summary>Cadena para <c>ConnectionStrings:KnockRedis</c> (con contraseña).</summary>
    public string ConnectionString => $"{_container.Hostname}:{_container.GetMappedPublicPort(Port)},password={_password}";

    public static Task<RedisTestFixture> SharedAsync() => _shared.Value;

    private static async Task<RedisTestFixture> StartAsync()
    {
        var fixture = new RedisTestFixture();
        await fixture._container.StartAsync();
        return fixture;
    }
}
