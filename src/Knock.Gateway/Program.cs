using Knock.Contracts.Correlation;
using Knock.Gateway.Hosting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// La tabla de rutas y las direcciones de los servicios viven en la configuración (sección ReverseProxy),
// para cambiarlas por entorno sin recompilar (spec 001, R-02).
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddKnockData();
// Spec 011: Serilog estructurado y OpenTelemetry con redacción; Azure Monitor solo si hay APPLICATIONINSIGHTS_CONNECTION_STRING.
builder.Services.AddKnockLogging(builder.Configuration);
builder.Services.AddKnockObservability(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseKnockCorrelationId();
app.UseSerilogRequestLogging(LoggingExtensions.ConfigureRequestLogging);
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "gateway" }));
app.MapKnockReadiness();
app.MapReverseProxy();

await app.RunAsync();

/// <summary>Punto de entrada accesible para las pruebas de integración.</summary>
internal partial class Program
{
    protected Program()
    {
    }
}
