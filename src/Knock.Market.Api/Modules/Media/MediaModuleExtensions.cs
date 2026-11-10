using Azure.Storage.Blobs;
using Knock.Market.Api.Modules.Media.Data;
using Knock.Market.Api.Modules.Media.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Knock.Market.Api.Modules.Media;

/// <summary>Module registration (spec 013). Only this class and <see cref="IMediaApi"/> are public; the rest is internal.</summary>
public static class MediaModuleExtensions
{
    public static IServiceCollection AddMediaModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<MediaOptions>()
            .BindConfiguration(MediaOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton(provider =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(MediaOptions.ConnectionStringName);
            return string.IsNullOrWhiteSpace(connectionString)
                ? throw new InvalidOperationException($"ConnectionStrings:{MediaOptions.ConnectionStringName} is not configured.")
                : new BlobServiceClient(connectionString);
        });
        services.AddSingleton<IMediaFileStore, BlobMediaFileStore>();
        services.AddSingleton<IMediaBlobStore, AzureMediaBlobStore>();
        services.AddSingleton<ImageProcessor>();
        services.AddSingleton<MediaService>();
        services.AddSingleton<IMediaApi, MediaApi>();
        return services;
    }
}
