using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.Hosting.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Knock.UnitTest.Hosting;

/// <summary>
/// Spec 056, R-08 y CA-08 (T-06): los registros de una llamada de servicio aceptada y de una rechazada, con el nivel de detalle más alto (Trace), no contienen la clave de ningún emisor,
/// la clave anterior, el token ni ninguna de sus partes. Las claves y los tokens son sintéticos y se generan al azar en cada ejecución.
/// </summary>
public sealed class Spec056RegistrosTests
{
    private const string _scheme = "InternalTest";
    private const string _issuer = "knock-market";
    private const string _audience = "knock-test-receiver";
    private const string _keySetting = "Security:InternalTokens:MarketKey";

    private static readonly string[] _permissions = ["prueba"];

    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);
    private readonly byte[] _otherKey = RandomNumberGenerator.GetBytes(32);
    private readonly byte[] _previousKey = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public async Task AcceptedAndRejectedCalls_LeaveNoKeyOrTokenInTheLogs_CA_056_08()
    {
        var accepted = Token(_key, TimeSpan.FromMinutes(1));
        var wrongKey = Token(_otherKey, TimeSpan.FromMinutes(1));
        var expired = Token(_key, TimeSpan.FromMinutes(1), issuedAt: DateTime.UtcNow.AddMinutes(-10));
        var secrets = new List<string>(Secrets(_key, _otherKey, _previousKey)) { accepted, wrongKey, expired };
        secrets.AddRange(accepted.Split('.').Concat(wrongKey.Split('.')).Concat(expired.Split('.')).Where(part => part.Length >= 16));
        using var logs = new CapturingLoggerProvider();
        var writtenProblems = new List<string>();
        using var provider = Provider(logs, writtenProblems);

        var okResult = await Authenticate(provider, accepted);
        var wrongKeyResult = await Authenticate(provider, wrongKey);
        var expiredResult = await Authenticate(provider, expired);
        var malformedResult = await Authenticate(provider, "no.es.un-token");
        var missingResult = await Authenticate(provider, null);

        okResult.Succeeded.Should().BeTrue();
        wrongKeyResult.Succeeded.Should().BeFalse("la firma es de otra clave");
        expiredResult.Succeeded.Should().BeFalse("el token venció");
        malformedResult.Succeeded.Should().BeFalse("no es un token");
        missingResult.Succeeded.Should().BeFalse("no trae credencial");
        logs.Entries.Should().NotBeEmpty("el nivel Trace deja registros de cada llamada");
        foreach (var secret in secrets)
        {
            logs.Entries.Should().NotContain(entry => entry.Contains(secret, StringComparison.Ordinal), "ningún registro repite una clave, un token ni una de sus partes");
            writtenProblems.Should().NotContain(problem => problem.Contains(secret, StringComparison.Ordinal));
        }
    }

    private ServiceProvider Provider(CapturingLoggerProvider logs, List<string> problems)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [_keySetting] = Convert.ToBase64String(_key),
            [_keySetting + ServiceCredential.PreviousSuffix] = Convert.ToBase64String(_previousKey),
            [_keySetting + ServiceCredential.PreviousUntilSuffix] = DateTimeOffset.UtcNow.AddHours(1).ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        services.AddAuthentication().AddKnockServiceCredential(new ServiceCredentialSettings
        {
            Scheme = _scheme,
            Audience = _audience,
            IssuerKeyConfigurationKeys = new Dictionary<string, string> { [_issuer] = _keySetting },
            WriteProblem = (_, status, title, code) =>
            {
                problems.Add($"{status} {title} {code}");
                return Task.CompletedTask;
            },
            UnauthorizedCode = "unauthorized",
        });
        return services.BuildServiceProvider();
    }

    private static async Task<AuthenticateResult> Authenticate(ServiceProvider provider, string? token)
    {
        // Un ámbito por petición: el manejador de autenticación guarda su resultado dentro del ámbito.
        using var scope = provider.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Response.Body = new MemoryStream();
        if (token is not null)
        {
            context.Request.Headers.Authorization = "Bearer " + token;
        }

        var result = await context.AuthenticateAsync(_scheme);
        if (!result.Succeeded)
        {
            await context.ChallengeAsync(_scheme);
        }

        return result;
    }

    private static IEnumerable<string> Secrets(params byte[][] keys) =>
        keys.SelectMany(key => (string[])[Convert.ToBase64String(key), Convert.ToHexString(key), Convert.ToBase64String(key).TrimEnd('=')]);

    private static string Token(byte[] key, TimeSpan lifetime, DateTime? issuedAt = null)
    {
        var issued = issuedAt ?? DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _issuer,
            Audience = _audience,
            Claims = new Dictionary<string, object> { ["permisos"] = _permissions, ["jti"] = Guid.NewGuid().ToString("N") },
            IssuedAt = issued,
            NotBefore = issued.AddSeconds(-1),
            Expires = issued + lifetime,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256),
        });
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (owner.Entries)
                {
                    owner.Entries.Add(formatter(state, exception) + " " + exception);
                }
            }
        }
    }
}
