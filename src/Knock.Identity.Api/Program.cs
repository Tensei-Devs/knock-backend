using Knock.Contracts.Correlation;
using Knock.Identity.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
// Más información sobre OpenAPI: https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddKnockData();

var app = builder.Build();

app.UseKnockCorrelationId();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "identidad" }));
app.MapControllers();
app.MapKnockReadiness();

await app.RunAsync();
