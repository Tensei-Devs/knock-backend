using Knock.Hosting.Lifecycle;
using Knock.Market.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);
// Spec 034 R-03: apagado ordenado (readiness en 503 y drenado de 25 s).
builder.Services.AddKnockGracefulShutdown();
builder.AddKnockApi();

var app = builder.Build();
app.UseKnockApi();
await app.RunAsync();
