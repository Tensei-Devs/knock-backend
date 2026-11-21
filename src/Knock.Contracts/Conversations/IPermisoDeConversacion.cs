namespace Knock.Contracts.Conversations;

/// <summary>Lo que se pudo saber del permiso de una conversación. <see cref="NoDisponible"/> es 0 para que un valor sin asignar falle cerrado: nunca se asume que se puede conversar.</summary>
public enum EstadoConsultaPermiso
{
    NoDisponible = 0,
    Denegado = 1,
    Concedido = 2,
}

/// <summary>Resultado de preguntar a Mercado por el permiso. <see cref="Permiso"/> solo viene con <see cref="EstadoConsultaPermiso.Concedido"/>.</summary>
public sealed record ResultadoPermiso(EstadoConsultaPermiso Estado, PermisoConversacion? Permiso = null);

/// <summary>
/// Pregunta a Mercado quién participa en el par (solicitud, técnico) y si la conversación está abierta (spec 030, R-02 y R-09), reenviando la sesión de quien pide.
/// Con <c>tecnicoId</c> el cliente; sin él, el técnico. Sin respuesta de Mercado devuelve <see cref="EstadoConsultaPermiso.NoDisponible"/>: nunca se asume permiso.
/// </summary>
public interface IPermisoDeConversacion
{
    Task<ResultadoPermiso> ConsultarAsync(string solicitudId, string? tecnicoId, string bearerToken, CancellationToken cancellationToken);
}
