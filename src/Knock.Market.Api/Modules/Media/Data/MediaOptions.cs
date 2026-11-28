using System.ComponentModel.DataAnnotations;

namespace Knock.Market.Api.Modules.Media.Data;

/// <summary>Module configuration (<c>Media</c> section of appsettings, no secrets).</summary>
internal sealed class MediaOptions
{
    public const string SectionName = "Media";
    public const string ConnectionStringName = "KnockBlob";

    [Required] public string QuarantineContainer { get; set; } = "cuarentena";
    [Required] public string FilesContainer { get; set; } = Domain.BlobPrivado.ContenedorArchivos;
    [Range(1, int.MaxValue)] public int ReducedMaxSide { get; set; } = 1600;
    [Range(1, int.MaxValue)] public int ProfileReducedMaxSide { get; set; } = 512;
    /// <summary>Tope de subidas pendientes vigentes por persona (spec 058, R-09). Se valida al arrancar.</summary>
    [Range(1, 1000)] public int MaxPendingUploads { get; set; } = Domain.MediaRules.DefaultMaxPendingUploads;
    [Range(1, int.MaxValue)] public int MaxConcurrentProcessing { get; set; } = 1;
}
