using Knock.Market.Api.Modules.Media;

namespace Knock.UnitTest.Requests;

/// <summary>Módulo de archivos de mentira para las pruebas de los adaptadores de fotos: responde con <paramref name="answer"/> y anota cada consulta.</summary>
internal sealed class FakeMediaApi(Func<Guid, string, bool> answer) : IMediaApi
{
    public List<(Guid, string)> Calls { get; } = [];

    public Task<bool> IsReadyAndOwnedByAsync(Guid fileId, string ownerId, CancellationToken cancellationToken)
    {
        var result = answer(fileId, ownerId);
        Calls.Add((fileId, ownerId));
        return Task.FromResult(result);
    }
}
