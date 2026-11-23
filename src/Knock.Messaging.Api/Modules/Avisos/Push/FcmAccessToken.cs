using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Knock.Messaging.Api.Modules.Avisos.Push;

/// <summary>Token de acceso OAuth2 de la cuenta de servicio (JWT RS256 firmado con la llave privada, intercambiado en <c>token_uri</c>); se guarda hasta un minuto antes de vencer.</summary>
internal sealed class FcmAccessToken(HttpClient http, FcmOptions options, TimeProvider clock) : IDisposable
{
    private const string _scope = "https://www.googleapis.com/auth/firebase.messaging";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public async Task<string?> GetAsync(CancellationToken cancellationToken)
    {
        if (IsFresh())
        {
            return _token;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!IsFresh())
            {
                await RefreshAsync(cancellationToken);
            }

            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate() => _token = null;

    public void Dispose() => _gate.Dispose();

    private bool IsFresh() => _token is not null && clock.GetUtcNow() < _expiresAt;

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, options.TokenUri)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = BuildAssertion(),
            }),
        };
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _token = null;
            return;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        var seconds = root.TryGetProperty("expires_in", out var expires) && expires.TryGetInt32(out var value) ? value : 0;
        _token = root.TryGetProperty("access_token", out var token) ? token.GetString() : null;
        _expiresAt = clock.GetUtcNow().AddSeconds(Math.Max(0, seconds - 60));
    }

    private string BuildAssertion()
    {
        var now = clock.GetUtcNow();
        using var rsa = RSA.Create();
        rsa.ImportFromPem(options.PrivateKeyPem);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.ClientEmail,
            Audience = options.TokenUri.ToString(),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddHours(1).UtcDateTime,
            Claims = new Dictionary<string, object> { ["scope"] = _scope },
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256) { CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false } },
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
