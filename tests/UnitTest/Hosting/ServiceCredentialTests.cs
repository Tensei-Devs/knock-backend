using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.Hosting.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Knock.UnitTest.Hosting;

/// <summary>
/// Spec 056 (T-02, T-05, T-07): la validación única de la credencial de servicio de <c>Knock.Hosting</c>. Todas las claves son sintéticas y se generan al azar en cada ejecución;
/// nunca se escriben en disco ni aparecen en un mensaje.
/// </summary>
public sealed class ServiceCredentialTests
{
    private const string _marketIssuer = "knock-market";
    private const string _identityIssuer = "knock-identity";
    private const string _audience = "knock-test-receiver";
    private const string _marketKeySetting = "Security:InternalTokens:MarketKey";
    private const string _identityKeySetting = "Security:InternalTokens:IdentityKey";

    private static readonly DateTimeOffset _now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly byte[] _marketKey = RandomNumberGenerator.GetBytes(32);
    private readonly byte[] _identityKey = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public async Task Sin_Credencial_Otra_Clave_O_Algoritmo_Distinto_Rechaza_CA_056_01()
    {
        var parameters = Parameters();

        (await Validate(Issue(_marketIssuer, RandomNumberGenerator.GetBytes(32)), parameters)).IsValid.Should().BeFalse("firmada con otra clave");
        (await Validate(Issue(_marketIssuer, RandomNumberGenerator.GetBytes(64), algorithm: SecurityAlgorithms.HmacSha512), parameters)).IsValid.Should().BeFalse("algoritmo distinto de HS256");
        (await Validate(string.Empty, parameters)).IsValid.Should().BeFalse("sin credencial");
        (await Validate(Unsigned(), parameters)).IsValid.Should().BeFalse("sin firma");
        (await Validate(Issue(_marketIssuer, _marketKey), parameters)).IsValid.Should().BeTrue("control: la credencial válida sí pasa");
    }

    [Fact]
    public async Task Claims_Se_Validan_Emisor_Destinatario_Vencida_Vida_Y_Jti_CA_056_02()
    {
        var parameters = Parameters();

        (await Validate(Issue("knock-otro", _marketKey), parameters)).IsValid.Should().BeFalse("iss desconocido");
        (await Validate(Issue(_marketIssuer, _marketKey, audience: "knock-otro"), parameters)).IsValid.Should().BeFalse("aud ajeno");
        (await Validate(Issue(_marketIssuer, _marketKey, issuedAt: DateTime.UtcNow.AddMinutes(-10), lifetime: TimeSpan.FromMinutes(1)), parameters)).IsValid.Should().BeFalse("vencida");
        var long6 = await Validate(Issue(_marketIssuer, _marketKey, lifetime: TimeSpan.FromMinutes(6)), parameters);
        long6.IsValid.Should().BeTrue("la firma y el tiempo valen; la vida máxima la corta HasBoundedLifetime");
        ServiceCredential.HasBoundedLifetime(Id(long6), Claim(long6, "iat"), Claim(long6, "exp")).Should().BeFalse("vida mayor a 5 minutos");
        var noJti = await Validate(Issue(_marketIssuer, _marketKey, withJti: false), parameters);
        ServiceCredential.HasBoundedLifetime(Id(noJti), Claim(noJti, "iat"), Claim(noJti, "exp")).Should().BeFalse("sin jti");
        var good = await Validate(Issue(_marketIssuer, _marketKey, lifetime: TimeSpan.FromMinutes(5)), parameters);
        ServiceCredential.HasBoundedLifetime(Id(good), Claim(good, "iat"), Claim(good, "exp")).Should().BeTrue("cinco minutos exactos valen");
    }

    [Fact]
    public async Task Un_Jwt_De_Usuario_Con_Otra_Clave_No_Es_Credencial_De_Servicio_CA_056_04()
    {
        var userSigningKey = RandomNumberGenerator.GetBytes(32);

        (await Validate(Issue("knock-identity", userSigningKey, audience: "knock-market"), Parameters())).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Un_Emisor_No_Puede_Firmar_Como_Otro_CA_056_05()
    {
        var parameters = Parameters();

        (await Validate(Issue(_marketIssuer, _identityKey), parameters)).IsValid.Should().BeFalse("iss de Mercado firmado con la clave de Identidad");
        (await Validate(Issue(_identityIssuer, _marketKey), parameters)).IsValid.Should().BeFalse("iss de Identidad firmado con la clave de Mercado");
        (await Validate(Issue(_identityIssuer, _identityKey), parameters)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-base64!!")]
    [InlineData("AAAA")]
    public async Task Clave_Invalida_Falla_Cerrado_Sin_Repetir_El_Valor_CA_056_06(string? configured)
    {
        var settings = new Dictionary<string, string?> { [_marketKeySetting] = configured, [_identityKeySetting] = Convert.ToBase64String(_identityKey) };
        var parameters = ServiceCredential.CreateValidationParameters(Settings(), Configuration(settings));

        var result = await Validate(Issue(_marketIssuer, _marketKey), parameters);

        result.IsValid.Should().BeFalse();
        if (!string.IsNullOrEmpty(configured))
        {
            (result.Exception?.ToString() ?? string.Empty).Should().NotContain(configured);
        }

        (await Validate(Issue(_identityIssuer, _identityKey), parameters)).IsValid.Should().BeTrue("el emisor con clave válida no se ve afectado");
        ServiceCredential.TryReadKey(configured).Should().BeNull();
        ServiceCredential.ReadKey(configured).Should().HaveCount(32).And.NotEqual(ServiceCredential.ReadKey(configured), "la clave al azar nadie la conoce");
    }

    [Fact]
    public async Task Segunda_Presentacion_Dentro_De_La_Vida_Se_Acepta_Y_Vencida_Se_Rechaza_CA_056_10()
    {
        var parameters = Parameters();
        var credential = Issue(_marketIssuer, _marketKey, lifetime: TimeSpan.FromSeconds(30));

        (await Validate(credential, parameters)).IsValid.Should().BeTrue();
        (await Validate(credential, parameters)).IsValid.Should().BeTrue("sin jti guardado: la repetición se acepta y los receptores son idempotentes");
        var expired = Issue(_marketIssuer, _marketKey, issuedAt: DateTime.UtcNow.AddMinutes(-2), lifetime: TimeSpan.FromSeconds(30));
        (await Validate(expired, parameters)).IsValid.Should().BeFalse("después de su vencimiento");
    }

    [Fact]
    public async Task Clave_Anterior_Se_Acepta_Hasta_El_Fin_De_La_Ventana_De_24_Horas_CA_056_11()
    {
        var previous = RandomNumberGenerator.GetBytes(32);
        var settings = new Dictionary<string, string?>
        {
            [_marketKeySetting] = Convert.ToBase64String(_marketKey),
            [_marketKeySetting + ServiceCredential.PreviousSuffix] = Convert.ToBase64String(previous),
            [_marketKeySetting + ServiceCredential.PreviousUntilSuffix] = _now.AddHours(24).ToString("O"),
            [_identityKeySetting] = Convert.ToBase64String(_identityKey),
        };
        var clock = new FixedTime(_now);
        var parameters = ServiceCredential.CreateValidationParameters(Settings(), Configuration(settings), clock);
        var withPrevious = Issue(_marketIssuer, previous);
        var withCurrent = Issue(_marketIssuer, _marketKey);

        (await Validate(withPrevious, parameters)).IsValid.Should().BeTrue("dentro de las 24 horas");
        (await Validate(withCurrent, parameters)).IsValid.Should().BeTrue("la vigente se acepta siempre");

        clock.Now = _now.AddHours(24).AddSeconds(1);

        (await Validate(withPrevious, parameters)).IsValid.Should().BeFalse("pasada la ventana");
        (await Validate(withCurrent, parameters)).IsValid.Should().BeTrue("la vigente se acepta siempre");
    }

    [Fact]
    public async Task Clave_Anterior_Sin_Fecha_De_Retiro_No_Vale_CA_056_11()
    {
        var previous = RandomNumberGenerator.GetBytes(32);
        var settings = new Dictionary<string, string?>
        {
            [_marketKeySetting] = Convert.ToBase64String(_marketKey),
            [_marketKeySetting + ServiceCredential.PreviousSuffix] = Convert.ToBase64String(previous),
            [_identityKeySetting] = Convert.ToBase64String(_identityKey),
        };

        (await Validate(Issue(_marketIssuer, previous), ServiceCredential.CreateValidationParameters(Settings(), Configuration(settings)))).IsValid.Should().BeFalse("falla cerrado");
    }

    private static string? Id(TokenValidationResult result) => Claim(result, "jti");

    private static string? Claim(TokenValidationResult result, string name) =>
        result.Claims.TryGetValue(name, out var value) ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) : null;

    private static Task<TokenValidationResult> Validate(string token, TokenValidationParameters parameters) => new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);

    private static IConfiguration Configuration(IDictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ServiceCredentialSettings Settings() => new()
    {
        Scheme = "Test",
        Audience = _audience,
        IssuerKeyConfigurationKeys = new Dictionary<string, string> { [_marketIssuer] = _marketKeySetting, [_identityIssuer] = _identityKeySetting },
        WriteProblem = (_, _, _, _) => Task.CompletedTask,
        UnauthorizedCode = "unauthorized",
    };

    private TokenValidationParameters Parameters() => ServiceCredential.CreateValidationParameters(
        Settings(),
        Configuration(new Dictionary<string, string?> { [_marketKeySetting] = Convert.ToBase64String(_marketKey), [_identityKeySetting] = Convert.ToBase64String(_identityKey) }));

    private static string Issue(
        string issuer,
        byte[] key,
        string audience = _audience,
        TimeSpan? lifetime = null,
        DateTime? issuedAt = null,
        bool withJti = true,
        string algorithm = SecurityAlgorithms.HmacSha256)
    {
        var issued = issuedAt ?? DateTime.UtcNow;
        var claims = new Dictionary<string, object> { ["permisos"] = new[] { "prueba" } };
        if (withJti)
        {
            claims["jti"] = Guid.NewGuid().ToString("N");
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Claims = claims,
            IssuedAt = issued,
            NotBefore = issued.AddSeconds(-1),
            Expires = issued + (lifetime ?? TimeSpan.FromMinutes(1)),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), algorithm),
        });
    }

    private static string Unsigned() => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor { Issuer = _marketIssuer, Audience = _audience, Expires = DateTime.UtcNow.AddMinutes(1) });

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
