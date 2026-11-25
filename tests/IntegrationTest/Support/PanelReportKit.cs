extern alias IdentityApi;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using IdentityApi::Knock.Identity.Api.Modules.Panel.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.IntegrationTest.Support;

/// <summary>
/// Lo que las pruebas de la spec 059 necesitan para decidir un reporte por la ruta real del panel (<c>POST /api/v1/admin/reportes/{id}/decision</c>, spec 053): la llave del panel de la prueba, tokens del panel,
/// cuentas del panel con roles y reportes abiertos sembrados. Solo datos ficticios; las llaves y los identificadores son aleatorios por proceso y nunca se escriben en disco.
/// </summary>
internal static class PanelReportKit
{
    public const string Issuer = "knock-api";
    public const string PanelAudience = "knock-panel";

    private static readonly byte[] _panelKey = RandomNumberGenerator.GetBytes(32);

    /// <summary>Configuración que enciende el panel en el host de pruebas (en Production, sin llave, el panel queda apagado).</summary>
    public static IReadOnlyDictionary<string, string?> HostSettings { get; } = new Dictionary<string, string?> { ["PanelJwt:SigningKey"] = Convert.ToBase64String(_panelKey) };

    /// <summary>Token del panel como el que emite la sesión tras la doble verificación: HS256, <c>sub</c>, <c>sid</c> y <c>pv</c>. Cada parámetro en <see langword="null"/> omite ese dato.</summary>
    public static string Token(string? sub, string? sid = "", int? permissionsVersion = 1, string audience = PanelAudience, byte[]? key = null)
    {
        var claims = new Dictionary<string, object>();
        if (sub is not null)
        {
            claims["sub"] = sub;
        }

        if (sid is not null)
        {
            claims["sid"] = sid.Length == 0 ? ObjectId.GenerateNewId().ToString() : sid;
        }

        if (permissionsVersion is { } version)
        {
            claims["pv"] = version;
        }

        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Claims = claims,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(15),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key ?? _panelKey), SecurityAlgorithms.HmacSha256),
        });
    }

    /// <summary>Crea una cuenta del panel con los roles dados (versión de permisos 1) y devuelve su <c>sub</c>; sin <paramref name="sub"/> usa uno nuevo.</summary>
    public static async Task<string> SeedPanelAccountAsync(IServiceProvider services, string[] roles, string? sub = null)
    {
        var owner = sub ?? Guid.NewGuid().ToString("D");
        await services.GetRequiredService<IPanelCuentasStore>().InsertAsync(new NuevaCuentaPanel(owner, "Moderadora de prueba", roles, DateTime.UtcNow), CancellationToken.None);
        return owner;
    }

    /// <summary>Siembra una cuenta de la app (técnica o cliente) con nombre y celular ficticios y devuelve su <c>_id</c> y su <c>sub</c>.</summary>
    public static async Task<(ObjectId Id, string Sub)> SeedAccountAsync(IMongoCollection<BsonDocument> users, bool technician, string state = "activa", string name = "Demo Reportado")
    {
        var id = ObjectId.GenerateNewId();
        var sub = Guid.NewGuid().ToString("D");
        await users.InsertOneAsync(new BsonDocument
        {
            { "_id", id }, { "sub", sub }, { "nombre", name }, { "esTecnico", technician }, { "estado", state }, { "perfilCompleto", true },
            { "creadoEn", new BsonDateTime(DateTime.UtcNow) }, { "version", 1 }, { "celularHash", new BsonBinaryData(RandomNumberGenerator.GetBytes(32)) },
        });
        return (id, sub);
    }

    /// <summary>Siembra un reporte <c>abierto</c> de <paramref name="reporterId"/> contra <paramref name="reportedId"/> y devuelve su identificador.</summary>
    public static async Task<string> SeedReportAsync(IMongoDatabase database, string reporterId, string reportedId, string state = "abierto", string? severity = null)
    {
        var id = ObjectId.GenerateNewId();
        var report = new BsonDocument
        {
            { "_id", id }, { "reportaId", reporterId }, { "reportadoId", reportedId }, { "motivo", "conducta" }, { "descripcion", "Texto ficticio del reporte" },
            { "estado", state }, { "creadoEn", new BsonDateTime(DateTime.UtcNow) },
        };
        if (severity is not null)
        {
            report["gravedad"] = severity;
        }

        await database.GetCollection<BsonDocument>("reportes").InsertOneAsync(report);
        return id.ToString();
    }

    /// <summary>Decide el reporte por la ruta del panel con el token dado (o sin token). <paramref name="body"/> es el cuerpo JSON tal cual.</summary>
    public static async Task<HttpResponseMessage> DecideAsync(HttpClient client, string? token, string reportId, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/v1/admin/reportes/{reportId}/decision", UriKind.Relative)) { Content = JsonContent.Create(body) };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request);
    }

    /// <summary>Envía una petición al panel con el token dado; <paramref name="body"/> opcional va como JSON.</summary>
    public static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response) => JsonElement.Parse(await response.Content.ReadAsStringAsync());
}
