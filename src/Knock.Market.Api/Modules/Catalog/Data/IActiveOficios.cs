using Knock.Market.Api.Modules.Catalog.Domain;

namespace Knock.Market.Api.Modules.Catalog.Data;

/// <summary>
/// Lectura de los oficios activos con la caché de 24 horas (spec 010 y spec 045, R-04 y R-19). Existe para probar <see cref="Services.OficioQueries"/> sin MongoDB ni Redis; la implementa
/// <see cref="CatalogStore"/>.
/// </summary>
internal interface IActiveOficios
{
    /// <summary>Oficios activos ordenados por <c>nombreBusqueda</c> y, a igual valor, por clave.</summary>
    Task<IReadOnlyList<OficioListing>> FindActiveOficiosAsync(CancellationToken cancellationToken);

    /// <summary>Borra la caché de oficios activos y sube su generación (spec 045, R-19): la usan las escrituras del panel. Si falla, el error sube: una escritura no puede dejar datos viejos en la caché.</summary>
    Task InvalidateActiveOficiosAsync(CancellationToken cancellationToken);
}
