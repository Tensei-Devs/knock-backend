namespace Knock.Market.Api.Modules.Catalog.Services;

/// <summary>Consulta interna «¿existe una colonia con este nombre?» (spec 017, R-06); la interfaz deja a las pruebas sustituir el catálogo.</summary>
internal interface INeighborhoodNames
{
    /// <summary>Compara sin distinguir mayúsculas ni acentos (el mismo <c>nombreBusqueda</c> con el que se guardan las colonias).</summary>
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken);
}
