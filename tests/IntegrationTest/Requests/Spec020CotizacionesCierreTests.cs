extern alias MarketApi;

using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Modules.Requests.Services;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>Spec 020 T-11: cotización duplicada y cupo bajo concurrencia, y registros sin datos personales del flujo de cotizaciones. Contra MongoDB y Redis reales.</summary>
public sealed class Spec020CotizacionesCierreTests
{
    [Fact]
    public async Task Cotizacion_Duplicada_Simultanea_Solo_Crea_Una_CA_020_07()
    {
        using var factory = await MarketApiFactory.CreateAsync();
        var id = await QuoteHttp.Request(factory);
        using var client = await QuoteHttp.Technician(factory);
        var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => client.PostAsJsonAsync(QuoteHttp.Route(id), QuoteHttp.Body())));
        try
        {
            responses.Count(x => x.StatusCode == HttpStatusCode.Created).Should().Be(1);
            responses.Where(x => x.StatusCode != HttpStatusCode.Created).Should().OnlyContain(x => x.StatusCode == HttpStatusCode.Conflict);
            foreach (var conflict in responses.Where(x => x.StatusCode == HttpStatusCode.Conflict)) await QuoteHttp.Code(conflict, "quote_duplicate");
            (await factory.Quotes.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(1);
            var messages = await factory.Database.GetCollection<BsonDocument>("outbox").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("datos.solicitudId", id.ToString()));
            messages.Should().Be(1, "solo la cotización guardada emite evento");
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Cupo_Lleno_Con_Seis_Simultaneas_Deja_Exactamente_Cinco_CA_020_19()
    {
        using var factory = await MarketApiFactory.CreateAsync();
        var id = await QuoteHttp.Request(factory);
        var clients = new List<HttpClient>();
        try
        {
            for (var i = 0; i < 6; i++) clients.Add(await QuoteHttp.Technician(factory));
            var responses = await Task.WhenAll(clients.Select(c => c.PostAsJsonAsync(QuoteHttp.Route(id), QuoteHttp.Body())));
            try
            {
                responses.Count(x => x.StatusCode == HttpStatusCode.Created).Should().Be(5);
                responses.Count(x => x.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
                (await factory.Quotes.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(5);
            }
            finally { foreach (var response in responses) response.Dispose(); }
        }
        finally { foreach (var client in clients) client.Dispose(); }
    }

    [Fact]
    public async Task Cotizar_No_Registra_La_Nota_CA_020_17()
    {
        using var factory = await MarketApiFactory.CreateAsync(configureServices: services => services.AddSingleton<INoteModerator>(new ApprovingNoteModerator()));
        var id = await QuoteHttp.Request(factory);
        using var client = await QuoteHttp.Technician(factory);
        var note = "Nota-ficticia-" + Guid.NewGuid().ToString("N");
        var output = await ConsoleCapture.RunAsync(async () =>
        {
            using var created = await client.PostAsJsonAsync(QuoteHttp.Route(id), new
            {
                tipoPrecio = "fijo",
                precio = 123,
                dia = DateOnly.FromDateTime(DateTime.UtcNow),
                hora = "12:00",
                nota = note,
            });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            using var duplicate = await client.PostAsJsonAsync(QuoteHttp.Route(id), QuoteHttp.Body());
            duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        });
        output.Should().NotContain(note);
    }
}

/// <summary>Moderador de notas que aprueba cualquier nota: el real falla cerrado (503) sin el servicio de moderación.</summary>
internal sealed class ApprovingNoteModerator : INoteModerator
{
    public Task<NoteModerationResult> ModerateAsync(string note, CancellationToken cancellationToken) => Task.FromResult(NoteModerationResult.Approved);
}
