namespace Knock.Market.Api.Modules.Requests;

/// <summary>Contrato público del módulo Requests hacia el resto del servicio. Crece con las tareas de la spec 020; hoy identifica al módulo, calcula la llegada y responde la participación de un técnico.</summary>
public interface IRequestsApi
{
    string ModuleName { get; }

    bool CanArrive(string state, string modality, string day, bool blocked, bool codeUsed, DateTime? expiresAtUtc, DateTime nowUtc);

    /// <summary>Si la solicitud llegó al técnico (el <paramref name="userId"/> del token) y, si ya se cerró, cuándo (spec 052, R-21: la bitácora se borra 30 días después del cierre). Un identificador mal formado o ajeno no participa.</summary>
    Task<(bool Participates, DateTime? ClosedAtUtc)> GetTechnicianAccessAsync(string solicitudId, string userId, CancellationToken cancellationToken);

    /// <summary>Cuándo se cerró cada una de las solicitudes pedidas, sin importar quién participó (spec 052, R-21). Las abiertas, inexistentes o con un identificador mal formado no aparecen.</summary>
    Task<IReadOnlyDictionary<string, DateTime>> GetClosedAtAsync(IReadOnlyCollection<string> solicitudIds, CancellationToken cancellationToken);

    /// <summary>De <paramref name="others"/>, las personas con un bloqueo en cualquier sentido con <paramref name="person"/> (spec 043, R-16: el bloqueo corta en ambos sentidos). Una sola consulta a la proyección <c>bloqueos</c>.</summary>
    Task<IReadOnlySet<string>> BlockedWithAsync(string person, IEnumerable<string> others, CancellationToken cancellationToken);
}
