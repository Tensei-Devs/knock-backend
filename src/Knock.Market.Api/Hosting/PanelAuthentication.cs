using Knock.Hosting.Security;
using Knock.Market.Api.Common.Errors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Knock.Market.Api.Hosting;

/// <summary>Resultado de preguntar si una sesión del panel puede usar un permiso.</summary>
internal enum PanelDecision
{
    Allowed,
    Denied,
}

/// <summary>
/// Frontera hacia las cuentas del panel (spec 033, R-21): decide con los roles vigentes en el servidor, nunca con los claims del token (R-13: no lleva permisos). Las cuentas viven en Identidad,
/// así que la implementación real llamará a Identidad con la autenticación entre servicios (spec 056). Mientras no exista, <see cref="DenyAllPanelPermissionGate"/> niega cualquier permiso.
/// </summary>
internal interface IPanelPermissionGate
{
    /// <summary>Decide si la cuenta <paramref name="subject"/> (sesión <paramref name="sessionId"/>, versión de permisos <paramref name="permissionsVersion"/>) tiene <paramref name="permission"/>.</summary>
    Task<PanelDecision> DecideAsync(string subject, string sessionId, string permissionsVersion, string permission, CancellationToken cancellationToken);
}

/// <summary>Implementación por omisión: falla cerrado. Ninguna ruta de administración de Mercado funciona hasta registrar la implementación real.</summary>
internal sealed class DenyAllPanelPermissionGate : IPanelPermissionGate
{
    public Task<PanelDecision> DecideAsync(string subject, string sessionId, string permissionsVersion, string permission, CancellationToken cancellationToken) =>
        Task.FromResult(PanelDecision.Denied);
}

/// <summary>Requisito de las políticas del panel en Mercado: el permiso (spec 033, R-02) que la ruta declara.</summary>
internal sealed record PanelPermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>
/// Esquema <c>PanelBearer</c> de Mercado (PROVISIONAL, spec 045, R-20): valida el token del panel (llave <c>PanelJwt:SigningKey</c> en Base64 de al menos 32 bytes, destinatario <c>knock-panel</c>, mismo
/// emisor que la app, HMAC-SHA256, sin tolerancia de reloj) y autoriza por permiso con <see cref="IPanelPermissionGate"/>. Sin llave válida ningún token se acepta. El token de la app no sirve aquí
/// ni el del panel en las rutas de la app (otra llave y otro destinatario).
/// </summary>
internal static class PanelAuthentication
{
    public const string Scheme = "PanelBearer";
    public const string KeyConfigurationKey = "PanelJwt:SigningKey";
    public const string AudienceConfigurationKey = "PanelJwt:Audience";
    public const string DefaultAudience = "knock-panel";
    public const string CatalogManagePermission = "catalog:manage";
    public const string MetricsReadPermission = "metrics:read";
    public const string ReviewsModeratePermission = "reviews:moderate";
    public const string ReviewsModeratePolicy = "panel-reviews-moderate";
    public const string CatalogManagePolicy = "panel-catalog-manage";
    public const string MetricsReadPolicy = "panel-metrics-read";
    public const string InsufficientPermissionCode = "insufficient_permission";

    public static AuthenticationBuilder AddPanelScheme(this AuthenticationBuilder builder) => builder.AddJwtBearer(Scheme, _ => { });

    public static IServiceCollection AddKnockPanelAuthentication(this IServiceCollection services)
    {
        services.AddOptions<JwtBearerOptions>(Scheme).Configure<IConfiguration, JwtSettings>((options, configuration, app) =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = app.Issuer,
                ValidateAudience = true,
                ValidAudience = configuration[AudienceConfigurationKey] is { Length: > 0 } audience ? audience : DefaultAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(InternalServiceAuthentication.ReadKey(configuration[KeyConfigurationKey])),
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
                    var principal = context.Principal;
                    if (principal?.FindFirst("sub") is not { Value.Length: > 0 } || principal.FindFirst("sid") is not { Value.Length: > 0 } || principal.FindFirst("pv") is null)
                    {
                        context.Fail("invalid");
                    }

                    return Task.CompletedTask;
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    await ProblemResponses.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "Unauthorized", ErrorCodes.UnauthorizedCode);
                },
                OnForbidden = context => ProblemResponses.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden, "Forbidden", InsufficientPermissionCode),
            };
        });
        services.AddSingleton<IAuthorizationHandler, PanelPermissionHandler>();
        services.AddAuthorizationBuilder()
            .AddPolicy(CatalogManagePolicy, policy => policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser().AddRequirements(new PanelPermissionRequirement(CatalogManagePermission)))
            .AddPolicy(ReviewsModeratePolicy, policy => policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser().AddRequirements(new PanelPermissionRequirement(ReviewsModeratePermission)))
            .AddPolicy(MetricsReadPolicy, policy => policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser().AddRequirements(new PanelPermissionRequirement(MetricsReadPermission)));
        return services;
    }
}

/// <summary>Autoriza el permiso de la ruta preguntando a <see cref="IPanelPermissionGate"/> con la cuenta, la sesión y la versión de permisos del token.</summary>
internal sealed class PanelPermissionHandler(IPanelPermissionGate gate, IHttpContextAccessor accessor) : AuthorizationHandler<PanelPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PanelPermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);
        var sub = context.User.FindFirst("sub")?.Value;
        var sid = context.User.FindFirst("sid")?.Value;
        var pv = context.User.FindFirst("pv")?.Value;
        if (string.IsNullOrEmpty(sub) || string.IsNullOrEmpty(sid) || pv is null)
        {
            return;
        }

        var cancellation = accessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        if (await gate.DecideAsync(sub, sid, pv, requirement.Permission, cancellation) == PanelDecision.Allowed)
        {
            context.Succeed(requirement);
        }
    }
}
