using Knock.Identity.Api.Modules.Account.Services;

namespace Knock.Identity.Api.Modules.Account.Data;

/// <summary>Lo que la exportación (spec 039, R-22) lee de <c>usuarios</c>: solo los datos que la persona dio o aceptó. Existe para probar el manejador sin MongoDB.</summary>
internal interface IAccountExportSource
{
    /// <summary>Datos de la cuenta por su <c>_id</c> (hexadecimal); <c>null</c> si no existe o ya está eliminada.</summary>
    Task<AccountExportRecord?> FindAsync(string userId, CancellationToken cancellationToken);
}
