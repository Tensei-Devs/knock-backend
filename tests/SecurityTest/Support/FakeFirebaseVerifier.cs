extern alias IdentityApi;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using IdentityApi::Knock.Identity.Api.Modules.Sesiones.Services;

namespace Knock.SecurityTest.Support;

/// <summary>
/// Doble del verificador de Firebase (spec 015, R-13): reconoce solo los tokens que la prueba registró y rechaza cualquier otro con <c>null</c>,
/// igual que el verificador real (que nunca distingue el motivo). Solo datos ficticios; no hay emulador ni red.
/// </summary>
internal sealed class FakeFirebaseVerifier : IFirebaseIdTokenVerifier
{
    private readonly ConcurrentDictionary<string, FirebaseIdentity> _tokens = new(StringComparer.Ordinal);
    private int _calls;

    /// <summary>Cuántas veces se llamó al verificador: los cuerpos inválidos no deben llegar hasta aquí.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Registra un token válido para <paramref name="uid"/> y <paramref name="phone"/> y lo devuelve.</summary>
    public string Issue(string uid, string phone)
    {
        var token = "fake-firebase-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        _tokens[token] = new FirebaseIdentity(uid, phone);
        return token;
    }

    public Task<FirebaseIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult(_tokens.TryGetValue(idToken, out var identity) ? identity : null);
    }
}
