using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Knock.Hosting.Security;

/// <summary>Registro del esquema de la credencial de servicio con la validación única (spec 056). Las políticas por permiso las define cada servicio.</summary>
public static class ServiceCredentialExtensions
{
    public static AuthenticationBuilder AddKnockServiceCredential(this AuthenticationBuilder builder, ServiceCredentialSettings settings)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(settings);
        builder.AddJwtBearer(settings.Scheme, _ => { });
        builder.Services.AddOptions<JwtBearerOptions>(settings.Scheme).Configure<IConfiguration, IServiceProvider>((options, configuration, provider) =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = ServiceCredential.CreateValidationParameters(settings, configuration, provider.GetService<TimeProvider>());
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = RejectUnboundedLifetime,
                OnChallenge = context => ChallengeAsync(context, settings),
                OnForbidden = context => settings.WriteProblem(context.HttpContext, StatusCodes.Status403Forbidden, "Forbidden", settings.ForbiddenCode),
            };
        });
        return builder;
    }

    private static Task RejectUnboundedLifetime(TokenValidatedContext context)
    {
        if (!ServiceCredential.HasBoundedLifetime(context.Principal))
        {
            context.Fail("The service token lifetime is not valid.");
        }

        return Task.CompletedTask;
    }

    private static Task ChallengeAsync(JwtBearerChallengeContext context, ServiceCredentialSettings settings)
    {
        context.HandleResponse();
        return settings.WriteProblem(context.HttpContext, StatusCodes.Status401Unauthorized, "Unauthorized", settings.UnauthorizedCode);
    }
}
