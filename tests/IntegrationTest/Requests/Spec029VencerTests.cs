extern alias MarketApi;

using System.Net;
using AwesomeAssertions;
using MarketApi::Knock.Market.Api.Common.TareasProgramadas;
using MarketApi::Knock.Market.Api.Modules.Requests.Services;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>Spec 029, T-04 y T-07: tarea <c>VencerSolicitud</c> y barrido <c>BarrerVencidas</c> (MongoDB real; solo corren en CI).</summary>
public sealed class Spec029VencerTests
{
    private static async Task<ObjectId> CreateAsync(Spec020Host host)
    {
        using var created = await host.CreateAsync(host.ValidBody());
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        return ObjectId.Parse((await Spec020Host.ReadAsync(created)).GetProperty("id").GetString());
    }

    private static Task<UpdateResult> SetAsync(Spec020Host host, ObjectId id, BsonDocument set) =>
        host.Database.GetCollection<BsonDocument>("solicitudes").UpdateOneAsync(new BsonDocument("_id", id), new BsonDocument("$set", set));

    private static async Task<string> StateOfAsync(Spec020Host host, ObjectId id) =>
        (await host.Database.GetCollection<BsonDocument>("solicitudes").Find(new BsonDocument("_id", id)).SingleAsync())["estado"].AsString;

    private static Task<long> ExpiredEventsAsync(Spec020Host host, ObjectId id) =>
        host.Database.GetCollection<BsonDocument>("outbox").CountDocumentsAsync(new BsonDocument("_id", "SolicitudVencida:" + id));

    private static TareaReservada ExpiryTask(ObjectId id) => new(ObjectId.GenerateNewId(), "VencerSolicitud", id.ToString(), "x", 0);

    [Fact]
    public async Task Crear_Programa_La_Tarea_De_Vencimiento_CA_029_14()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await CreateAsync(host);

        var request = await host.Database.GetCollection<BsonDocument>("solicitudes").Find(new BsonDocument("_id", id)).SingleAsync();
        var task = await host.Database.GetCollection<BsonDocument>("tareas_programadas")
            .Find(new BsonDocument { { "tipo", "VencerSolicitud" }, { "claveNegocio", id.ToString() } }).SingleAsync();

        task["programadaPara"].ToUniversalTime().Should().Be(request["venceEn"].ToUniversalTime());
        task["estado"].AsString.Should().Be("pendiente");
    }

    [Fact]
    public async Task Plazo_Configurable_CA_029_14()
    {
        await using var host = await Spec020Host.StartAsync(new Dictionary<string, string?> { ["Solicitudes:HorasVencimiento"] = "24" });
        var id = await CreateAsync(host);

        var request = await host.Database.GetCollection<BsonDocument>("solicitudes").Find(new BsonDocument("_id", id)).SingleAsync();

        (request["venceEn"].ToUniversalTime() - request["creadoEn"].ToUniversalTime()).Should().Be(TimeSpan.FromHours(24));
    }

    [Fact]
    public async Task Plazo_Fuera_De_Rango_Impide_Arrancar_CA_029_14()
    {
        var act = () => Spec020Host.StartAsync(new Dictionary<string, string?> { ["Solicitudes:HorasVencimiento"] = "200" });

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Vence_En_Cascada_CA_029_06()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await CreateAsync(host);
        await host.Database.GetCollection<BsonDocument>("cotizaciones").InsertOneAsync(new BsonDocument { { "solicitudId", id }, { "tecnicoId", ObjectId.GenerateNewId() }, { "estado", "enviada" } });
        await SetAsync(host, id, new BsonDocument("venceEn", DateTime.UtcNow.AddHours(-1)));
        using var scope = host.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<VencerSolicitud>().HandleAsync(ExpiryTask(id), CancellationToken.None);

        (await StateOfAsync(host, id)).Should().Be("vencida");
        (await host.Database.GetCollection<BsonDocument>("cotizaciones").Find(new BsonDocument("solicitudId", id)).SingleAsync())["motivoRechazo"].AsString.Should().Be("request_expired");
        var message = await host.Database.GetCollection<BsonDocument>("outbox").Find(new BsonDocument("_id", "SolicitudVencida:" + id)).SingleAsync();
        message["datos"]["clienteId"].AsString.Should().Be(host.ClientSub);
    }

    [Fact]
    public async Task No_Vence_Con_Cita_O_Antes_De_Tiempo_CA_029_07()
    {
        await using var host = await Spec020Host.StartAsync();
        var withVisit = await CreateAsync(host);
        var early = await CreateAsync(host);
        await SetAsync(host, withVisit, new BsonDocument { { "estado", "cita_confirmada" }, { "venceEn", DateTime.UtcNow.AddHours(-1) } });
        using var scope = host.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<VencerSolicitud>();

        await handler.HandleAsync(ExpiryTask(withVisit), CancellationToken.None);
        await handler.HandleAsync(ExpiryTask(early), CancellationToken.None);

        (await StateOfAsync(host, withVisit)).Should().Be("cita_confirmada");
        (await StateOfAsync(host, early)).Should().Be("enviada");
        (await ExpiredEventsAsync(host, withVisit)).Should().Be(0);
        (await ExpiredEventsAsync(host, early)).Should().Be(0);
    }

    [Fact]
    public async Task Cotizacion_Elegida_La_Tarea_Queda_Hecha_Sin_Efecto_CA_054_15()
    {
        // El intervalo largo evita que el proceso de fondo del host tome la tarea antes que la prueba.
        await using var host = await Spec020Host.StartAsync(new Dictionary<string, string?> { ["TareasProgramadas:IntervaloSegundos"] = "3600" });
        var (requestId, quotes, _) = await host.QuotedRequestAsync(1);
        (await host.ChooseAsync(requestId, quotes[0])).EnsureSuccessStatusCode();
        var tasks = host.Database.GetCollection<BsonDocument>("tareas_programadas");
        var expiry = new BsonDocument { { "tipo", "VencerSolicitud" }, { "claveNegocio", requestId.ToString() } };
        await tasks.UpdateOneAsync(expiry, new BsonDocument("$set", new BsonDocument("programadaPara", DateTime.UtcNow.AddMinutes(-1))));
        await SetAsync(host, requestId, new BsonDocument("venceEn", DateTime.UtcNow.AddMinutes(-1)));
        using var scope = host.Services.CreateScope();
        var executor = scope.ServiceProvider.GetRequiredService<EjecutorDeTareasProgramadas>();
        var executed = 0;

        while (executed < 10 && await executor.ExecuteOneAsync(CancellationToken.None))
        {
            executed++;
        }

        (await tasks.Find(expiry).SingleAsync())["estado"].AsString.Should().Be("hecha");
        (await StateOfAsync(host, requestId)).Should().Be("cita_confirmada");
        (await ExpiredEventsAsync(host, requestId)).Should().Be(0);
    }

    [Fact]
    public async Task Vence_Una_Sola_Vez_CA_029_08()
    {
        await using var host = await Spec020Host.StartAsync();
        var id = await CreateAsync(host);
        await SetAsync(host, id, new BsonDocument("venceEn", DateTime.UtcNow.AddHours(-1)));

        await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            using var scope = host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<VencerSolicitud>().HandleAsync(ExpiryTask(id), CancellationToken.None);
        }));

        (await StateOfAsync(host, id)).Should().Be("vencida");
        (await ExpiredEventsAsync(host, id)).Should().Be(1);
    }

    [Fact]
    public async Task Barrido_De_Respaldo_Por_Lotes_CA_029_09()
    {
        await using var host = await Spec020Host.StartAsync();
        var requests = host.Database.GetCollection<BsonDocument>("solicitudes");
        var template = await requests.Find(new BsonDocument("_id", await CreateAsync(host))).SingleAsync();
        await requests.DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        var documents = Enumerable.Range(0, 150).Select(_ =>
        {
            var copy = template.DeepClone().AsBsonDocument;
            copy["_id"] = ObjectId.GenerateNewId();
            copy["venceEn"] = DateTime.UtcNow.AddHours(-1);
            return copy;
        }).ToList();
        await requests.InsertManyAsync(documents);
        var task = new TareaReservada(ObjectId.GenerateNewId(), "BarrerVencidas", "recurrente:inicial", "x", 0);

        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<BarrerVencidas>().HandleAsync(task, CancellationToken.None);
        }

        (await requests.CountDocumentsAsync(new BsonDocument("estado", "vencida"))).Should().Be(100, "un barrido procesa 100");
        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<BarrerVencidas>().HandleAsync(task, CancellationToken.None);
        }

        (await requests.CountDocumentsAsync(new BsonDocument("estado", "vencida"))).Should().Be(150, "el siguiente procesa las 50 restantes");
        (await host.Database.GetCollection<BsonDocument>("tareas_programadas").CountDocumentsAsync(new BsonDocument("tipo", "BarrerVencidas"))).Should().BeGreaterThan(0, "se reprograma");
    }
}
