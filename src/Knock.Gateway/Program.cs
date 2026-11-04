using Knock.Contracts.Correlation;
using Knock.Gateway.Hosting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// La tabla de rutas y las direcciones de los servicios viven en la configuración (sección ReverseProxy),
// para cambiarlas por entorno sin recompilar (spec 001, R-02). Cada ruta declara su política: `default` exige sesión
// y `anonymous` deja pasar (specs 009 y 015).
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services
    .AddKnockGatewayProblemDetails()
    .AddKnockGatewayForwardedHeaders()
    .AddKnockGatewayCors(builder.Configuration)
    .AddKnockData()
    .AddKnockGatewayRateLimiting()
    .AddKnockGatewayAuthentication();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseKnockCorrelationId();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseRouting();
app.UseCors(CorsExtensions.PolicyName);
app.UseMiddleware<IpRateLimitMiddleware>();
app.UseAuthentication();
app.UseMiddleware<UserRateLimitMiddleware>();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "gateway" })).AllowAnonymous();
app.MapHealthChecks(HealthExtensions.ReadyPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains(HealthExtensions.ReadyTag) }).AllowAnonymous();
app.MapReverseProxy();

await app.RunAsync();

/// <summary>Punto de entrada accesible para las pruebas de integración.</summary>
internal partial class Program
{
    protected Program()
    {
    }
}
