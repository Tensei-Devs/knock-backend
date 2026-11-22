namespace Knock.Messaging.Api.Modules.Conversaciones;

/// <summary>Contrato del módulo Conversaciones para los demás módulos de Mensajería.</summary>
internal interface IConversacionesApi
{
    /// <summary>Deja de solo lectura las conversaciones abiertas entre las dos personas por un bloqueo (spec 043, R-18), sin borrar mensajes y con un motivo que no lo delata (R-17).</summary>
    Task<long> CloseForBlockAsync(string personA, string personB, DateTime nowUtc, CancellationToken cancellationToken);
}
