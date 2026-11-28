namespace Knock.Identity.Api.Modules.Trust.Domain;

/// <summary>Razones de una purga de fotos (spec 057, §5): al decidir, al vencer los 7 días o al dar de baja la cuenta.</summary>
internal static class PurgeReasons
{
    public const string Decided = "decidida";
    public const string Expired = "vencida";
    public const string AccountDeletion = "baja_de_cuenta";

    /// <summary>Quien deja constancia cuando no hay verificador: el vencimiento y la baja (spec 057, D-06).</summary>
    public const string SystemActor = "sistema";
}

/// <summary>Nombres de la auditoría, los eventos y las tareas de la purga (spec 057, §4). Las cargas llevan solo identificadores y razón.</summary>
internal static class PurgeEvents
{
    public const string AuditPurged = "verificacion_purgada";
    public const string UnreviewedAlert = "VerificacionSinRevisarAlertada";
    public const string PhotosDeleted = "FotosDeVerificacionBorradas";
    public const string AlertTask = "AvisarAlVerificador";
    public const string DeleteTask = "BorrarFotosDeVerificacion";
}
