namespace Knock.Identity.Api.Modules.Trust.Services;

/// <summary>Arranque de la verificación de demo: corre después de las cuentas demo (el módulo Usuarios se registra antes) y solo con <c>Seed:Demo</c> fuera de Producción.</summary>
internal sealed class DemoVerificationStartup(IServiceProvider services, IConfiguration configuration, IHostEnvironment environment) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (DemoVerificationSeeder.ShouldRun(configuration, environment))
        {
            await services.GetRequiredService<DemoVerificationSeeder>()
                .SeedAsync(Path.Combine(AppContext.BaseDirectory, "db", "seeds", "demo", "identity", "verificaciones.json"), cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
