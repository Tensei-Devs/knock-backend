using Azure.Storage.Blobs;
using Knock.Contracts.Moderation;
using Knock.Hosting.Data;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Media.Data;
using Knock.Market.Api.Modules.Media.Options;
using Knock.Market.Api.Modules.Media.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Knock.Market.Api.Modules.Media;

/// <summary>Module registration (spec 013). Only this class and <see cref="IMediaApi"/> are public; the rest is internal.</summary>
public static class MediaModuleExtensions
{
    public static IServiceCollection AddMediaModule(this IServiceCollection services, bool isDevelopment = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (isDevelopment)
        {
            services.TryAddSingleton<IContentModerator, UnavailableModerator>();
        }
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<MediaOptions>()
            .BindConfiguration(MediaOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton(ImagenesServiceCollectionExtensions.CrearClientePrivado);
        services.AddKnockMongoDatabase(ArchivosMongo.ModuleKey);
        services.AddSingleton<IMediaFileStore, MongoMediaFileStore>();
        services.AddSingleton<IMediaBlobStore, AzureMediaBlobStore>();
        services.AddSingleton<ImageProcessor>();
        services.AddSingleton<MediaMetrics>();
        services.AddSingleton<MediaService>();
        services.AddSingleton<IMediaApi, MediaApi>();
        services.AddImagenesPublicas();
        return services;
    }

    private sealed class UnavailableModerator : IContentModerator
    {
        public Task<ModerationResult> ReviewTextAsync(string text, CancellationToken cancellationToken) =>
            Task.FromResult(new ModerationResult(ModerationOutcome.Unavailable));

        public Task<ModerationResult> ReviewImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) =>
            Task.FromResult(new ModerationResult(ModerationOutcome.Unavailable));
    }
}
