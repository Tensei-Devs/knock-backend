namespace Knock.Contracts.Colonias;

/// <summary>
/// Consulta al catálogo de colonias, que es del servicio de Mercado (spec 017, R-06). Identidad la usa para validar la colonia de un perfil
/// sin leer la base de Mercado. La implementación puede fallar si Mercado no responde; quien llama decide qué hacer (spec 017, R-11: 503).
/// </summary>
public interface IColoniaCatalog
{
    /// <summary>Indica si la colonia existe en el catálogo.</summary>
    /// <exception cref="HttpRequestException">Mercado no respondió o respondió con un error.</exception>
    /// <exception cref="TimeoutException">Mercado no respondió a tiempo.</exception>
    Task<bool> ExistsAsync(string colonia, CancellationToken cancellationToken);
}
