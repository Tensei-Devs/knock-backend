namespace Knock.Identity.Api.Modules.Account.Services;

/// <summary>
/// Salida de eventos de la cuenta hacia la bandeja de salida y las tareas programadas (specs 037 y 054). La implementación escribe en esos dos mecanismos; ninguna
/// carga ni clave de negocio lleva datos personales (spec 054, R-02).
/// </summary>
internal interface IAccountOutbox
{
    /// <summary>Deja el aviso <c>sesion_revocada { sid }</c> (spec 037, R-13) para que el despachador borre los dispositivos de esa sesión.</summary>
    Task EnqueueSessionRevokedAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Programa la tarea <c>PrepararExportacion</c> (spec 054) de la persona: reúne sus datos, arma el ZIP y la avisa con un enlace de 24 horas.</summary>
    Task EnqueueExportAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Programa la tarea <c>EliminarCuenta</c> (spec 054) del <paramref name="accountSub"/> para cuando venza la gracia (<paramref name="deleteAt"/>). Cancelar y volver a programar crea otra tarea.</summary>
    Task EnqueueDeletionAsync(string accountSub, DateTime deleteAt, CancellationToken cancellationToken);

    /// <summary>Deja el evento <c>CuentaEliminada { sub }</c> para Mercado (cancelar visitas confirmadas, anonimizar opiniones, spec 039 R-25). Repetirlo no duplica.</summary>
    Task EnqueueAccountDeletedAsync(string accountSub, CancellationToken cancellationToken);
}
