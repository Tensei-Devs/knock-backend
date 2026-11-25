using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Tecnicos.Services;

/// <summary>Quién es la persona detrás de cada perfil de técnico, para cruzarla con la proyección de bloqueos (spec 043, R-16).</summary>
internal interface ITechnicianOwnerReader
{
    /// <summary>Identificador del perfil → <c>usuarioId</c> de la cuenta, solo de los perfiles de <paramref name="technicianIds"/> que tienen cuenta.</summary>
    Task<IReadOnlyDictionary<string, string>> FindOwnersAsync(IReadOnlyCollection<ObjectId> technicianIds, CancellationToken cancellationToken);
}

/// <summary>
/// Búsquedas con sesión (spec 043, R-16): un técnico con un bloqueo en cualquier sentido con quien busca no aparece. Reutiliza la proyección <c>bloqueos</c> de Mercado a través de
/// <see cref="Requests.IRequestsApi"/>; se aplica después de la caché compartida, así que ninguna clave lleva datos de la persona (spec 046, R-07). Sin sesión no hay a quién cruzar.
/// </summary>
internal sealed class TechnicianBlockFilter(ITechnicianOwnerReader owners, Requests.IRequestsApi requests)
{
    /// <summary>De <paramref name="technicianIds"/>, los perfiles que <paramref name="viewer"/> no debe ver.</summary>
    public async Task<IReadOnlySet<string>> HiddenAsync(string? viewer, IEnumerable<string> technicianIds, CancellationToken cancellationToken)
    {
        var hidden = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(viewer))
        {
            return hidden;
        }

        var ids = technicianIds.Distinct(StringComparer.Ordinal).Select(x => ObjectId.TryParse(x, out var id) ? id : (ObjectId?)null).OfType<ObjectId>().ToArray();
        if (ids.Length == 0)
        {
            return hidden;
        }

        var ownerById = await owners.FindOwnersAsync(ids, cancellationToken);
        var blocked = await requests.BlockedWithAsync(viewer, ownerById.Values, cancellationToken);
        hidden.UnionWith(ownerById.Where(x => blocked.Contains(x.Value)).Select(x => x.Key));
        return hidden;
    }
}
