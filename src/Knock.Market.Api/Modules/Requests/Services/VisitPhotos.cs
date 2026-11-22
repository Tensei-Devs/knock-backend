namespace Knock.Market.Api.Modules.Requests.Services;

/// <summary>
/// Fotos privadas del trabajo terminado (spec 050, R-28, y spec 032). Cuando el módulo de archivos exponga <c>IMediaApi</c>, una sola clase de este módulo lo adapta a esta
/// interfaz. Mientras tanto, <see cref="UnavailableVisitPhotos"/> falla cerrado y las pruebas usan dobles.
/// </summary>
internal interface IVisitPhotos
{
    /// <summary>Verdadero solo si todas las fotos existen, son de <paramref name="ownerId"/> (el técnico) y están <c>listo</c>. No dice cuál condición falló.</summary>
    Task<bool> AreReadyForAsync(string ownerId, IReadOnlyList<string> photoIds, CancellationToken cancellationToken);
}

/// <summary>Sin módulo de archivos todavía: ninguna foto es válida, así que ninguna visita se termina. Cierra el paso en lugar de aceptar fotos que nadie confirmó.</summary>
internal sealed class UnavailableVisitPhotos : IVisitPhotos
{
    public Task<bool> AreReadyForAsync(string ownerId, IReadOnlyList<string> photoIds, CancellationToken cancellationToken) => Task.FromResult(false);
}
