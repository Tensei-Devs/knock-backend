using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Knock.Hosting.Security;

/// <summary>
/// Validación única de la credencial de servicio entre servicios (spec 056, R-01 a R-06, R-10; P-01b: catálogo de <c>Knock.Hosting</c>): token HS256 con clave por emisor, <c>iss</c>, <c>aud</c>,
/// <c>jti</c>, <c>iat</c>, <c>exp</c> de 5 minutos como máximo, <c>ClockSkew</c> en cero y el claim <c>permisos</c>. Falla cerrado: sin clave válida ningún token del emisor se valida. La clave anterior
/// (<c>&lt;Clave&gt;:Previous</c>) se acepta solo hasta el instante de <c>&lt;Clave&gt;:PreviousUntil</c> (ventana de 24 horas de la rotación, D-01 y D-02); sin esa fecha no vale.
/// </summary>
public static class ServiceCredential
{
    public const string PermissionsClaim = "permisos";
    public const string ServiceNotAllowedCode = "service_not_allowed";
    public const string PreviousSuffix = ":Previous";
    public const string PreviousUntilSuffix = ":PreviousUntil";

    /// <summary>Un token de servicio dura 5 minutos como máximo (spec 037, R-11).</summary>
    public const int MaxLifetimeSeconds = 300;

    /// <summary>Longitud mínima de la clave HMAC: 32 bytes (256 bits).</summary>
    public const int MinKeyBytes = 32;

    /// <summary>La clave de la configuración si es Base64 de al menos 32 bytes; si no, una clave al azar que nadie conoce, así que ningún token se valida (R-06).</summary>
    public static byte[] ReadKey(string? configured) => TryReadKey(configured) ?? RandomNumberGenerator.GetBytes(MinKeyBytes);

    /// <summary>La clave si es Base64 de al menos 32 bytes; si no, <see langword="null"/>. El mensaje nunca repite el valor.</summary>
    public static byte[]? TryReadKey(string? configured)
    {
        var buffer = new byte[configured?.Length ?? 0];
        var valid = !string.IsNullOrWhiteSpace(configured) && Convert.TryFromBase64String(configured, buffer, out var written) && written >= MinKeyBytes;
        return valid ? Convert.FromBase64String(configured!) : null;
    }

    /// <summary>Los permisos del emisor (un claim por permiso o una lista separada por comas o espacios).</summary>
    public static HashSet<string> PermissionsOf(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.FindAll(PermissionsClaim)
            .SelectMany(claim => claim.Value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>El token trae <c>jti</c> e <c>iat</c> y vive 5 minutos o menos (<c>exp</c> menos <c>iat</c>).</summary>
    public static bool HasBoundedLifetime(ClaimsPrincipal? principal) =>
        HasBoundedLifetime(principal?.FindFirst("jti")?.Value, principal?.FindFirst("iat")?.Value, principal?.FindFirst("exp")?.Value);

    /// <summary>Lo mismo con los valores crudos de los claims (para quien valida con <c>JsonWebTokenHandler</c>).</summary>
    public static bool HasBoundedLifetime(string? jti, string? issuedAt, string? expires) =>
        !string.IsNullOrEmpty(jti)
        && long.TryParse(issuedAt, NumberStyles.None, CultureInfo.InvariantCulture, out var iat)
        && long.TryParse(expires, NumberStyles.None, CultureInfo.InvariantCulture, out var exp)
        && exp - iat is > 0 and <= MaxLifetimeSeconds;

    /// <summary>Los parámetros de validación de la regla común para los emisores y el destinatario de <paramref name="settings"/>.</summary>
    public static TokenValidationParameters CreateValidationParameters(ServiceCredentialSettings settings, IConfiguration configuration, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(configuration);
        var clock = time ?? TimeProvider.System;
        var keys = settings.IssuerKeyConfigurationKeys.ToDictionary(pair => pair.Key, pair => new IssuerKeys(pair.Value, configuration), StringComparer.Ordinal);
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = [.. keys.Keys],
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = (_, token, _, _) => keys.TryGetValue(token.Issuer ?? string.Empty, out var issuer) ? issuer.Active(clock.GetUtcNow()) : [],
            RequireSignedTokens = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero,
        };
    }

    /// <summary>Claves de un emisor: la vigente (o una al azar si no es válida) y, si hay ventana de rotación abierta, la anterior.</summary>
    private sealed class IssuerKeys(string configurationKey, IConfiguration configuration)
    {
        private readonly SecurityKey _current = new SymmetricSecurityKey(ReadKey(configuration[configurationKey]));
        private readonly SecurityKey? _previous = TryReadKey(configuration[configurationKey + PreviousSuffix]) is { } key ? new SymmetricSecurityKey(key) : null;
        private readonly DateTimeOffset? _previousUntil = DateTimeOffset.TryParse(
            configuration[configurationKey + PreviousUntilSuffix], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var until) ? until : null;

        public IEnumerable<SecurityKey> Active(DateTimeOffset now) =>
            _previous is not null && _previousUntil is { } limit && now < limit ? [_current, _previous] : [_current];
    }
}
