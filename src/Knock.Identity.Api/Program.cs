using Knock.Contracts.Correlation;
using Knock.Identity.Api.Hosting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

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

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "identidad" }));
app.MapControllers();
app.MapKnockReadiness();

await app.RunAsync();
