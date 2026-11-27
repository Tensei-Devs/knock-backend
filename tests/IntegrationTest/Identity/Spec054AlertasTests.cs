using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.Hosting.Data;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Identity;

/// <summary>Spec 054, T-07: la consulta de la alerta contra MongoDB real: lo fallido y lo que lleva más de 10 minutos de retraso, sin leer la carga.</summary>
public sealed class Spec054AlertasTests
{
    private static readonly DateTime _now = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FindsFailedAndLateItems_AndIgnoresTheRest_CA_054_20()
    {
        var fixture = await DocumentDbFixture.SharedAsync();
        using var client = new MongoClient(fixture.ConnectionString);
        var name = "s054_alert_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var database = client.GetDatabase(name);
        var marketId = ObjectId.GenerateNewId();
        var failedTask = ObjectId.GenerateNewId();
        var stuckTask = ObjectId.GenerateNewId();
        try
        {
            await database.GetCollection<BsonDocument>("outbox").InsertManyAsync(
            [
                Item("failed", "fallido", _now.AddMinutes(-1)),
                Item("late", "pendiente", _now.AddMinutes(-11)),
                Item("recent", "pendiente", _now.AddMinutes(-9)),
                Item("scheduled", "pendiente", _now.AddHours(1)),
                Item("delivered", "entregado", _now.AddHours(-1)),
                new BsonDocument { ["_id"] = marketId, ["tipo"] = "CotizacionEnviada", ["estado"] = "pendiente", ["creadoEn"] = _now.AddHours(-1) },
            ]);
            await database.GetCollection<BsonDocument>("tareas_programadas").InsertManyAsync(
            [
                Item(failedTask, "fallida", _now),
                Item(stuckTask, "en_proceso", _now.AddMinutes(-30)),
                Item(ObjectId.GenerateNewId(), "hecha", _now.AddHours(-1)),
            ]);

            var alerts = await new ConsultaDeAlertasMongo(database).FindAsync(_now.AddMinutes(-10), CancellationToken.None);

            (string, string, string)[] expected =
            [
                ("outbox", "failed", "fallido"),
                ("outbox", "late", "atrasado"),
                ("outbox", marketId.ToString(), "atrasado"),
                ("tareas_programadas", failedTask.ToString(), "fallida"),
                ("tareas_programadas", stuckTask.ToString(), "atrasado"),
            ];
            alerts.Select(x => (x.Proceso, x.Id, x.Resultado)).Should().BeEquivalentTo(expected);
            alerts.Should().Contain(x => x.Id == "failed" && x.Tipo == "sesion_revocada" && x.Intentos == 2);
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }

    private static BsonDocument Item(BsonValue id, string status, DateTime due) =>
        new() { ["_id"] = id, ["tipo"] = "sesion_revocada", ["estado"] = status, ["programadaPara"] = due, ["intentos"] = 2, ["creadoEn"] = due, ["carga"] = new BsonDocument("sid", "no-se-lee") };
}
