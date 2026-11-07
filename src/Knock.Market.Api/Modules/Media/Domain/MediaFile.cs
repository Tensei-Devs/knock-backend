namespace Knock.Market.Api.Modules.Media.Domain;

/// <summary>Uso previsto de la foto.</summary>
internal enum MediaUsage { Profile, Request, Job }
/// <summary>Estados del archivo.</summary>
internal enum MediaFileState { Pending, Ready, Rejected }
/// <summary>Formato real detectado por firma.</summary>
internal enum ImageFormat { Jpeg, Png }

/// <summary>Registro de un archivo privado. Dueño es el <c>sub</c> del JWT; nombres de blob los genera el servidor.</summary>
internal sealed record MediaFile(
    Guid Id, string OwnerId, MediaUsage Usage, MediaFileState State,
    string DeclaredContentType, long DeclaredSizeBytes, DateTimeOffset CreatedAt)
{
    public string? OriginalBlob { get; init; }
    public string? ReducedBlob { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public DateTimeOffset? ConfirmedAt { get; init; }
    public string QuarantineBlob => Id.ToString("N");
    public bool IsReadyFor(string ownerId) => State == MediaFileState.Ready && string.Equals(OwnerId, ownerId, StringComparison.Ordinal);
    public static MediaFile CreatePending(string ownerId, MediaUsage usage, string contentType, long sizeBytes, DateTimeOffset now) =>
        new(Guid.CreateVersion7(now), ownerId, usage, MediaFileState.Pending, contentType, sizeBytes, now);
    public MediaFile MarkReady(ImageFormat format, int width, int height, DateTimeOffset now)
    {
        EnsurePending();
        var extension = format == ImageFormat.Png ? "png" : "jpg";
        return this with { State = MediaFileState.Ready, OriginalBlob = $"{Id:N}/original.{extension}", ReducedBlob = $"{Id:N}/reducida.{extension}", Width = width, Height = height, ConfirmedAt = now };
    }
    public MediaFile MarkRejected(DateTimeOffset now)
    {
        EnsurePending();
        return this with { State = MediaFileState.Rejected, ConfirmedAt = now };
    }
    private void EnsurePending()
    {
        if (State != MediaFileState.Pending) throw new InvalidOperationException($"Media file is {State}; only pending files change state.");
    }
}
