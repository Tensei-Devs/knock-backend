using Knock.Market.Api.Modules.Media;

namespace Knock.UnitTest.Requests;

/// <summary>Módulo de archivos de mentira para las pruebas de los adaptadores de fotos: responde con <paramref name="answer"/>, anota cada consulta y su <c>uso</c>, y emite enlaces con <see cref="Links"/>.</summary>
internal sealed class FakeMediaApi(Func<Guid, string, bool> answer) : IMediaApi
{
    public List<(Guid, string)> Calls { get; } = [];

    /// <summary>Uso pedido en cada consulta de <see cref="IsReadyOwnedAndUsedForAsync"/>.</summary>
    public List<string> Uses { get; } = [];

    /// <summary>Cada petición de enlaces: quién la pidió, para qué uso y qué archivos.</summary>
    public List<(string Requester, string Use, IReadOnlyList<Guid> Files)> LinkRequests { get; } = [];

    /// <summary>Enlaces que devuelve <see cref="CreateReadLinksAsync"/>; por omisión uno ficticio por archivo.</summary>
    public Func<IReadOnlyList<Guid>, IReadOnlyList<(Guid FileId, Uri Url, DateTimeOffset ExpiresAt)>> Links { get; set; } =
        files => [.. files.Select(id => (id, new Uri("https://files.test/" + id.ToString("N") + "?sig=ficticia"), new DateTimeOffset(2026, 11, 22, 12, 5, 0, TimeSpan.Zero)))];

    public Task<bool> IsReadyAndOwnedByAsync(Guid fileId, string ownerId, CancellationToken cancellationToken)
    {
        var result = answer(fileId, ownerId);
        Calls.Add((fileId, ownerId));
        return Task.FromResult(result);
    }

    public Task<bool> IsReadyOwnedAndUsedForAsync(Guid fileId, string ownerId, string use, CancellationToken cancellationToken)
    {
        Uses.Add(use);
        return IsReadyAndOwnedByAsync(fileId, ownerId, cancellationToken);
    }

    public Task<IReadOnlyList<(Guid FileId, Uri Url, DateTimeOffset ExpiresAt)>> CreateReadLinksAsync(IReadOnlyList<Guid> fileIds, string use, string requesterSub, CancellationToken cancellationToken)
    {
        LinkRequests.Add((requesterSub, use, fileIds));
        return Task.FromResult(Links(fileIds));
    }
}
