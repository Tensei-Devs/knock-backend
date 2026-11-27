using Knock.Hosting.Data;
using Knock.Hosting.ForwardedHeaders;
using Knock.Hosting.Health;
using Knock.Hosting.Http;
using Knock.Hosting.Logging;
using Knock.Hosting.Observability;
using Knock.Hosting.Procesos;
using Knock.Hosting.Security;
using Knock.Market.Api.Common.Errors;
using Microsoft.Extensions.Options;
using Serilog;

namespace Knock.Market.Api.Hosting;

internal static class KnockApplicationExtensions
{
    public static WebApplicationBuilder AddKnockApi(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddKnockLogging(builder.Configuration, typeof(KnockApplicationExtensions).Assembly)
            .AddControllers(options => options.Filters.Add<ValidationFilter>())
            .AddInternalControllers();
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, MarketJsonSerializerContext.Default));
        builder.AddKnockSecurity();
        builder.Services
            .AddKnockProblemDetails()
            .AddExceptionHandler<DomainExceptionHandler>()
            .AddKnockValidation()
            .AddKnockForwardedHeaders()
            .AddKnockRateLimiting()
            .AddKnockApiVersioning()
            .AddKnockAuthentication()
            .AddKnockOpenApi()
            .AddKnockHealth()
            .AddKnockMongo()
            .AddKnockModules(builder.Configuration, builder.Environment.IsDevelopment())
            .AddKnockObservability(builder.Configuration, builder.Environment, typeof(KnockApplicationExtensions).Assembly, "knock-market", ProcesosMetrics.MeterName);
        return builder;
    }

    public static WebApplication UseKnockApi(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseKnockSecurityHeaders(new SecurityHeadersOptions { RelaxContentSecurityPolicyForApiDocsInDevelopment = true });
        app.UseExceptionHandler();
        // Una respuesta de error sin cuerpo (404 de una ruta inexistente, 405…) sale como ProblemDetails igual que las excepciones (spec 002, R-03).
        app.UseStatusCodePages();
        app.UseSerilogRequestLogging(LoggingExtensions.ConfigureRequestLogging);
        var securedTransport = !app.Environment.IsDevelopment();
        if (securedTransport)
        {
            app.UseHsts();
        }

        if (securedTransport && app.Services.GetRequiredService<IOptions<SecuritySettings>>().Value.Https.RedirectPort is not null)
        {
            app.UseHttpsRedirection();
        }

        app.UseKnockCors(CorsOptions(app.Services.GetRequiredService<IOptions<SecuritySettings>>().Value.Cors));
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.Equals("/api/v1/zonas/lista-espera", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Headers.CacheControl = "private, no-store";
            }

            await next(context);
        });
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseMiddleware<RequestBodyLimitMiddleware>(new RequestBodyLimitOptions { PayloadTooLargeCode = ErrorCodes.PayloadTooLargeCode });
        app.UseKnockJsonCompression();
        app.UseAuthorization();
        app.MapKnockHealth();
        app.MapKnockOpenApi(app.Environment);
        app.MapControllers();
        return app;
    }

    /// <summary>Los orígenes y la vigencia del preflight salen de <c>Security:Cors</c>; los encabezados de petición que concede este servicio son los de su contrato.</summary>
    private static KnockCorsOptions CorsOptions(CorsSettings cors) =>
        new(cors.AllowedOrigins, cors.PreflightMaxAge, ["Authorization", "Content-Type", "X-Correlation-Id", "Idempotency-Key", "If-Match", "If-None-Match", "Cache-Control"]);
}
