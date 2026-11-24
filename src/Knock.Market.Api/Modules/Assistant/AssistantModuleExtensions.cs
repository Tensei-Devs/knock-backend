using Azure.Core;
using Azure.Identity;
using Knock.Hosting.Data;
using Knock.Hosting.Redis;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Assistant.Data;
using Knock.Market.Api.Modules.Assistant.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Assistant;

/// <summary>
/// Registro del módulo Assistant (spec 024, R-15). No expone rutas ni se agrega al gateway. Con <c>Asistente:Habilitado</c> en falso (por omisión) el cliente responde siempre con la plantilla, sin
/// red, Redis ni MongoDB; encendido, registra Redis, MongoDB y el cliente HTTP de Azure OpenAI. Solo este módulo conoce al proveedor (una prueba de arquitectura lo verifica).
/// </summary>
public static class AssistantModuleExtensions
{
    public const string MongoModule = "Assistant";

    public static IServiceCollection AddAssistantModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<AssistantOptions>().BindConfiguration(AssistantOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<AssistantOptions>, AssistantOptionsValidator>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAiImageSource, UnavailableAiImageSource>();
        services.AddSingleton(_ => PlantillaIa.Load());
        services.AddSingleton<IAiClient, AiClient>();
        services.AddKnockMongoDatabase(MongoModule);
        services.AddMemoryCache();
        services.TryAddSingleton<IAsistenteCuentas, AsistenteCuentasReal>();
        services.TryAddSingleton<IConversacionesAsistente, UnavailableConversaciones>();
        services.TryAddSingleton<ISolicitudesAsistente, AsistenteSolicitudesReal>();
        services.TryAddSingleton<IReferenciaMercado, UnavailableReferenciaMercado>();
        services.AddSingleton<IBitacoraStore>(provider => new BitacoraStore(provider.GetRequiredKeyedService<IMongoDatabase>(MongoModule)));
        services.AddSingleton<AsistenteService>();
        services.AddSingleton<RangoPrecioService>();
        services.AddSingleton<BitacoraService>();
        var options = configuration.GetSection(AssistantOptions.SectionName).Get<AssistantOptions>() ?? new AssistantOptions();
        if (options.Habilitado)
        {
            AddAzure(services, options);
        }
        else
        {
            services.AddSingleton<IProveedorIa, ClosedProvider>();
            services.AddSingleton<IConsumoIa, ClosedConsumption>();
            services.AddSingleton<IUsoIa, DiscardingUsage>();
        }

        return services;
    }

    private static void AddAzure(IServiceCollection services, AssistantOptions options)
    {
        services.AddKnockRedis(Knock.Market.Api.Hosting.MarketRedis.ClientName);
        // Sin llave (la cuenta tiene `local_auth_enabled=false`): el proveedor pide el token con la identidad administrada del servicio; un TokenCredential ya registrado (pruebas) gana.
        services.TryAddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
        services.AddHttpClient<IProveedorIa, ProveedorIa>(http =>
        {
            http.BaseAddress = new Uri(options.AzureOpenAI.Endpoint!, UriKind.Absolute);
            http.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.AddSingleton<IConsumoIa, ConsumoIaStore>();
        services.AddSingleton<IUsoIa>(provider => new UsoIaStore(provider.GetRequiredKeyedService<IMongoDatabase>(MongoModule)));
    }

    private sealed class ClosedProvider : IProveedorIa
    {
        public Task<ProviderResponse> CompleteAsync(string systemPrompt, string censoredText, IReadOnlyList<byte[]> images, int maxOutputTokens, CancellationToken cancellationToken) =>
            Task.FromResult(new ProviderResponse(null, 0, 0));
    }

    private sealed class ClosedConsumption : IConsumoIa
    {
        public Task<ReservationResult> ReserveAsync(string user, DateOnly day, int amount, int userLimit, int globalLimit, CancellationToken cancellationToken) =>
            Task.FromResult(new ReservationResult(ConsumptionResult.LimitUnavailable, null));

        public Task<AdjustmentResult> AdjustAsync(ConsumptionReservation reservation, int actual, CancellationToken cancellationToken) => Task.FromResult(AdjustmentResult.Unavailable);
    }

    private sealed class DiscardingUsage : IUsoIa
    {
        public Task SaveAsync(UsageRecord usage, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
