using Knock.Hosting.Http;
using Knock.Hosting.Security;
using Knock.Market.Api.Common.Errors;
using Microsoft.AspNetCore.Authentication;

namespace Knock.Market.Api.Hosting;

/// <summary>
/// Autenticación de servicio a servicio de las rutas internas de Mercado (spec 041, R-17, con el esquema de la spec 037, R-11): token HS256 firmado con una clave por emisor
/// (<c>Security:InternalTokens:IdentityKey</c>, el secreto <c>interno-key-identity</c>), <c>iss</c> <c>knock-identity</c>, <c>aud</c> <c>knock-market</c>, <c>exp</c> de 5 minutos como máximo,
/// <c>jti</c> y el claim <c>permisos</c>. No reutiliza <c>jwt-signing-key</c>, la clave de los tokens de usuario. Sin clave válida ninguna ruta interna acepta nada (falla cerrado).
/// </summary>
internal static class InternalServiceAuthentication
{
    public const string Scheme = "InternalService";
    public const string HistoryPolicy = "InternalHistory";
    public const string EventsPolicy = "InternalEvents";
    public const string AccountPolicy = "InternalAccount";
    public const string AccountPermission = "cuenta";
    public const string KeyConfigurationKey = "Security:InternalTokens:IdentityKey";
    public const string Issuer = "knock-identity";
    public const string Audience = "knock-market";
    public const string PermissionsClaim = ServiceCredential.PermissionsClaim;
    public const string HistoryPermission = "historial";
    public const string BlocksPolicy = "InternalBlocks";
    public const string BlocksPermission = "bloqueos";
    public const string EventsPermission = "eventos";
    public const string TechnicianHiddenPolicy = "InternalTechnicianHidden";
    public const string TechnicianHiddenPermission = "tecnico-ocultado";
    public const string NotFinishedReportPolicy = "InternalNotFinishedReport";
    public const string NotFinishedReportPermission = "reporte-no-terminado";
    public const string ServiceNotAllowedCode = ServiceCredential.ServiceNotAllowedCode;

    /// <summary>Un token de servicio dura 5 minutos como máximo (spec 037, R-11).</summary>
    public const int MaxLifetimeSeconds = ServiceCredential.MaxLifetimeSeconds;

    /// <summary>Longitud mínima de la clave HMAC: 32 bytes (256 bits).</summary>
    public const int MinKeyBytes = ServiceCredential.MinKeyBytes;

    /// <summary>La validación de la credencial es la única de <c>Knock.Hosting</c> (spec 056, T-02): aquí solo se dice el destinatario, el emisor y su clave.</summary>
    public static AuthenticationBuilder AddInternalServiceScheme(this AuthenticationBuilder builder) =>
        builder.AddKnockServiceCredential(new ServiceCredentialSettings
        {
            Scheme = Scheme,
            Audience = Audience,
            IssuerKeyConfigurationKeys = new Dictionary<string, string> { [Issuer] = KeyConfigurationKey },
            WriteProblem = (context, status, title, code) => ProblemResponses.WriteAsync(context, status, title, code),
            UnauthorizedCode = ErrorCodes.UnauthorizedCode,
        });

    public static IServiceCollection AddKnockInternalServiceAuthentication(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder().AddPolicy(
            HistoryPolicy,
            policy => policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser().RequireClaim(PermissionsClaim, HistoryPermission))
            .AddPolicy(BlocksPolicy, policy => policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser().RequireClaim(PermissionsClaim, BlocksPermission))
            .AddPolicy(
                EventsPolicy,
                policy => policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser().RequireClaim(PermissionsClaim, EventsPermission))
            .AddPolicy(AccountPolicy, policy => policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser().RequireClaim(PermissionsClaim, AccountPermission))
            .AddPolicy(TechnicianHiddenPolicy, policy => policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser().RequireClaim(PermissionsClaim, TechnicianHiddenPermission))
            .AddPolicy(NotFinishedReportPolicy, policy => policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser().RequireClaim(PermissionsClaim, NotFinishedReportPermission));
        return services;
    }

    /// <summary>La clave de la configuración si es Base64 de al menos 32 bytes; si no, una clave al azar que nadie conoce, así que ningún token se valida.</summary>
    internal static byte[] ReadKey(string? configured) => ServiceCredential.ReadKey(configured);
}
