using Knock.Hosting.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace Knock.Gateway.Hosting;

/// <summary>
/// Autenticación del gateway (spec 009): un solo algoritmo (HMAC-SHA256), firmante, destinatario y vencimiento sin tolerancia, y rechazo de los JWT
/// cuyo <c>sid</c> esté cancelado en Redis (R-08). Las rutas se declaran en la configuración de YARP: <c>AuthorizationPolicy: default</c> exige
/// sesión y <c>anonymous</c> deja pasar; lo que no declara nada cae en la política de respaldo, que exige sesión (denegar por defecto).
/// </summary>
internal static class AuthenticationExtensions
{
    /// <summary>Clave de <c>HttpContext.Items</c> que marca que Redis no respondió al revisar la sesión: se responde 503 y no 401.</summary>
    public const string RedisUnavailableItem = "Knock.Gateway.RedisUnavailable";

    public static IServiceCollection AddKnockGatewayAuthentication(this IServiceCollection services)
    {
        services.AddSingleton(provider => JwtSettingsFactory.CreateRequiringKey(provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton<IRevokedSessions, RedisRevokedSessions>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtSettings>((options, settings) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = settings.ToValidationParameters();
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = RejectCancelledSessionAsync,
                    OnChallenge = ChallengeAsync,
                };
            });
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.DefaultPolicy = options.FallbackPolicy;
        });
        return services;
    }

    /// <summary>Mismo 401 para cualquier fallo (503 si Redis no respondió al revisar la sesión), sin motivo en el cuerpo.</summary>
    internal static async Task ChallengeAsync(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        var unavailable = context.HttpContext.Items.ContainsKey(RedisUnavailableItem);
        await ProblemResponses.WriteAsync(
            context.HttpContext,
            unavailable ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status401Unauthorized,
            unavailable ? "Service Unavailable" : "Unauthorized");
    }

    internal static async Task RejectCancelledSessionAsync(TokenValidatedContext context)
    {
        var sessionId = context.Principal?.FindFirst("sid")?.Value;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            // Un JWT de Knock siempre lleva sid: el que no lo trae no salió de Identidad.
            context.Fail("The token has no session.");
            return;
        }

        try
        {
            var revoked = context.HttpContext.RequestServices.GetRequiredService<IRevokedSessions>();
            if (await revoked.IsRevokedAsync(sessionId, context.HttpContext.RequestAborted))
            {
                context.Fail("The session was cancelled.");
            }
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            // Sin Redis no se puede saber si la sesión sigue viva: se cierra el paso (503) en lugar de dejarlo abierto.
            context.HttpContext.Items[RedisUnavailableItem] = true;
            context.Fail("The session list is unavailable.");
        }
    }
}
