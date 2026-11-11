using Knock.Contracts.Correlation;
using Knock.Messaging.Api.Hosting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// BUG-003: Kestrel agrega «Server: Kestrel» al final de la respuesta, después de cualquier middleware; solo se apaga aquí.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddControllers();
// Más información sobre OpenAPI: https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
// Spec 011: Serilog estructurado y OpenTelemetry con redacción; Azure Monitor solo si hay APPLICATIONINSIGHTS_CONNECTION_STRING.
builder.Services.AddKnockLogging(builder.Configuration);
builder.Services.AddKnockObservability(builder.Configuration, builder.Environment);
builder.Services.AddKnockData();

var app = builder.Build();

app.UseKnockCorrelationId();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseSerilogRequestLogging(LoggingExtensions.ConfigureRequestLogging);

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "mensajeria" }));
app.MapControllers();
app.MapKnockReadiness();

await app.RunAsync();
