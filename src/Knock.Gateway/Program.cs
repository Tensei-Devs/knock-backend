using Knock.Contracts.Correlation;
using Knock.Gateway.Hosting;

var builder = WebApplication.CreateBuilder(args);

// La tabla de rutas y las direcciones de los servicios viven en la configuración (sección ReverseProxy),
// para cambiarlas por entorno sin recompilar (spec 001, R-02).
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddKnockData();

var app = builder.Build();

app.UseKnockCorrelationId();
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
