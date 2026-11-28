namespace Knock.Contracts.Safety;

/// <summary>
/// Evento <c>BloqueoCreado</c> (spec 043, R-23): Identidad lo publica por la bandeja de salida (spec 054) y Mercado y Mensajería mantienen con él su proyección mínima <c>bloqueos</c>.
/// Solo lleva los dos identificadores y la fecha: ningún nombre, teléfono ni texto. El consumidor debe tratarlo de forma idempotente (el mismo par no cambia nada la segunda vez).
/// </summary>
public sealed record BlockCreated(string BlockerId, string BlockedId, DateTime OccurredAtUtc)
{
    public const string EventType = "BloqueoCreado";
}

/// <summary>Evento <c>BloqueoEliminado</c> (spec 043, R-23): quita el par de la proyección <c>bloqueos</c>; repetirlo no cambia nada.</summary>
public sealed record BlockRemoved(string BlockerId, string BlockedId, DateTime OccurredAtUtc)
{
    public const string EventType = "BloqueoEliminado";
}
