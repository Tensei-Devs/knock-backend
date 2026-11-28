using Knock.Hosting.Lifecycle;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.Panel.Cli;

// Modo de herramienta (spec 033, R-25): `Knock.Identity.Api panel <comando>` no levanta el servidor web.
if (PanelCliHost.IsToolMode(args))
{
    return await PanelCliHost.RunAsync(args[1..]);
}

var builder = WebApplication.CreateBuilder(args);
// Spec 034 R-03: apagado ordenado (readiness en 503 y drenado de 25 s).
builder.Services.AddKnockGracefulShutdown();
builder.AddKnockApi();

var app = builder.Build();
app.UseKnockApi();
await app.RunAsync();
return 0;
