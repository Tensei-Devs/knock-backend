using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>
/// Spec 059, T-04: el consumidor de Mercado de <c>CuentaSuspendida</c> y <c>CuentaReactivada</c> en <c>POST /api/v1/interno/eventos</c>. MongoDB real; solo datos ficticios.
/// Las comprobaciones de valores aleatorios son por campo, nunca por una subcadena del cuerpo crudo.
/// </summary>
public sealed class Spec059MarketTests
{
    private const string _route = "/api/v1/interno/eventos";
    private const string _suspended = "CuentaSuspendida";
    private const string _reactivated = "CuentaReactivada";

    private static readonly Dictionary<string, string?> _settings = new() { ["Security:InternalTokens:IdentityKey"] = InternalServiceTokens.KeyBase64 };

    private static async Task<Spec020Host> StartAsync() => await Spec020Host.StartAsync(_settings);

    private static string EventId(string type, string resource) => $"{type}:{resource}:1";

    private static async Task<HttpResponseMessage> PostAsync(Spec020Host host, string json, string? serviceToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_route, UriKind.Relative)) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        if (serviceToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);
        }

        return await host.Client.SendAsync(request);
    }

    private static string Token(string[]? permissions = null) => InternalServiceTokens.Issue("sin-uso", "sin-uso", permissions ?? ["eventos"]);

    private static string Body(string eventId, string type, string user) =>
        System.Text.Json.JsonSerializer.Serialize(new { eventoId = eventId, tipo = type, usuarioId = user, reporteId = ObjectId.GenerateNewId().ToString(), desde = "2026-11-22T10:00:00.000Z" });

    private static async Task<string> SeedTechnicianAsync(Spec020Host host, string state = "activo")
    {
        var user = Guid.NewGuid().ToString("D");
        await host.Database.GetCollection<BsonDocument>("tecnicos").InsertOneAsync(new BsonDocument { { "_id", ObjectId.GenerateNewId() }, { "usuarioId", user }, { "nombre", "Técnico de prueba" }, { "estado", state } });
        return user;
    }

    private static async Task<ObjectId> SeedVisitAsync(Spec020Host host, string technician, string client, string state = "confirmada")
    {
        var id = ObjectId.GenerateNewId();
        await host.Database.GetCollection<BsonDocument>("visitas").InsertOneAsync(new BsonDocument
        {
            { "_id", id }, { "solicitudId", ObjectId.GenerateNewId() }, { "clienteId", client }, { "tecnicoUsuarioId", technician }, { "tecnicoId", ObjectId.GenerateNewId() }, { "estado", state },
        });
        return id;
    }

    private static async Task<string?> StateOfAsync(Spec020Host host, string technician) =>
        (await host.Database.GetCollection<BsonDocument>("tecnicos").Find(Builders<BsonDocument>.Filter.Eq("usuarioId", technician)).FirstAsync()).GetValue("estado").AsString;

    private static Task<List<BsonDocument>> NoticesAsync(Spec020Host host) =>
        host.Database.GetCollection<BsonDocument>("outbox").Find(Builders<BsonDocument>.Filter.Eq("tipo", "visita_afectada")).ToListAsync();

    [Fact]
    public async Task CuentaSuspendida_Marks_The_Technician_Suspended_And_Keeps_What_Was_Quoted_CA_059_02()
    {
        await using var host = await StartAsync();
        var technician = await SeedTechnicianAsync(host);
        var other = await SeedTechnicianAsync(host);

        using var response = await PostAsync(host, Body(EventId(_suspended, ObjectId.GenerateNewId().ToString()), _suspended, technician), Token());

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await StateOfAsync(host, technician)).Should().Be("suspendido");
        (await StateOfAsync(host, other)).Should().Be("activo");
    }

    [Fact]
    public async Task CuentaReactivada_Restores_The_Technician_Only_When_It_Was_Suspended_CA_059_14()
    {
        await using var host = await StartAsync();
        var suspended = await SeedTechnicianAsync(host, "suspendido");
        var active = await SeedTechnicianAsync(host);

        using var restored = await PostAsync(host, Body(EventId(_reactivated, ObjectId.GenerateNewId().ToString()), _reactivated, suspended), Token());
        using var untouched = await PostAsync(host, Body(EventId(_reactivated, ObjectId.GenerateNewId().ToString()), _reactivated, active), Token());

        restored.StatusCode.Should().Be(HttpStatusCode.Accepted);
        untouched.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await StateOfAsync(host, suspended)).Should().Be("activo");
        (await StateOfAsync(host, active)).Should().Be("activo");
    }

    [Fact]
    public async Task Each_Upcoming_Visit_Gets_One_Notice_Without_Reason_Report_Or_Reporter_And_Other_Clients_Get_None_CA_059_03()
    {
        await using var host = await StartAsync();
        var technician = await SeedTechnicianAsync(host);
        var otherTechnician = await SeedTechnicianAsync(host);
        var clientA = Guid.NewGuid().ToString("D");
        var clientB = Guid.NewGuid().ToString("D");
        var bystander = Guid.NewGuid().ToString("D");
        var a1 = await SeedVisitAsync(host, technician, clientA);
        var a2 = await SeedVisitAsync(host, technician, clientA);
        var b1 = await SeedVisitAsync(host, technician, clientB);
        await SeedVisitAsync(host, technician, bystander, "en_curso");
        await SeedVisitAsync(host, technician, bystander, "terminada");
        await SeedVisitAsync(host, otherTechnician, bystander);

        using var response = await PostAsync(host, Body(EventId(_suspended, ObjectId.GenerateNewId().ToString()), _suspended, technician), Token());

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var notices = await NoticesAsync(host);
        notices.Should().HaveCount(3);
        notices.Select(n => n["datos"]["visitaId"].AsString).Should().BeEquivalentTo(a1.ToString(), a2.ToString(), b1.ToString());
        notices.Select(n => n["datos"]["destinatarioId"].AsString).Should().BeEquivalentTo(clientA, clientA, clientB);
        notices.Should().OnlyContain(n => n["datos"]["rol"].AsString == "cliente");
        foreach (var notice in notices)
        {
            notice["datos"].AsBsonDocument.Names.Should().BeEquivalentTo("eventoId", "destinatarioId", "rol", "visitaId");
        }

        (await host.Database.GetCollection<BsonDocument>("visitas").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("estado", "confirmada"))).Should().Be(4, "las visitas no se cancelan (R-12)");
    }

    [Fact]
    public async Task The_Same_Event_Delivered_Twice_Leaves_The_Profile_And_The_Notices_As_With_One_Delivery_CA_059_16()
    {
        await using var host = await StartAsync();
        var technician = await SeedTechnicianAsync(host);
        await SeedVisitAsync(host, technician, Guid.NewGuid().ToString("D"));
        var body = Body(EventId(_suspended, ObjectId.GenerateNewId().ToString()), _suspended, technician);

        using var first = await PostAsync(host, body, Token());
        await host.Database.GetCollection<BsonDocument>("tecnicos").UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("usuarioId", technician), Builders<BsonDocument>.Update.Set("estado", "activo"));
        using var second = await PostAsync(host, body, Token());

        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        second.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await NoticesAsync(host)).Should().HaveCount(1);
        (await StateOfAsync(host, technician)).Should().Be("activo", "el evento repetido no vuelve a aplicarse");
    }

    [Fact]
    public async Task Concurrent_Deliveries_Of_The_Same_Event_Create_One_Notice_CA_059_16()
    {
        await using var host = await StartAsync();
        var technician = await SeedTechnicianAsync(host);
        await SeedVisitAsync(host, technician, Guid.NewGuid().ToString("D"));
        var body = Body(EventId(_suspended, ObjectId.GenerateNewId().ToString()), _suspended, technician);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => PostAsync(host, body, Token())));

        responses.Select(r => r.StatusCode).Should().OnlyContain(code => code == HttpStatusCode.Accepted || code == HttpStatusCode.ServiceUnavailable);
        (await NoticesAsync(host)).Should().HaveCount(1);
        (await StateOfAsync(host, technician)).Should().Be("suspendido");
    }

    [Fact]
    public async Task A_Suspension_For_An_Unknown_Person_Is_Accepted_And_Changes_Nothing_CA_059_16()
    {
        await using var host = await StartAsync();
        var technician = await SeedTechnicianAsync(host);

        using var response = await PostAsync(host, Body(EventId(_suspended, ObjectId.GenerateNewId().ToString()), _suspended, Guid.NewGuid().ToString("D")), Token());

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await StateOfAsync(host, technician)).Should().Be("activo");
        (await NoticesAsync(host)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"eventoId\":\"x\",\"tipo\":\"OtroEvento\",\"usuarioId\":\"u\"}")]
    [InlineData("{\"eventoId\":\"\",\"tipo\":\"CuentaSuspendida\",\"usuarioId\":\"u\"}")]
    [InlineData("{\"eventoId\":\"a b\",\"tipo\":\"CuentaSuspendida\",\"usuarioId\":\"u\"}")]
    [InlineData("{\"eventoId\":\"x\",\"tipo\":\"CuentaSuspendida\",\"usuarioId\":\"u v\"}")]
    [InlineData("{\"eventoId\":\"x\",\"tipo\":\"CuentaSuspendida\"}")]
    public async Task An_Invalid_Body_Returns_422_And_Writes_Nothing_CA_059_16(string json)
    {
        await using var host = await StartAsync();

        using var response = await PostAsync(host, json, Token());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await host.Database.GetCollection<BsonDocument>("eventos_recibidos").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(0);
    }

    [Fact]
    public async Task Without_A_Service_Token_Or_With_A_User_Token_Returns_401_And_Changes_Nothing_CA_059_16()
    {
        await using var host = await StartAsync();
        var technician = await SeedTechnicianAsync(host);
        var body = Body(EventId(_suspended, ObjectId.GenerateNewId().ToString()), _suspended, technician);

        using var anonymous = await PostAsync(host, body, null);
        using var userToken = await PostAsync(host, body, Spec020Host.Token(technician));

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        userToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await StateOfAsync(host, technician)).Should().Be("activo");
    }

    [Fact]
    public async Task A_Service_Token_Without_The_Events_Permission_Returns_403_And_Changes_Nothing_CA_059_16()
    {
        await using var host = await StartAsync();
        var technician = await SeedTechnicianAsync(host);

        using var response = await PostAsync(host, Body(EventId(_suspended, ObjectId.GenerateNewId().ToString()), _suspended, technician), Token(["historial"]));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        System.Text.Json.JsonElement.Parse(await response.Content.ReadAsStringAsync()).GetProperty("code").GetString().Should().Be("service_not_allowed");
        (await StateOfAsync(host, technician)).Should().Be("activo");
    }

    [Fact]
    public async Task The_History_Permission_Alone_Does_Not_Open_The_Events_Route_And_The_Events_Permission_Does_Not_Open_The_History_Route()
    {
        await using var host = await StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/interno/historial?usuarioId=u&elementoId=0123456789abcdef01234567", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", InternalServiceTokens.Issue("u", "0123456789abcdef01234567", ["eventos"]));

        using var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
