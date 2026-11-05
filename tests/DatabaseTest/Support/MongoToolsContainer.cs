using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Knock.IntegrationTest.Support;

namespace Knock.DatabaseTest.Support;

/// <summary>
/// Contenedor con mongosh, mongodump y mongorestore (imagen oficial de MongoDB, solo por sus herramientas) y los scripts del
/// repositorio montados: <c>db/</c> en <see cref="DbDirectory"/> y <c>deploy/backup/</c> en <see cref="BackupDirectory"/>. La URI de
/// documentdb-local solo viaja en variables de entorno. Spec 003, CA-01 y CA-04.
/// </summary>
internal sealed class MongoToolsContainer : IAsyncDisposable
{
    /// <summary>mongo:8.0 del 2026-10-27, fijada por digest.</summary>
    public const string Image = "mongo:8.0@sha256:b9924c330a8bbc5800fe9c69f04a84e221eca7b28fd049a0354147d9908fa142";

    public const string DbDirectory = "/opt/knock/db";
    public const string BackupDirectory = "/opt/knock/backup";

    private readonly IContainer _container;

    private MongoToolsContainer(IContainer container) => _container = container;

    public static async Task<MongoToolsContainer> StartAsync(DocumentDbFixture documentDb, IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(documentDb);
        ArgumentNullException.ThrowIfNull(environment);
        var root = RepositoryRoot();
        var builder = new ContainerBuilder(Image)
            .WithEntrypoint("sleep")
            .WithCommand("infinity")
            .WithEnvironment("KNOCK_DB_URI", documentDb.ContainerNetworkConnectionString)
            .WithEnvironment("KNOCK_BACKUP_MONGO_URI", documentDb.ContainerNetworkConnectionString)
            .WithResourceMapping(new FileInfo(Path.Combine(root, "deploy", "backup", "mongo-backup.sh")), BackupDirectory)
            // Los scripts de db/ (numerados y de entrega) y las semillas del catálogo que carga db/entrega.js (spec 014).
            .WithResourceMapping(new DirectoryInfo(Path.Combine(root, "db", "seeds", "catalog")), DbDirectory + "/seeds/catalog");
        foreach (var script in Directory.GetFiles(Path.Combine(root, "db"), "*.js"))
        {
            builder = builder.WithResourceMapping(new FileInfo(script), DbDirectory);
        }

        foreach (var (name, value) in environment)
        {
            builder = builder.WithEnvironment(name, value);
        }

        var container = builder.Build();
        await container.StartAsync();
        return new MongoToolsContainer(container);
    }

    /// <summary>Ejecuta el comando; devuelve código de salida y la salida combinada.</summary>
    public async Task<(long ExitCode, string Output)> RunAsync(params string[] command)
    {
        var result = await _container.ExecAsync(command);
        return (result.ExitCode ?? -1, result.Stdout + result.Stderr);
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Knock.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Knock.slnx was not found above the test output directory.");
    }
}
