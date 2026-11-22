namespace Knock.Market.Api.Modules.Requests;

/// <summary>Contrato público del módulo Requests hacia el resto del servicio. Crece con las tareas de la spec 020; hoy identifica al módulo, calcula la llegada y responde la participación de un técnico.</summary>
public interface IRequestsApi
{
    string ModuleName { get; }

    bool CanArrive(string state, string modality, string day, bool blocked, bool codeUsed, DateTime? expiresAtUtc, DateTime nowUtc);

    /// <summary>Si la solicitud llegó al técnico (el <paramref name="userId"/> del token) y, si ya se cerró, cuándo (spec 052, R-21: la bitácora se borra 30 días después del cierre). Un identificador mal formado o ajeno no participa.</summary>
    Task<(bool Participates, DateTime? ClosedAtUtc)> GetTechnicianAccessAsync(string solicitudId, string userId, CancellationToken cancellationToken);
}
