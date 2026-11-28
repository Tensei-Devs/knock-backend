using Knock.Identity.Api.Modules.Account.Data;
using Knock.Identity.Api.Modules.Account.Domain;
using Knock.Identity.Api.Modules.Sesiones.Services;

namespace Knock.UnitTest.Identity;

/// <summary>Una sesión del doble: dueño, si sigue abierta.</summary>
internal sealed class FakeAccountSession(string id, string userId)
{
    public string Id { get; } = id;

    public string UserId { get; } = userId;

    public bool Revoked { get; set; }
}

/// <summary>Doble en memoria de <see cref="IAccountStore"/> (spec 039): guarda lo que el servicio escribe y deja simular carreras.</summary>
internal sealed class FakeAccountStore : IAccountStore
{
    public List<AccountSnapshot> Accounts { get; } = [];

    public List<FakeAccountSession> Sessions { get; } = [];

    /// <summary>Si es verdadero, el cambio de celular lo rechaza el índice único (otra cuenta ganó la carrera).</summary>
    public bool PhoneTakenOnChange { get; set; }

    /// <summary>Celulares guardados por <c>_id</c> de usuario: el último cifrado y su hash.</summary>
    public Dictionary<string, (ProtectedPhone Phone, PhoneHash Hash)> StoredPhones { get; } = [];

    public Task<AccountSnapshot?> FindBySubAsync(string sub, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.FirstOrDefault(a => a.Sub == sub));

    public Task<bool> PhoneHashExistsAsync(byte[] phoneHash, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.Any(a => a.PhoneHash.AsSpan().SequenceEqual(phoneHash)));

    public Task<IReadOnlyList<string>> FindOtherOpenSessionIdsAsync(string userId, string currentSessionId, DateTime now, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([.. Sessions.Where(s => s.UserId == userId && !s.Revoked && s.Id != currentSessionId).Select(s => s.Id)]);

    public Task<(PhoneChangeOutcome Outcome, IReadOnlyList<string> RevokedSessionIds)> ChangePhoneAsync(
        string userId, ProtectedPhone phone, PhoneHash hash, string currentSessionId, DateTime now, CancellationToken cancellationToken)
    {
        if (PhoneTakenOnChange)
        {
            return Task.FromResult<(PhoneChangeOutcome, IReadOnlyList<string>)>((PhoneChangeOutcome.PhoneTaken, []));
        }

        var index = Accounts.FindIndex(a => a.Id == userId);
        Accounts[index] = Accounts[index] with { PhoneHash = hash.Value };
        StoredPhones[userId] = (phone, hash);
        var revoked = Sessions.Where(s => s.UserId == userId && !s.Revoked && s.Id != currentSessionId).ToList();
        revoked.ForEach(s => s.Revoked = true);
        return Task.FromResult<(PhoneChangeOutcome, IReadOnlyList<string>)>((PhoneChangeOutcome.Changed, [.. revoked.Select(s => s.Id)]));
    }

    /// <summary>Casos de recuperación guardados, en orden.</summary>
    public List<RecoveryCase> RecoveryCases { get; } = [];

    public Task<AccountSnapshot?> FindByPhoneHashAsync(byte[] phoneHash, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.FirstOrDefault(a => a.PhoneHash.AsSpan().SequenceEqual(phoneHash)));

    public Task<bool> CreateRecoveryCaseIfNoneOpenAsync(RecoveryCase recoveryCase, CancellationToken cancellationToken)
    {
        if (RecoveryCases.Any(c => c.UserId == recoveryCase.UserId))
        {
            return Task.FromResult(false);
        }

        RecoveryCases.Add(recoveryCase);
        return Task.FromResult(true);
    }

    /// <summary>Casos de recuperación vencidos por la tarea <c>VencerRecuperacion</c>: usuario y segundo Unix de creación.</summary>
    public List<(string UserId, long CreatedAtSeconds)> ExpiredRecoveries { get; } = [];

    public Task<bool> ExpireRecoveryAsync(string userId, long createdAtSeconds, DateTime now, CancellationToken cancellationToken)
    {
        var index = RecoveryCases.FindIndex(c => c.UserId == userId && new DateTimeOffset(c.CreatedAt, TimeSpan.Zero).ToUnixTimeSeconds() == createdAtSeconds);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        RecoveryCases.RemoveAt(index);
        ExpiredRecoveries.Add((userId, createdAtSeconds));
        return Task.FromResult(true);
    }

    /// <summary>Cuentas con una exportación en curso (por <c>_id</c>).</summary>
    public HashSet<string> ExportsInProgress { get; } = [];

    public Task<ExportStartOutcome> TryStartExportAsync(string userId, DateTime now, CancellationToken cancellationToken) =>
        Task.FromResult(ExportsInProgress.Add(userId) ? ExportStartOutcome.Started : ExportStartOutcome.AlreadyInProgress);

    /// <summary>Última exportación lista por <c>_id</c> de la cuenta: nombre del ZIP y momento.</summary>
    public Dictionary<string, (string BlobName, DateTime ReadyAt)> LastExports { get; } = [];

    public Task SetLastExportAsync(string userId, string blobName, DateTime readyAt, CancellationToken cancellationToken)
    {
        LastExports[userId] = (blobName, readyAt);
        return Task.CompletedTask;
    }

    public Task<ExportStatus?> FindExportStatusAsync(string sub, CancellationToken cancellationToken)
    {
        var account = Accounts.FirstOrDefault(candidate => candidate.Sub == sub);
        if (account is null)
        {
            return Task.FromResult<ExportStatus?>(null);
        }

        var last = LastExports.TryGetValue(account.Id, out var stored) ? stored : default;
        return Task.FromResult<ExportStatus?>(new ExportStatus(ExportsInProgress.Contains(account.Id), last.BlobName, last.BlobName is null ? null : last.ReadyAt));
    }

    public Task ClearExportAsync(string userId, CancellationToken cancellationToken)
    {
        ExportsInProgress.Remove(userId);
        return Task.CompletedTask;
    }

    /// <summary>Consentimientos guardados por <c>sub</c>.</summary>
    public Dictionary<string, List<StoredConsent>> Consents { get; } = [];

    public Task<IReadOnlyList<StoredConsent>?> FindConsentsAsync(string sub, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StoredConsent>?>(Consents.TryGetValue(sub, out var list) ? [.. list] : null);

    public Task<StoredConsent?> SetOptionalConsentAsync(string sub, string type, bool active, string version, DateTime now, CancellationToken cancellationToken)
    {
        if (!Consents.TryGetValue(sub, out var list))
        {
            return Task.FromResult<StoredConsent?>(null);
        }

        list.RemoveAll(consent => consent.Document == type);
        var saved = new StoredConsent(type, version, now, active);
        list.Add(saved);
        return Task.FromResult<StoredConsent?>(saved);
    }

    /// <summary>Inicio de cada sesión (por id) para la regla de las 24 horas.</summary>
    public Dictionary<string, DateTime> SessionStarts { get; } = [];

    public Task<DateTime?> FindSessionStartAsync(string sessionId, string userId, CancellationToken cancellationToken) =>
        Task.FromResult<DateTime?>(SessionStarts.TryGetValue(sessionId, out var start) ? start : null);

    public Task<bool> ScheduleDeletionAsync(string userId, DateTime deleteAt, DateTime now, CancellationToken cancellationToken)
    {
        var index = Accounts.FindIndex(a => a.Id == userId);
        if (index < 0 || Accounts[index].State != "activa")
        {
            return Task.FromResult(false);
        }

        Accounts[index] = Accounts[index] with { State = "eliminacion_programada", DeleteAt = deleteAt };
        return Task.FromResult(true);
    }

    /// <summary>Cuentas borradas por <see cref="EraseAccountAsync"/>, por <c>_id</c>.</summary>
    public List<string> Erased { get; } = [];

    public Task<IReadOnlyList<string>?> EraseAccountAsync(string userId, DateTime now, CancellationToken cancellationToken)
    {
        var index = Accounts.FindIndex(a => a.Id == userId);
        if (index < 0 || Accounts[index].State != "eliminacion_programada" || Accounts[index].DeleteAt > now)
        {
            return Task.FromResult<IReadOnlyList<string>?>(null);
        }

        Accounts[index] = Accounts[index] with { State = "eliminada", DeleteAt = null };
        Erased.Add(userId);
        var revoked = Sessions.Where(s => s.UserId == userId && !s.Revoked).ToList();
        revoked.ForEach(s => s.Revoked = true);
        return Task.FromResult<IReadOnlyList<string>?>([.. revoked.Select(s => s.Id)]);
    }

    public Task<bool> CancelDeletionAsync(string userId, DateTime now, CancellationToken cancellationToken)
    {
        var index = Accounts.FindIndex(a => a.Id == userId);
        if (index < 0 || Accounts[index].State != "eliminacion_programada" || Accounts[index].DeleteAt <= now)
        {
            return Task.FromResult(false);
        }

        Accounts[index] = Accounts[index] with { State = "activa", DeleteAt = null };
        return Task.FromResult(true);
    }
}
