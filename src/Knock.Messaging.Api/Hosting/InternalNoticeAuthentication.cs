using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using Knock.Messaging.Api.Common.Errors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Knock.Messaging.Api.Hosting;

/// <summary>
/// Autenticación de servicio a servicio de <c>POST /api/v1/interno/avisos</c> (spec 037, R-11): token HS256 firmado con la clave HMAC propia de cada emisor
/// (<c>Security:InternalTokens:MarketKey</c> e <c>IdentityKey</c>, los secretos <c>avisos-key-market</c> y <c>avisos-key-identity</c>), <c>iss</c> del emisor (<c>knock-market</c> o
/// <c>knock-identity</c>), <c>aud</c> <c>knock-notifications</c>, <c>exp</c> de 5 minutos como máximo, <c>jti</c> y el claim <c>permisos</c>. La clave se elige por <c>iss</c>, así que un emisor
/// no puede firmar como el otro. No reutiliza <c>jwt-signing-key</c>. Sin clave válida de un emisor, ninguno de sus tokens se valida (falla cerrado).
/// </summary>
internal static class InternalNoticeAuthentication
{
    public const string Scheme = "InternalNotices";
    public const string Policy = "InternalNotices";
    public const string Audience = "knock-notifications";
    public const string PermissionsClaim = "permisos";
    public const string ServiceNotAllowedCode = "service_not_allowed";
    public const string MarketIssuer = "knock-market";
    public const string IdentityIssuer = "knock-identity";
    public const string MarketKeyConfigurationKey = "Security:InternalTokens:MarketKey";
    public const string IdentityKeyConfigurationKey = "Security:InternalTokens:IdentityKey";

    /// <summary>Un token de servicio dura 5 minutos como máximo (spec 037, R-11).</summary>
    public const int MaxLifetimeSeconds = 300;

    /// <summary>Longitud mínima de la clave HMAC: 32 bytes (256 bits).</summary>
    public const int MinKeyBytes = 32;

    public static AuthenticationBuilder AddInternalNoticeScheme(this AuthenticationBuilder builder) =>
        builder.AddJwtBearer(Scheme, _ => { });

    public static IServiceCollection AddKnockInternalNoticeAuthentication(this IServiceCollection services)
    {
        services.AddOptions<JwtBearerOptions>(Scheme).Configure<IConfiguration>((options, configuration) =>
        {
            var keys = new Dictionary<string, SecurityKey>(StringComparer.Ordinal)
            {
                [MarketIssuer] = new SymmetricSecurityKey(ReadKey(configuration[MarketKeyConfigurationKey])),
                [IdentityIssuer] = new SymmetricSecurityKey(ReadKey(configuration[IdentityKeyConfigurationKey])),
            };
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuers = [MarketIssuer, IdentityIssuer],
                ValidateAudience = true,
                ValidAudience = Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeyResolver = (_, token, _, _) => keys.TryGetValue(token.Issuer ?? string.Empty, out var key) ? [key] : [],
                RequireSignedTokens = true,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.Zero,
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    if (!HasBoundedLifetime(context.Principal))
                    {
                        context.Fail("The service token lifetime is not valid.");
                    }

                    return Task.CompletedTask;
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    await ProblemResponses.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "Unauthorized", ErrorCodes.UnauthorizedCode);
                },
                OnForbidden = context => ProblemResponses.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden, "Forbidden", ServiceNotAllowedCode),
            };
        });
        services.AddAuthorizationBuilder().AddPolicy(
            Policy,
            policy => policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser().RequireClaim(PermissionsClaim));
        return services;
    }

    /// <summary>Los tipos que el emisor puede enviar según el claim <c>permisos</c> (un claim por tipo o una lista separada por comas o espacios).</summary>
    internal static HashSet<string> PermissionsOf(ClaimsPrincipal principal) =>
        principal.FindAll(PermissionsClaim)
            .SelectMany(claim => claim.Value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>La clave de la configuración si es Base64 de al menos 32 bytes; si no, una clave al azar que nadie conoce, así que ningún token de ese emisor se valida.</summary>
    internal static byte[] ReadKey(string? configured)
    {
        var buffer = new byte[configured?.Length ?? 0];
        var valid = !string.IsNullOrWhiteSpace(configured) && Convert.TryFromBase64String(configured, buffer, out var written) && written >= MinKeyBytes;
        return valid ? Convert.FromBase64String(configured!) : RandomNumberGenerator.GetBytes(MinKeyBytes);
    }

    /// <summary>El token trae <c>jti</c> e <c>iat</c> y vive 5 minutos o menos (<c>exp</c> menos <c>iat</c>).</summary>
    private static bool HasBoundedLifetime(ClaimsPrincipal? principal)
    {
        var lifetime = TimestampOf(principal, "exp") - TimestampOf(principal, "iat");
        return !string.IsNullOrEmpty(principal?.FindFirst("jti")?.Value) && lifetime is > 0 and <= MaxLifetimeSeconds;
    }

    /// <summary>Un claim numérico (segundos Unix) o un valor muy negativo que hace fallar la vida útil si falta o no es número.</summary>
    private static long TimestampOf(ClaimsPrincipal? principal, string claim) =>
        long.TryParse(principal?.FindFirst(claim)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : long.MinValue / 4;
}
