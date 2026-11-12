using Knock.Contracts.Correlation;
using Knock.Messaging.Api.Hosting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// BUG-003: Kestrel agrega «Server: Kestrel» al final de la respuesta, después de cualquier middleware; solo se apaga aquí.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddControllers();
// BUG-004: JWT, encabezados de seguridad y límite por IP como en Identidad y Mercado, antes del primer endpoint.
builder.Services
    .AddKnockProblemDetails()
    .AddKnockRateLimiting()
    .AddKnockAuthentication();
// Más información sobre OpenAPI: https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
// Spec 011: Serilog estructurado y OpenTelemetry con redacción; Azure Monitor solo si hay APPLICATIONINSIGHTS_CONNECTION_STRING.
builder.Services.AddKnockLogging(builder.Configuration);
builder.Services.AddKnockObservability(builder.Configuration, builder.Environment);
builder.Services.AddKnockForwardedHeaders();
builder.Services.AddKnockData();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseKnockCorrelationId();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseExceptionHandler();
// Una respuesta de error sin cuerpo (404, 405…) sale como ProblemDetails, igual que en los otros servicios.
app.UseStatusCodePages();
app.UseSerilogRequestLogging(LoggingExtensions.ConfigureRequestLogging);
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "mensajeria" })).AllowAnonymous();
app.MapControllers();
app.MapKnockReadiness();

await app.RunAsync();
