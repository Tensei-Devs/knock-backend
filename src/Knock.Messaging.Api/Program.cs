using Knock.Messaging.Api.Hosting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
// Spec 011: Serilog estructurado y OpenTelemetry con redacción; Azure Monitor solo si hay APPLICATIONINSIGHTS_CONNECTION_STRING.
builder.Services.AddKnockLogging(builder.Configuration);
builder.Services.AddKnockObservability(builder.Configuration, builder.Environment);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseSerilogRequestLogging(LoggingExtensions.ConfigureRequestLogging);
// Pendiente (spec 011, R-02): X-Correlation-Id llega con Knock.Contracts en el PR #3; no se copia CorrelationIdMiddleware aquí.

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
