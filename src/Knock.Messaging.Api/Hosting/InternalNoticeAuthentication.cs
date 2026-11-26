using System.Security.Claims;
using Knock.Hosting.Http;
using Knock.Hosting.Security;
using Knock.Messaging.Api.Common.Errors;
using Microsoft.AspNetCore.Authentication;

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
    public const string PermissionsClaim = ServiceCredential.PermissionsClaim;
    public const string ServiceNotAllowedCode = ServiceCredential.ServiceNotAllowedCode;
    public const string MarketIssuer = "knock-market";
    public const string IdentityIssuer = "knock-identity";
    public const string MarketKeyConfigurationKey = "Security:InternalTokens:MarketKey";
    public const string IdentityKeyConfigurationKey = "Security:InternalTokens:IdentityKey";

    /// <summary>Un token de servicio dura 5 minutos como máximo (spec 037, R-11).</summary>
    public const int MaxLifetimeSeconds = ServiceCredential.MaxLifetimeSeconds;

    /// <summary>Longitud mínima de la clave HMAC: 32 bytes (256 bits).</summary>
    public const int MinKeyBytes = ServiceCredential.MinKeyBytes;

    /// <summary>La validación de la credencial es la única de <c>Knock.Hosting</c> (spec 056, T-02): aquí solo se dicen el destinatario, los emisores y sus claves.</summary>
    public static AuthenticationBuilder AddInternalNoticeScheme(this AuthenticationBuilder builder) =>
        builder.AddKnockServiceCredential(new ServiceCredentialSettings
        {
            Scheme = Scheme,
            Audience = Audience,
            IssuerKeyConfigurationKeys = new Dictionary<string, string>
            {
                [MarketIssuer] = MarketKeyConfigurationKey,
                [IdentityIssuer] = IdentityKeyConfigurationKey,
            },
            WriteProblem = (context, status, title, code) => ProblemResponses.WriteAsync(context, status, title, code),
            UnauthorizedCode = ErrorCodes.UnauthorizedCode,
        });

    public static IServiceCollection AddKnockInternalNoticeAuthentication(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder().AddPolicy(
            Policy,
            policy => policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser().RequireClaim(PermissionsClaim));
        return services;
    }

    /// <summary>Los tipos que el emisor puede enviar según el claim <c>permisos</c> (un claim por tipo o una lista separada por comas o espacios).</summary>
    internal static HashSet<string> PermissionsOf(ClaimsPrincipal principal) => ServiceCredential.PermissionsOf(principal);

    /// <summary>La clave de la configuración si es Base64 de al menos 32 bytes; si no, una clave al azar que nadie conoce, así que ningún token de ese emisor se valida.</summary>
    internal static byte[] ReadKey(string? configured) => ServiceCredential.ReadKey(configured);
}
