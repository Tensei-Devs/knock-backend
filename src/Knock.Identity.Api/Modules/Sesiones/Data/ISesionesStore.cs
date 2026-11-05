using Knock.Identity.Api.Modules.Sesiones.Domain;

namespace Knock.Identity.Api.Modules.Sesiones.Data;

/// <summary>Lo que el canje necesita de las colecciones <c>usuarios</c> y <c>sesiones</c>; existe para probar el flujo con dobles sin MongoDB.</summary>
internal interface ISesionesStore
{
    Task<UserAccount?> FindBySubAsync(string sub, CancellationToken cancellationToken);

    Task<UserAccount?> FindByPhoneHashAsync(byte[] phoneHash, CancellationToken cancellationToken);

    Task<AccountCreation> CreateAccountAsync(NewAccount account, CancellationToken cancellationToken);

    /// <summary>Abre una sesión de una cuenta que ya existe y, si la cuenta pasa de <see cref="NewSession.MaxOpenPerAccount"/>, revoca la más antigua.</summary>
    Task AddSessionAsync(NewSession session, CancellationToken cancellationToken);
}
