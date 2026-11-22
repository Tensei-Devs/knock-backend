using Knock.Identity.Api.Modules.Panel;

namespace Knock.Identity.Api.Modules.Sesiones;

/// <summary>Una cuenta de la app tal como la ven los demás módulos: su <c>sub</c> y si está suspendida, nada más (spec 033, R-06).</summary>
public sealed record AppAccount(string Sub, bool IsSuspended);

/// <summary>Estado y rol de una cuenta leídos dentro de una transacción para decidir si se suspende (spec 059, R-03, R-06).</summary>
public sealed record AccountForSuspension(string State, bool IsTechnician);

/// <summary>
/// Lo que el módulo de sesiones ofrece a los demás módulos de Identidad sin que lean su colección ni su dominio (spec 002): buscar una cuenta de la app
/// por su <c>sub</c> o por su celular. El celular solo se usa para buscar por su HMAC y nunca se devuelve.
/// </summary>
public interface ISesionesApi
{
    /// <summary>La cuenta de la app de ese <c>sub</c> (<paramref name="subject"/>), o <see langword="null"/> si no existe.</summary>
    Task<AppAccount?> FindAccountBySubAsync(string subject, CancellationToken cancellationToken);

    /// <summary>El <c>sub</c> de la cuenta de la app con ese celular E.164 (+52, ya validado), o <see langword="null"/> si no existe.</summary>
    Task<string?> FindSubByPhoneAsync(string phone, CancellationToken cancellationToken);

    /// <summary>Lee estado y rol de la cuenta dentro de la transacción de quien llama (spec 059); <see langword="null"/> si no existe.</summary>
    Task<AccountForSuspension?> ReadAccountForSuspensionAsync(StoreTransaction transaction, string subject, CancellationToken cancellationToken);

    /// <summary>
    /// Suspende la cuenta con una operación condicionada a que siga en <paramref name="expectedState"/>, escribe <c>usuarios.suspension</c> y revoca todas sus sesiones, en la
    /// misma transacción de quien llama (spec 059, R-04 a, b, c). Devuelve los <c>sid</c> revocados, o <see langword="null"/> si el estado ya cambió y no se tocó nada.
    /// </summary>
    Task<IReadOnlyList<string>?> SuspendAccountAsync(StoreTransaction transaction, string subject, string expectedState, string type, string reportId, DateTime since, CancellationToken cancellationToken);

    /// <summary>Cambia <c>suspendida</c> a <c>activa</c> y borra <c>usuarios.suspension</c> dentro de la transacción; <see langword="false"/> si no estaba suspendida. No restaura sesiones (spec 059, R-15).</summary>
    Task<bool> ReactivateAccountAsync(StoreTransaction transaction, string subject, CancellationToken cancellationToken);

    /// <summary>Escribe <c>sesion-cancelada:{sid}</c> en Redis por 15 minutos para cada sesión (spec 016, R-06). Lanza si Redis falla; el llamador reintenta.</summary>
    Task CancelSessionsAsync(IReadOnlyList<string> sessionIds, CancellationToken cancellationToken);
}
