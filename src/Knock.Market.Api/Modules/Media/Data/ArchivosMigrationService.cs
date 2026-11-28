using System.ComponentModel.DataAnnotations;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Modules.Media.Data;

/// <summary>Configuración de la migración explícita (spec 058, R-08/R-17): apagada por omisión; el nombre del contenedor de origen no tiene valor por omisión y lo pone la persona que la ejecuta.</summary>
internal sealed class ArchivosMigrationOptions
{
    public const string SectionName = "Media:Migracion";

    public bool Habilitada { get; set; }

    [MaxLength(63)]
    public string? ContenedorOrigen { get; set; }

    public static bool IsValid(ArchivosMigrationOptions options) => !options.Habilitada || !string.IsNullOrWhiteSpace(options.ContenedorOrigen);
}

/// <summary>
/// Corre el migrador una vez al arrancar solo cuando <c>Media:Migracion:Habilitada</c> es verdadera (spec 058, R-08: «comando o bandera de configuración»; sin tarea programada). Es idempotente por <c>archivoId</c>, así que
/// varias réplicas a la vez no duplican. Un fallo se registra y no tumba el servicio.
/// </summary>
internal sealed partial class ArchivosMigrationService(
    IOptions<ArchivosMigrationOptions> options,
    IServiceProvider services,
    ILogger<ArchivosMigrationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Habilitada)
        {
            return;
        }

        try
        {
            await Task.Yield();
            // Se resuelven solo al ejecutar: con la bandera apagada el servicio no exige ningún cliente de Blob.
            await services.GetRequiredService<ArchivosMigrationRunner>().RunAsync(services.GetRequiredService<BlobServiceClient>(), options.Value.ContenedorOrigen!, stoppingToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Failed(logger, error.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Archivos migration failed: {ErrorType}")]
    private static partial void Failed(ILogger logger, string errorType);
}
