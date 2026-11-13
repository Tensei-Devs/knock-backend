using System.ComponentModel.DataAnnotations;

namespace Knock.Market.Api.Modules.Media.Data;

/// <summary>Configuración del módulo (sección <c>Media</c> de appsettings, sin secretos). OI-1: sin Blob ni Azure.</summary>
internal sealed class MediaOptions
{
    public const string SectionName = "Media";

    [Required] public string QuarantineContainer { get; set; } = "cuarentena";
    [Required] public string FilesContainer { get; set; } = "archivos";
    [Required] public string RecordsContainer { get; set; } = "registros-temporales";
    [Range(1, int.MaxValue)] public int ReducedMaxSide { get; set; } = 1024;
    [Range(1, int.MaxValue)] public int MaxConcurrentProcessing { get; set; } = 2;
}
