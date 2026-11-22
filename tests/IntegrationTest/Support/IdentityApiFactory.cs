extern alias IdentityApi;

using System.Net;
using System.Security.Cryptography;
using IdentityApi::Knock.Identity.Api.Modules.Panel.Data;
using IdentityApi::Knock.Identity.Api.Modules.Sesiones.Data;
using IdentityApi::Knock.Identity.Api.Modules.Sesiones.Services;
using IdentityApi::Knock.Identity.Api.Modules.Trust.Crypto;
using IdentityApi::Knock.Identity.Api.Modules.Trust.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;

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
    private static readonly byte[] _fieldEncryptionKey = RandomNumberGenerator.GetBytes(32);
    private static readonly byte[] _curpHmacKey = RandomNumberGenerator.GetBytes(32);

    /// <summary>Servicios que la prueba sustituye en el host (por ejemplo, el catálogo de colonias de Mercado).</summary>
    public Action<IServiceCollection>? ExtraServices { get; private init; }

    public string DatabaseName => settings["Mongo:Databases:Identity"]!;

    public static string SigningKeyBase64 => Convert.ToBase64String(_signingKey);

    public IMongoDatabase Database => Services.GetRequiredService<IMongoClient>().GetDatabase(DatabaseName);

    public IMongoCollection<BsonDocument> Users => Database.GetCollection<BsonDocument>(SesionesStore.UsersCollection);

    /// <summary>Redis del host: la lista de sesiones canceladas (<c>sesion-cancelada:{sid}</c>).</summary>
    public IDatabase Redis => Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();

    public IMongoCollection<BsonDocument> Sessions => Database.GetCollection<BsonDocument>(SesionesStore.SessionsCollection);

    /// <summary>Crea el host con una base propia y sus índices. <paramref name="overrides"/> reemplaza cualquier valor de configuración.</summary>
    public static async Task<IdentityApiFactory> CreateAsync(IReadOnlyDictionary<string, string?>? overrides = null, Action<IServiceCollection>? configureServices = null)
    {
        var mongo = await DocumentDbFixture.SharedAsync();
        var redis = await RedisTestFixture.SharedAsync();
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:KnockMongo"] = mongo.ConnectionString,
            // Redis real del contenedor de pruebas (con la contraseña de un solo uso que arma el fixture): Identity la valida al arrancar (spec 003)
            // y la lista de sesiones canceladas (spec 016) se escribe y se lee aquí.
            ["ConnectionStrings:KnockRedis"] = redis.ConnectionString,
            ["Mongo:Databases:Identity"] = "identity_it_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6)),
        };
        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        var factory = new IdentityApiFactory(values) { ExtraServices = configureServices };
        await factory.Services.GetRequiredService<SesionesStore>().EnsureIndexesAsync(CancellationToken.None);
        await factory.Services.GetRequiredService<PanelCuentasStore>().EnsureIndexesAsync(CancellationToken.None);
        await factory.Services.GetRequiredService<PanelSesionesStore>().EnsureIndexesAsync(CancellationToken.None);
        await factory.Services.GetRequiredService<PanelActivacionesStore>().EnsureIndexesAsync(CancellationToken.None);
        await factory.Services.GetRequiredService<PanelAuditoriaStore>().EnsureIndexesAsync(CancellationToken.None);
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
                // Cifrado de campo de la dirección (spec 040): llave sintética por proceso; en producción llega de Key Vault.
                ["Security:FieldEncryption:ActiveVersion"] = "1",
                ["Security:FieldEncryption:Keys:1"] = Convert.ToBase64String(_fieldEncryptionKey),
                // Llave HMAC de la CURP (spec 025): sintética por proceso; en producción llega de Key Vault.
                ["Verificacion:CurpHmac:ActiveVersion"] = "1",
                ["Verificacion:CurpHmac:Keys:1"] = Convert.ToBase64String(_curpHmacKey),
            };
            foreach (var (key, value) in settings)
            {
                values[key] = value;
            }

            configuration.AddInMemoryCollection(values);
        });
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter>(new RemoteIpStartupFilter());
            // Sin Key Vault real (bloqueo: costo) el envoltorio de llaves de la verificación falla cerrado; el host de pruebas usa uno en memoria.
            services.Replace(ServiceDescriptor.Singleton<IVerificationKeyWrapper>(new MaskVerificationKeyWrapper()));
            services.Replace(ServiceDescriptor.Singleton<IVerificationBlobStore>(new LinksOnlyVerificationBlobStore()));
            ExtraServices?.Invoke(services);
        });
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

/// <summary>Envoltorio de llaves de verificación en memoria para el host de pruebas (máscara XOR aleatoria por proceso; no es criptografía de producción).</summary>
internal sealed class MaskVerificationKeyWrapper : IVerificationKeyWrapper
{
    private static readonly byte[] _mask = RandomNumberGenerator.GetBytes(64);

    public Task<WrappedVerificationKey> WrapAsync(byte[] dataKey, CancellationToken cancellationToken) =>
        Task.FromResult(new WrappedVerificationKey(Convert.ToBase64String(Xor(dataKey)), "test-kek"));

    public Task<byte[]> UnwrapAsync(WrappedVerificationKey wrapped, CancellationToken cancellationToken) =>
        Task.FromResult(Xor(Convert.FromBase64String(wrapped.Value)));

    private static byte[] Xor(byte[] data) => [.. data.Select((b, i) => (byte)(b ^ _mask[i % _mask.Length]))];
}

/// <summary>Almacén privado de fotos para el host de pruebas: solo emite enlaces (sin Blob real, bloqueo: costo); no guarda contenido, así que lecturas y escrituras no encuentran nada.</summary>
internal sealed class LinksOnlyVerificationBlobStore : IVerificationBlobStore
{
    public Task<Uri> IssueQuarantineUploadLinkAsync(string blobName, TimeSpan lifetime, CancellationToken cancellationToken) => Task.FromResult(new Uri("https://almacen.invalid/q/" + blobName));

    public Task<Uri> IssueFinalReadLinkAsync(string blobName, TimeSpan lifetime, CancellationToken cancellationToken) => Task.FromResult(new Uri("https://almacen.invalid/f/" + blobName));

    public Task<byte[]?> ReadQuarantineAsync(string blobName, int maxBytes, CancellationToken cancellationToken) => Task.FromResult<byte[]?>(null);

    public Task<byte[]?> ReadFinalAsync(string blobName, CancellationToken cancellationToken) => Task.FromResult<byte[]?>(null);

    public Task<bool> WriteFinalAsync(string blobName, byte[] content, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task DeleteQuarantineAsync(string blobName, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DeleteFinalAsync(string blobName, CancellationToken cancellationToken) => Task.CompletedTask;
}
