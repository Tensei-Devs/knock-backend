using System.Globalization;
using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.DatabaseTest.Support;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.DatabaseTest.Scripts;

/// <summary>
/// Spec 003, CA-04 (R-08): un respaldo con <c>deploy/backup/mongo-backup.sh</c> (mongodump) restaurado en una base vacía trae los mismos
/// documentos e índices. Prueba local en contenedor; la verificación en Azure queda para KNOCK-200. Solo datos ficticios.
/// </summary>
public sealed class Spec003BackupRestoreTests
{
    private static readonly string[] _collections = ["outbox", "tareas_programadas", "datos_ficticios"];

    [Fact]
    public async Task Backup_RestoredIntoEmptyDatabase_HasSameDocumentsAndIndexes_CA_04()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        var suffix = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        var sourceName = "s003_backup_src_" + suffix;
        var targetName = "s003_backup_dst_" + suffix;
        using var client = new MongoClient(documentDb.ConnectionString);
        try
        {
            var source = client.GetDatabase(sourceName);
            await SeedAsync(source);
            (await (await client.ListDatabaseNamesAsync()).ToListAsync()).Should().NotContain(targetName, "la base de destino está vacía");
            await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string>
            {
                ["KNOCK_BACKUP_SOURCE_DB"] = sourceName,
                ["KNOCK_BACKUP_TARGET_DB"] = targetName,
            });
            var script = $"{MongoToolsContainer.BackupDirectory}/mongo-backup.sh";
            const string archive = "/tmp/knock-backup.archive.gz";

            var dump = await tools.RunAsync("bash", script, "dump", archive);
            var restore = await tools.RunAsync("bash", script, "restore", archive);

            dump.ExitCode.Should().Be(0, dump.Output);
            restore.ExitCode.Should().Be(0, restore.Output);
            (dump.Output + restore.Output).Should().NotContain("Kn0ck", "la salida nunca muestra la contraseña");
            var target = client.GetDatabase(targetName);
            foreach (var name in _collections)
            {
                (await AllAsync(target, name)).Should().Equal(await AllAsync(source, name), $"los documentos de {name} coinciden");
                (await MongoIndexes.DescribeAsync(target, name)).Should().Equal(
                    await MongoIndexes.DescribeAsync(source, name), $"los índices de {name} coinciden");
            }
        }
        finally
        {
            await client.DropDatabaseAsync(sourceName);
            await client.DropDatabaseAsync(targetName);
        }
    }

    [Fact]
    public async Task Backup_WithoutConnectionVariable_FailsWithoutRunningTools_CA_04()
    {
        var documentDb = await DocumentDbFixture.SharedAsync();
        await using var tools = await MongoToolsContainer.StartAsync(documentDb, new Dictionary<string, string>
        {
            ["KNOCK_BACKUP_SOURCE_DB"] = "s003_any",
        });

        var result = await tools.RunAsync(
            "env", "-u", "KNOCK_BACKUP_MONGO_URI", "bash", $"{MongoToolsContainer.BackupDirectory}/mongo-backup.sh", "dump", "/tmp/x.gz");

        result.ExitCode.Should().NotBe(0);
        result.Output.Should().Contain("KNOCK_BACKUP_MONGO_URI");
    }

    private static async Task SeedAsync(IMongoDatabase database)
    {
        var now = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
        await database.GetCollection<BsonDocument>("outbox").InsertManyAsync(Enumerable.Range(1, 3).Select(n => new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "estado", "Pendiente" },
            { "tipo", "EventoFicticio" },
            { "creadoEn", now.AddMinutes(n) },
            { "version", 1 },
        }));
        await database.GetCollection<BsonDocument>("tareas_programadas").InsertOneAsync(new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "estado", "Pendiente" },
            { "tipo", "TareaFicticia" },
            { "programadaPara", now.AddHours(1) },
            { "version", 1 },
        });
        var datos = database.GetCollection<BsonDocument>("datos_ficticios");
        await datos.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("clave"), new CreateIndexOptions { Unique = true }));
        await datos.InsertManyAsync(Enumerable.Range(1, 5).Select(n => new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "clave", "ficticio-" + n.ToString(CultureInfo.InvariantCulture) },
            { "creadoEn", now },
        }));
    }

    private static async Task<List<BsonDocument>> AllAsync(IMongoDatabase database, string name) =>
        await database.GetCollection<BsonDocument>(name).Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(Builders<BsonDocument>.Sort.Ascending("_id")).ToListAsync();
}
