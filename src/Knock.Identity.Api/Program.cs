using Knock.Contracts.Correlation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
// Más información sobre OpenAPI: https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseKnockCorrelationId();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "identidad" }));
app.MapControllers();

await app.RunAsync();
