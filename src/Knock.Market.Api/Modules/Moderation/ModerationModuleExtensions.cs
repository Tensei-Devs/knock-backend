using Azure.Core;
using Azure.Identity;
using Knock.Contracts.Moderation;
using Knock.Hosting.Data;
using Knock.Hosting.Redis;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Moderation.Data;
using Knock.Market.Api.Modules.Moderation.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Moderation;

/// <summary>
/// Adaptador del contrato de la spec 013 (<see cref="IContentModerator"/>) sobre el de la spec 031. Los llamadores existentes (solicitudes, notas, imágenes) no cambian: reciben la decisión real.
/// La persona sale del <c>sub</c> del token de la petición; sin sesión no hay tope por persona que aplicar y se falla cerrado.
/// </summary>
internal sealed class LegacyContentModeratorAdapter(IContentModeration moderation, IHttpContextAccessor accessor, TimeProvider clock) : IContentModerator
{
    public async Task<ModerationResult> ReviewTextAsync(string text, CancellationToken cancellationToken) =>
        Translate(await ModerateAsync(user => moderation.ModerateTextAsync(new TextModerationRequest(text, ModerationContext.SolicitudDescripcion, user), cancellationToken)), isImage: false);

    public async Task<ModerationResult> ReviewImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) =>
        Translate(await ModerateAsync(user => moderation.ModerateImageAsync(new ImageModerationRequest(image, "image/jpeg", ModerationContext.ImagenPublica, user), cancellationToken)), isImage: true);

    private async Task<ModerationVerdict> ModerateAsync(Func<string, Task<ModerationVerdict>> call) =>
        accessor.HttpContext?.User.FindFirst("sub")?.Value is { Length: > 0 } user ? await call(user) : ModerationVerdict.Failed(ModerationReasonCodes.Unavailable);

    /// <summary>El tope por persona se distingue de la falla porque el llamador responde 429 y no 503; los segundos salen del traductor único (R-13), igual en todos los llamadores.</summary>
    private ModerationResult Translate(ModerationVerdict verdict, bool isImage)
    {
        var problem = ModerationProblemMapper.Map(verdict, isImage, clock.GetUtcNow());
        var outcome = verdict.Decision switch
        {
            ModerationDecision.Approved => ModerationOutcome.Allowed,
            ModerationDecision.Rejected => ModerationOutcome.Rejected,
            _ when problem?.Status == StatusCodes.Status429TooManyRequests => ModerationOutcome.LimitReached,
            _ => ModerationOutcome.Unavailable,
        };
        return new ModerationResult(outcome, verdict.Categories.Count > 0 ? verdict.Categories[0] : null, problem?.RetryAfterSeconds);
    }
}

public static class ModerationModuleExtensions
{
    public const string MongoModule = "Moderation";

    /// <summary>
    /// Registra la moderación de la spec 031 (R-02). Sin <c>Moderacion:Habilitado</c> (por omisión) solo registra el moderador que rechaza con la lista local y falla cerrado en lo demás: no necesita
    /// Redis, MongoDB ni Azure. Debe llamarse ANTES de los módulos que registran su <see cref="IContentModerator"/> de respaldo (usan <c>TryAdd</c>).
    /// </summary>
    public static IServiceCollection AddModerationModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<ModerationOptions>().BindConfiguration(ModerationOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<ModerationOptions>, ModerationOptionsValidator>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.TryAddSingleton(_ => ListaLocal.LoadEmbedded());
        services.AddSingleton<IContentModerator, LegacyContentModeratorAdapter>();
        var options = configuration.GetSection(ModerationOptions.SectionName).Get<ModerationOptions>() ?? new ModerationOptions();
        if (options.Habilitado && options.Modo == ModerationModes.Azure)
        {
            AddAzure(services, options);
        }
        else
        {
            services.AddSingleton<IContentModeration>(provider => LocalOnlyModeration.Create(provider));
        }

        return services;
    }

    private static void AddAzure(IServiceCollection services, ModerationOptions options)
    {
        services.AddKnockRedis(Knock.Market.Api.Hosting.MarketRedis.ClientName);
        services.AddKnockMongoDatabase(MongoModule);
        // Sin llave (la cuenta del recurso tiene `local_auth_enabled=false`): el cliente pide el token con la identidad administrada del servicio; un TokenCredential ya registrado (pruebas) gana.
        services.TryAddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
        services.AddHttpClient<IContentSafetyClient, ContentSafetyClient>((provider, http) =>
        {
            http.BaseAddress = new Uri(options.ContentSafety.Endpoint!, UriKind.Absolute);
            http.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.AddSingleton<IConsumoModeracionStore, ConsumoModeracionStore>();
        services.AddSingleton<IUsoModeracionStore>(provider => new UsoModeracionStore(provider.GetRequiredKeyedService<IMongoDatabase>(MongoModule)));
        services.AddSingleton<IContentModeration, ContentSafetyModerator>();
    }
}
