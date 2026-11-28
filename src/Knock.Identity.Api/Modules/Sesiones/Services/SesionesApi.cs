using Knock.Identity.Api.Modules.Panel;
using Knock.Identity.Api.Modules.Sesiones.Data;
using Knock.Identity.Api.Modules.Sesiones.Domain;

namespace Knock.Identity.Api.Modules.Sesiones.Services;

/// <summary>Implementación de <see cref="ISesionesApi"/> sobre el almacén de cuentas y el HMAC del celular.</summary>
internal sealed class SesionesApi(ISesionesStore store, PhoneProtector phones, ISesionesSuspensionStore? suspensionStore = null, ISesionCancelada? cancelledSessions = null) : ISesionesApi
{
    public async Task<AppAccount?> FindAccountBySubAsync(string subject, CancellationToken cancellationToken)
    {
        var account = await store.FindBySubAsync(subject, cancellationToken);
        return account is null ? null : new AppAccount(account.Sub, account.State == AccountState.Suspended);
    }

    public async Task<string?> FindSubByPhoneAsync(string phone, CancellationToken cancellationToken)
    {
        var account = await store.FindByPhoneHashAsync(phones.Hash(phone).Value, cancellationToken);
        return account?.Sub;
    }

    private ISesionesSuspensionStore Suspension => suspensionStore ?? throw new InvalidOperationException("The suspension store is not configured.");

    private ISesionCancelada Cancelled => cancelledSessions ?? throw new InvalidOperationException("The cancelled-session list is not configured.");

    public Task<AccountForSuspension?> ReadAccountForSuspensionAsync(StoreTransaction transaction, string subject, CancellationToken cancellationToken) =>
        Suspension.ReadAsync(transaction, subject, cancellationToken);

    public Task<IReadOnlyList<string>?> SuspendAccountAsync(StoreTransaction transaction, string subject, string expectedState, string type, string reportId, DateTime since, DateTime? expiresAt, CancellationToken cancellationToken) =>
        Suspension.SuspendAsync(transaction, subject, expectedState, type, MongoDB.Bson.ObjectId.Parse(reportId), since, expiresAt, cancellationToken);

    public Task<AppSuspension?> FindSuspensionByPhoneAsync(string phone, CancellationToken cancellationToken) =>
        Suspension.FindSuspensionByPhoneHashAsync(phones.Hash(phone).Value, cancellationToken);

    public Task<AppSuspension?> FindSuspensionBySubAsync(string subject, CancellationToken cancellationToken) =>
        Suspension.FindSuspensionBySubAsync(subject, cancellationToken);

    public Task<bool> ReactivateAccountAsync(StoreTransaction transaction, string subject, CancellationToken cancellationToken) =>
        Suspension.ReactivateAsync(transaction, subject, cancellationToken);

    public async Task CancelSessionsAsync(IReadOnlyList<string> sessionIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionIds);
        foreach (var sessionId in sessionIds)
        {
            await Cancelled.CancelAsync(sessionId, _sessionCancellationLifetime, cancellationToken);
        }
    }

    /// <summary>Vigencia del JWT más largo que pudo emitirse (spec 016, R-06).</summary>
    private static readonly TimeSpan _sessionCancellationLifetime = TimeSpan.FromMinutes(15);
}
