using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;

namespace Knock.SecurityTest.Support;

/// <summary>
/// Fabrica JWT de acceso válidos y alterados de formas conocidas (alg none, firma ajena, vencido, otro emisor o destinatario…) con la clave de
/// prueba de los hosts. Los datos son sintéticos: las claves se generan al azar por proceso y nunca se escriben en disco.
/// </summary>
internal static class TokenForge
{
    public const string Issuer = "knock-api";
    public const string Audience = "knock-app";

    /// <summary>Clave de firma de los hosts de prueba (HS256, 32 bytes aleatorios por proceso).</summary>
    public static readonly byte[] SigningKey = RandomNumberGenerator.GetBytes(32);

    private static readonly byte[] _foreignKey = RandomNumberGenerator.GetBytes(32);

    /// <summary>HS512 exige una clave de al menos 64 bytes para firmar; es la clave buena repetida, como la probaría quien conoce la clave.</summary>
    private static readonly byte[] _doubledKey = [.. SigningKey, .. SigningKey];

    public static string Sub() => Guid.NewGuid().ToString("D");

    public static string Sid() => ObjectId.GenerateNewId().ToString();

    /// <summary>JWT válido como los que emite Identidad: HS256, 15 minutos, <c>sub</c> y <c>sid</c>.</summary>
    public static string Valid(string? sub = null, string? sid = null) => Create(sub, sid);

    public static string Expired(string? sub = null) => Create(sub, null, notBefore: Now(-20), expires: Now(-1));

    public static string NotYetValid(string? sub = null) => Create(sub, null, notBefore: Now(600), expires: Now(1500));

    public static string WrongIssuer(string? sub = null) => Create(sub, null, issuer: "https://evil.example");

    public static string WrongAudience(string? sub = null) => Create(sub, null, audience: "otra-app");

    public static string SignedWithAnotherKey(string? sub = null) => Create(sub, null, key: _foreignKey);

    /// <summary>Firmado con HS512: el servidor solo acepta HS256.</summary>
    public static string Hs512(string? sub = null) => Create(sub, null, algorithm: SecurityAlgorithms.HmacSha512, key: _doubledKey);

    /// <summary>
    /// Sin <c>exp</c>: un token que nunca vence no debe aceptarse. Se arma a mano (la librería agrega un <c>exp</c> por defecto si el descriptor no trae uno)
    /// y se firma bien con HMAC-SHA256.
    /// </summary>
    public static string WithoutExpiration(string? sub = null)
    {
        var header = Base64UrlEncoder.Encode("""{"alg":"HS256","typ":"JWT"}""");
        var claims = new Dictionary<string, object>
        {
            ["iss"] = Issuer,
            ["aud"] = Audience,
            ["sub"] = sub ?? Sub(),
            ["sid"] = Sid(),
            ["nbf"] = UnixSeconds(Now(-5)),
        };
        var payload = Base64UrlEncoder.Encode(JsonSerializer.SerializeToUtf8Bytes(claims));
        var signature = Base64UrlEncoder.Encode(HMACSHA256.HashData(SigningKey, Encoding.ASCII.GetBytes($"{header}.{payload}")));
        return $"{header}.{payload}.{signature}";
    }

    /// <summary><c>alg: none</c> con la carga de un token válido y la firma vacía.</summary>
    public static string AlgNone(string? sub = null) => Unsigned("none", sub);

    /// <summary><c>alg: none</c> con otra capitalización, que algunas librerías viejas aceptaban.</summary>
    public static string AlgNoneMixedCase(string? sub = null) => Unsigned("NoNe", sub);

    /// <summary>Confusión de algoritmo: el encabezado dice RS256 pero la firma es un HMAC con la clave simétrica.</summary>
    public static string AlgRs256SignedWithHmac(string? sub = null)
    {
        var parts = Valid(sub).Split('.');
        var header = Base64UrlEncoder.Encode("""{"alg":"RS256","typ":"JWT"}""");
        var signature = Base64UrlEncoder.Encode(HMACSHA256.HashData(SigningKey, Encoding.ASCII.GetBytes($"{header}.{parts[1]}")));
        return $"{header}.{parts[1]}.{signature}";
    }

    /// <summary>Cambia el <c>sub</c> de la carga y deja la firma original: la firma ya no corresponde.</summary>
    public static string PayloadTampered(string victimSub)
    {
        var parts = Valid().Split('.');
        using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(parts[1]));
        var claims = payload.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Name == "sub" ? victimSub : (object)p.Value.Clone());
        var forged = Base64UrlEncoder.Encode(JsonSerializer.SerializeToUtf8Bytes(claims));
        return $"{parts[0]}.{forged}.{parts[2]}";
    }

    /// <summary>Cambia los últimos caracteres de la firma.</summary>
    public static string SignatureTampered(string? sub = null)
    {
        var token = Valid(sub);
        var replacement = token.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA";
        return token[..^4] + replacement;
    }

    /// <summary>Quita la firma pero deja el punto final.</summary>
    public static string SignatureStripped(string? sub = null) => string.Join('.', Valid(sub).Split('.')[0..2]) + ".";

    /// <summary>Solo encabezado y carga, sin tercer segmento.</summary>
    public static string TwoSegments(string? sub = null) => string.Join('.', Valid(sub).Split('.')[0..2]);

    /// <summary>Segundos Unix de un instante, para leer <c>exp</c> de un token emitido.</summary>
    public static long UnixSeconds(DateTime value) => new DateTimeOffset(value, TimeSpan.Zero).ToUnixTimeSeconds();

    /// <summary>Valor de un claim del token como texto (sin validar la firma): para comprobar lo que emite el servidor. Vacío si no existe.</summary>
    public static string Claim(string token, string name) =>
        new JsonWebTokenHandler().ReadJsonWebToken(token).Claims.FirstOrDefault(c => c.Type == name)?.Value ?? string.Empty;

    /// <summary>Token firmado con la clave buena pero sin el claim <c>sub</c>.</summary>
    public static string WithoutSub() => WithoutClaim("sub");

    /// <summary>Token firmado con la clave buena pero sin el claim <c>sid</c>.</summary>
    public static string WithoutSid() => WithoutClaim("sid");

    /// <summary>Todos los ataques a la credencial: nombre estable de la prueba y el valor que se manda como token <c>Bearer</c>. Ninguno debe autenticar.</summary>
    public static IReadOnlyDictionary<string, Func<string>> Attacks { get; } = new Dictionary<string, Func<string>>(StringComparer.Ordinal)
    {
        ["expired"] = () => Expired(),
        ["not-yet-valid"] = () => NotYetValid(),
        ["wrong-issuer"] = () => WrongIssuer(),
        ["wrong-audience"] = () => WrongAudience(),
        ["foreign-key"] = () => SignedWithAnotherKey(),
        ["hs512"] = () => Hs512(),
        ["no-expiration"] = () => WithoutExpiration(),
        ["alg-none"] = () => AlgNone(),
        ["alg-none-mixed-case"] = () => AlgNoneMixedCase(),
        ["rs256-with-hmac"] = () => AlgRs256SignedWithHmac(),
        ["payload-tampered"] = () => PayloadTampered(Sub()),
        ["signature-tampered"] = () => SignatureTampered(),
        ["signature-stripped"] = () => SignatureStripped(),
        ["two-segments"] = () => TwoSegments(),
        ["garbage"] = () => "esto-no-es-un-jwt",
        ["three-dots"] = () => "...",
        ["abc"] = () => "a.b.c",
        ["literal-null"] = () => "null",
        ["literal-undefined"] = () => "undefined",
        ["sql-injection"] = () => "' OR '1'='1' --",
        ["huge"] = () => new string('A', 16 * 1024),
    };

    private static string WithoutClaim(string omitted)
    {
        var claims = new Dictionary<string, object> { ["sub"] = Sub(), ["sid"] = Sid() };
        claims.Remove(omitted);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Claims = claims,
            NotBefore = Now(-5),
            Expires = Now(900),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(SigningKey), SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static DateTime Now(int seconds) => DateTime.UtcNow.AddSeconds(seconds);

    private static string Unsigned(string algorithm, string? sub)
    {
        var payload = Valid(sub).Split('.')[1];
        var header = Base64UrlEncoder.Encode($$"""{"alg":"{{algorithm}}","typ":"JWT"}""");
        return $"{header}.{payload}.";
    }

    private static string Create(
        string? sub,
        string? sid,
        string issuer = Issuer,
        string audience = Audience,
        string algorithm = SecurityAlgorithms.HmacSha256,
        byte[]? key = null,
        DateTime? notBefore = null,
        DateTime? expires = null) =>
        new JsonWebTokenHandler().CreateToken(Descriptor(sub, sid, issuer, audience, algorithm, key ?? SigningKey, notBefore ?? Now(-5), expires ?? Now(900)));

    private static SecurityTokenDescriptor Descriptor(
        string? sub, string? sid, string issuer, string audience, string algorithm, byte[] key, DateTime notBefore, DateTime? expires) =>
        new()
        {
            Issuer = issuer,
            Audience = audience,
            Claims = new Dictionary<string, object> { ["sub"] = sub ?? Sub(), ["sid"] = sid ?? Sid() },
            IssuedAt = notBefore,
            NotBefore = notBefore,
            Expires = expires,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), algorithm),
        };
}
