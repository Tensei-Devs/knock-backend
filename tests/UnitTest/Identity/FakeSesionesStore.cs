using Knock.Identity.Api.Modules.Sesiones.Data;
using Knock.Identity.Api.Modules.Sesiones.Domain;

namespace Knock.UnitTest.Identity;

/// <summary>
/// Doble en memoria de las colecciones <c>usuarios</c> y <c>sesiones</c>: guarda lo que el servicio escribe y deja simular la carrera de creación.
/// La rotación compara la huella vigente igual que el <c>updateOne</c> condicionado de MongoDB.
/// </summary>
internal sealed class FakeSesionesStore : ISesionesStore
{
    public List<UserAccount> Users { get; } = [];

    public List<NewAccount> Created { get; } = [];

    /// <summary>Sesiones abiertas por el canje, en orden.</summary>
    public List<NewSession> Sessions { get; } = [];

    /// <summary>Estado de las sesiones tal como lo ve la renovación (huella vigente, usadas, revocada, vencimiento).</summary>
    public List<FakeSession> Stored { get; } = [];

    /// <summary>Si se asigna, el siguiente alta la rechaza (otro canje ganó) y deja esta cuenta guardada.</summary>
    public UserAccount? WinnerOfRace { get; set; }

    /// <summary>Si es verdadero, la primera lectura por <c>sub</c> no ve la cuenta (otro canje la confirma entre las dos lecturas).</summary>
    public bool HideSubOnFirstRead { get; set; }

    public Task<UserAccount?> FindBySubAsync(string sub, CancellationToken cancellationToken)
    {
        if (HideSubOnFirstRead)
        {
            HideSubOnFirstRead = false;
            return Task.FromResult<UserAccount?>(null);
        }

        return Task.FromResult(Users.FirstOrDefault(a => a.Sub == sub));
    }

    public Task<UserAccount?> FindByPhoneHashAsync(byte[] phoneHash, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(a => a.PhoneHash.AsSpan().SequenceEqual(phoneHash)));

    public Task<UserAccount?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(a => a.Id == userId));

    public Task<AccountCreation> CreateAccountAsync(NewAccount account, CancellationToken cancellationToken)
    {
        if (WinnerOfRace is { } winner)
        {
            Users.Add(winner);
            WinnerOfRace = null;
            return Task.FromResult(AccountCreation.AlreadyExists);
        }

        Created.Add(account);
        Track(account.Session);
        Users.Add(new UserAccount(account.Id, account.Sub, AccountState.Active, false, account.PhoneHash));
        return Task.FromResult(AccountCreation.Created);
    }

    public Task AddSessionAsync(NewSession session, CancellationToken cancellationToken)
    {
        Track(session);
        return Task.CompletedTask;
    }

    public Task<StoredSession?> FindSessionByFingerprintAsync(byte[] fingerprint, CancellationToken cancellationToken) =>
        Task.FromResult(Stored.FirstOrDefault(s => s.Fingerprint.AsSpan().SequenceEqual(fingerprint))?.ToRecord());

    public Task<StoredSession?> FindSessionByUsedFingerprintAsync(byte[] fingerprint, CancellationToken cancellationToken) =>
        Task.FromResult(Stored.FirstOrDefault(s => s.Used.Any(used => used.AsSpan().SequenceEqual(fingerprint)))?.ToRecord());

    public Task<bool> RotateAsync(string sessionId, byte[] currentFingerprint, byte[] newFingerprint, DateTime now, DateTime newExpiresAt, CancellationToken cancellationToken)
    {
        var session = Stored.FirstOrDefault(s => s.Id == sessionId);
        if (session is null || session.Revoked || session.ExpiresAt <= now || !session.Fingerprint.AsSpan().SequenceEqual(currentFingerprint))
        {
            return Task.FromResult(false);
        }

        session.Used.Add(session.Fingerprint);
        session.Used.RemoveRange(0, Math.Max(0, session.Used.Count - SesionesStore.MaxUsedFingerprints));
        session.Fingerprint = newFingerprint;
        session.ExpiresAt = newExpiresAt;
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<string>> RevokeAllAsync(string userId, CancellationToken cancellationToken)
    {
        var open = Stored.Where(s => s.UserId == userId && !s.Revoked).ToList();
        foreach (var session in open)
        {
            session.Revoked = true;
        }

        return Task.FromResult<IReadOnlyList<string>>([.. open.Select(s => s.Id)]);
    }

    public Task<bool> RevokeAsync(string sessionId, string sub, CancellationToken cancellationToken)
    {
        var session = Stored.FirstOrDefault(s => s.Id == sessionId && s.Sub == sub);
        if (session is null)
        {
            return Task.FromResult(false);
        }

        session.Revoked = true;
        return Task.FromResult(true);
    }

    private void Track(NewSession session)
    {
        Sessions.Add(session);
        Stored.Add(new FakeSession
        {
            Id = session.Id,
            UserId = session.UserId,
            Sub = session.Sub,
            Fingerprint = session.Fingerprint,
            ExpiresAt = session.ExpiresAt,
        });
    }
}

/// <summary>Sesión mutable del doble: lo que cambia la rotación y la revocación.</summary>
internal sealed class FakeSession
{
    public required string Id { get; init; }

    public required string UserId { get; init; }

    public required string Sub { get; init; }

    public required byte[] Fingerprint { get; set; }

    public List<byte[]> Used { get; } = [];

    public bool Revoked { get; set; }

    public DateTime ExpiresAt { get; set; }

    public StoredSession ToRecord() => new(Id, UserId, Sub, Revoked, ExpiresAt);
}
