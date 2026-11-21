using Azure.Core;
using Azure.Storage.Blobs;
using Knock.Contracts.Moderation;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Media.Data;
using Knock.Market.Api.Modules.Media.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Modules.Media.Options;

internal static class ImagenesServiceCollectionExtensions
{
    /// <summary>Registra <see cref="ImagenesOptions"/> validadas al arrancar y <see cref="ImagenPublicaUrl"/> (spec 032).</summary>
    public static IServiceCollection AddImagenesPublicas(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<ImagenesOptions>()
            .BindConfiguration(ImagenesOptions.SectionName)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ImagenesOptions>, ImagenesOptionsValidator>());
        services.TryAddSingleton<ImagenPublicaUrl>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddKnockMongoDatabase(MediaMongo.ModuleKey);
        services.TryAddSingleton<ISubidasImagenStore, SubidasImagenStore>();
        services.TryAddSingleton<IAlmacenBlob>(CrearAlmacen);
        services.TryAddSingleton<ProcesadorImagen>(provider => new ProcesadorImagen(provider.GetRequiredService<ImageProcessor>()));
        services.AddLimitesImagen();
        services.TryAddSingleton<SubidaImagenService>();
        return services;
    }

    /// <summary>
    /// Clientes de Blob de las imágenes (spec 032, R-02): en Azure, una URL de servicio por cuenta (<c>Imagenes:CuentaPrivadaUrl</c> y <c>Imagenes:CuentaPublicaUrl</c>) y la
    /// identidad administrada (un <see cref="TokenCredential"/> registrado; <c>DefaultAzureCredential</c> exige el paquete Azure.Identity, pendiente de validar); en local y en pruebas, la cadena de Azurite (<c>ConnectionStrings:KnockBlob</c>) para las dos cuentas.
    /// </summary>
    private static AlmacenBlob CrearAlmacen(IServiceProvider provider)
    {
        var configuration = provider.GetRequiredService<IConfiguration>();
        var privada = configuration[$"{ImagenesOptions.SectionName}:CuentaPrivadaUrl"];
        var publica = configuration[$"{ImagenesOptions.SectionName}:CuentaPublicaUrl"];
        var reloj = provider.GetRequiredService<TimeProvider>();
        if (!string.IsNullOrWhiteSpace(privada) && !string.IsNullOrWhiteSpace(publica))
        {
            var credencial = provider.GetService<TokenCredential>()
                ?? throw new InvalidOperationException("A TokenCredential (managed identity) must be registered to use Imagenes account URLs.");
            return new AlmacenBlob(new BlobServiceClient(new Uri(privada), credencial), new BlobServiceClient(new Uri(publica), credencial), reloj);
        }

        var conexion = configuration.GetConnectionString(MediaOptions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(conexion))
        {
            throw new InvalidOperationException($"{ImagenesOptions.SectionName}:CuentaPrivadaUrl and CuentaPublicaUrl, or ConnectionStrings:{MediaOptions.ConnectionStringName}, must be configured.");
        }

        var servicio = new BlobServiceClient(conexion);
        return new AlmacenBlob(servicio, servicio, reloj);
    }
}
