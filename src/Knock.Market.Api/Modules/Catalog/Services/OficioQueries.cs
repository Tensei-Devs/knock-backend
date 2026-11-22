using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Modules.Catalog.Data;
using Knock.Market.Api.Modules.Catalog.Domain;
using Knock.Market.Api.Modules.Catalog.Dtos;

namespace Knock.Market.Api.Modules.Catalog.Services;

/// <summary>Consultas de solo lectura de los oficios del catálogo para la API HTTP del módulo.</summary>
internal sealed class OficioQueries(IActiveOficios store)
{
    /// <summary>Oficios activos ordenados por <c>nombreBusqueda</c> (sin mayúsculas ni acentos) y, a igual valor, por clave.</summary>
    /// <remarks>
    /// Regla de spec TT-PRU-01 §8 D4 tras la migración a MongoDB (TT-BD-06): usa el campo normalizado e indexado, por lo que ya no
    /// aplica el límite de acentos de <c>COLLATE "C"</c>. Solo <see cref="CatalogStore"/> toca <c>IMongoCollection</c>.
    /// </remarks>
    public async Task<IReadOnlyList<OficioResponse>> ListActiveAsync(CancellationToken cancellationToken) =>
        [.. (await store.FindActiveOficiosAsync(cancellationToken)).Select(o => new OficioResponse(o.Clave, o.Nombre, o.Modalidad))];

    /// <summary>
    /// Detalle de un oficio activo (spec 045, OFI-02). Sale de la misma lectura con caché de 24 horas que la lista, así que una edición del panel que invalida esa caché se ve en la siguiente consulta
    /// (R-04 y R-19). Un oficio que no existe o está inactivo lanza 404 <c>trade_not_found</c>, sin distinguir los dos casos.
    /// </summary>
    public async Task<TradeDetailResponse> GetActiveAsync(string clave, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clave);
        var listing = (await store.FindActiveOficiosAsync(cancellationToken)).FirstOrDefault(o => string.Equals(o.Clave, clave, StringComparison.Ordinal))
            ?? throw new DomainException(DomainErrorKind.NotFound, "The trade does not exist or is not active.", TradeErrorCodes.TradeNotFoundCode);
        return new TradeDetailResponse(listing.Clave, listing.Nombre, listing.Modalidad, listing.FrasesSugeridas ?? []);
    }
}
