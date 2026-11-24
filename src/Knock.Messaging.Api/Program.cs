using Knock.Contracts.Correlation;
using Knock.Hosting.ForwardedHeaders;
using Knock.Hosting.Lifecycle;
using Knock.Hosting.Logging;
using Knock.Hosting.Observability;
using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Avisos;
using Knock.Messaging.Api.Modules.Bloqueos;
using Knock.Messaging.Api.Modules.Chat;
using Knock.Messaging.Api.Modules.Conversaciones;
using Knock.Messaging.Api.Modules.Notices;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
// Spec 034 R-03: apagado ordenado (readiness en 503 y drenado de 25 s).
builder.Services.AddKnockGracefulShutdown();

// BUG-003: Kestrel agrega «Server: Kestrel» al final de la respuesta, después de cualquier middleware; solo se apaga aquí.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>()).AddInternalControllers();
// BUG-004: JWT, encabezados de seguridad y límite por IP como en Identidad y Mercado, antes del primer endpoint.
builder.Services
    .AddKnockProblemDetails()
    .AddKnockValidation()
    .AddKnockApiVersioning()
    .AddKnockRateLimiting()
    .AddKnockAuthentication();
// Más información sobre OpenAPI: https://aka.ms/aspnet/openapi
builder.Services.AddKnockOpenApi();
// Spec 011: Serilog estructurado y OpenTelemetry con redacción; Azure Monitor solo si hay APPLICATIONINSIGHTS_CONNECTION_STRING.
builder.Services.AddKnockLogging(builder.Configuration, typeof(Program).Assembly);
builder.Services.AddKnockObservability(
    builder.Configuration, builder.Environment, typeof(Program).Assembly, "knock-api", Knock.Messaging.Api.Modules.Chat.Services.ChatMetrics.MeterName);
builder.Services.AddKnockForwardedHeaders();
builder.Services.AddKnockData();
// Spec 026: chat de visita en tiempo real con SignalR y Redis como puente entre copias.
builder.Services.AddChatModule().AddKnockChatBridge();
// Spec 030: errores de dominio de Conversaciones como ProblemDetails y cliente del permiso de Mercado con caché de 60 s (usa Services:Market que valida AddChatModule).
builder.Services.AddConversacionesModule();
// Spec 037: dispositivos y despachador; FCM detrás de IPushSender con Notificaciones:PushHabilitado apagado por omisión (R-10).
builder.Services.AddAvisosModule();
// Spec 044: avisos dentro de la app (bandeja, contador y preferencias); solo lee, los avisos los crea el despachador de la spec 037.
builder.Services.AddNoticesModule();
// Spec 043 (R-23): proyección mínima de bloqueos alimentada por los eventos de Identidad.
builder.Services.AddBloqueosModule();

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

app.UseKnockChatGate();
// Como en Mercado: la sesión se resuelve antes del limitador para que las políticas por persona (sub) vean al usuario; el límite por IP sigue valiendo para todas las rutas.
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "mensajeria" })).AllowAnonymous();
app.MapControllers();
app.MapChatHub();
app.MapKnockReadiness();

await app.RunAsync();
