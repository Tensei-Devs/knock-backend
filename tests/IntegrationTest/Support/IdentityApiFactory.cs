extern alias IdentityApi;

using System.Net;
using System.Security.Cryptography;
using IdentityApi::Knock.Identity.Api.Modules.Sesiones.Data;
using IdentityApi::Knock.Identity.Api.Modules.Sesiones.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.IntegrationTest.Support;

/// <summary>
/// Host de pruebas de <c>Knock.Identity.Api</c> sobre documentdb-local (contenedor compartido): una base propia por host, con los índices de
/// la spec 015 creados, para que las pruebas no se pisen. El tipo <see cref="LoginService"/> solo ubica el ensamblado del servicio: así no
/// choca con el <c>Program</c> de otro servicio en este mismo proyecto. Solo datos ficticios.
/// </summary>
internal sealed class IdentityApiFactory(IReadOnlyDictionary<string, string?> settings) : WebApplicationFactory<LoginService>
{
    /// <summary>Encabezado que solo entiende el host de pruebas: fija <c>Connection.RemoteIpAddress</c> (TestServer no trae IP).</summary>
    public const string RemoteIpHeader = "X-Test-Remote-Ip";

    /// <summary>Límite alto por defecto: las pruebas que no miden el limitador no deben recibir 429 (el limitador sigue activo).</summary>
    private const string _highLimit = "1000000";

    // Clave de firma y llaves del celular sintéticas: 32 bytes aleatorios por proceso, nunca se escriben en disco.
    private static readonly byte[] _signingKey = RandomNumberGenerator.GetBytes(32);
    private static readonly byte[] _phoneEncryptionKey = RandomNumberGenerator.GetBytes(32);
    private static readonly byte[] _phoneHmacKey = RandomNumberGenerator.GetBytes(32);

    public string DatabaseName => settings["Mongo:Databases:Identity"]!;

    public IMongoDatabase Database => Services.GetRequiredService<IMongoClient>().GetDatabase(DatabaseName);

    public IMongoCollection<BsonDocument> Users => Database.GetCollection<BsonDocument>(SesionesStore.UsersCollection);

    public IMongoCollection<BsonDocument> Sessions => Database.GetCollection<BsonDocument>(SesionesStore.SessionsCollection);

    /// <summary>Crea el host con una base propia y sus índices. <paramref name="overrides"/> reemplaza cualquier valor de configuración.</summary>
    public static async Task<IdentityApiFactory> CreateAsync(IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var mongo = await DocumentDbFixture.SharedAsync();
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:KnockMongo"] = mongo.ConnectionString,
            // Identity valida su cadena de Redis al arrancar (spec 003); estas pruebas aún no usan Redis, solo necesitan que sea válida.
            // La contraseña se arma en cada ejecución: no hay ninguna literal en el repositorio.
            ["ConnectionStrings:KnockRedis"] = "127.0.0.1:6379,password=" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)),
            ["Mongo:Databases:Identity"] = "identity_it_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6)),
        };
        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        var factory = new IdentityApiFactory(values);
        await factory.Services.GetRequiredService<SesionesStore>().EnsureIndexesAsync(CancellationToken.None);
        return factory;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["Firebase:ProjectId"] = FirebaseEmulator.ProjectId,
                ["Jwt:Issuer"] = "knock-api",
                ["Jwt:Audience"] = "knock-app",
                ["Jwt:SigningKey"] = Convert.ToBase64String(_signingKey),
                ["RateLimiting:Global:PermitLimit"] = _highLimit,
                ["RateLimiting:Auth:PermitLimit"] = _highLimit,
                ["Identity:PhoneProtection:EncryptionKeys:1"] = Convert.ToBase64String(_phoneEncryptionKey),
                ["Identity:PhoneProtection:HmacKeys:1"] = Convert.ToBase64String(_phoneHmacKey),
                ["Identity:PhoneProtection:EncryptionKeyVersion"] = "1",
                ["Identity:PhoneProtection:HmacKeyVersion"] = "1",
            };
            foreach (var (key, value) in settings)
            {
                values[key] = value;
            }

            configuration.AddInMemoryCollection(values);
        });
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter>(new RemoteIpStartupFilter()));
    }
}

/// <summary>Pone la IP de conexión desde <see cref="IdentityApiFactory.RemoteIpHeader"/> antes del resto del pipeline de la API.</summary>
internal sealed class RemoteIpStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(IdentityApiFactory.RemoteIpHeader, out var value)
                    && IPAddress.TryParse(value.ToString(), out var address))
                {
                    context.Connection.RemoteIpAddress = address;
                }

                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
