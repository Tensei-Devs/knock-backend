namespace Knock.Messaging.Api.Modules.Bloqueos;

/// <summary>Contrato del módulo Bloqueos para los demás módulos de Mensajería (spec 043, R-16): solo consulta, nunca la proyección.</summary>
internal interface IBloqueosApi
{
    /// <summary>Si alguna de las dos personas bloqueó a la otra (el bloqueo corta en ambos sentidos).</summary>
    Task<bool> AreBlockedAsync(string personA, string personB, CancellationToken cancellationToken);
}
