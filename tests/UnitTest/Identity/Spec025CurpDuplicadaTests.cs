using System.Security.Cryptography;
using AwesomeAssertions;
using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Trust.Crypto;
using Knock.Identity.Api.Modules.Trust.Data;
using Knock.Identity.Api.Modules.Trust.Services;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 025, R-06 y CA-07: la búsqueda de duplicados usa el HMAC de todas las versiones de llave. CURP ficticia.</summary>
public sealed class Spec025CurpDuplicadaTests
{
    private const string _curp = "GODE561231HDFRRN09";

    private sealed class LiveCurpStore(IEnumerable<string> liveHmacs) : IVerificationStore
    {
        public IReadOnlyCollection<string>? Asked { get; private set; }

        public Task<bool> ExistsLiveCurpAsync(IReadOnlyCollection<string> curpHmacs, string exceptSub, CancellationToken cancellationToken)
        {
            Asked = curpHmacs;
            return Task.FromResult(curpHmacs.Any(liveHmacs.Contains));
        }

        public Task<VerificationAccount?> FindAccountAsync(string sub, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string?> FindSessionDeviceAsync(ObjectId userId, string sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StoredVerification?> FindLatestAsync(string sub, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task CreateAsync(NewVerification verification, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<VerificationForCapture?> FindOwnedAsync(string sub, ObjectId id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> MarkPhotoEncryptedAsync(ObjectId id, string type, DateTime confirmedAt, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SubmitOutcome> SubmitAsync(string sub, ObjectId id, DateTime sentAt, DateTime expiresAt, VerificationPurgeScheduler purgeScheduler, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Duplicada_Guardada_Con_La_Llave_Anterior_Se_Detecta_Tras_Rotar_CA_025_07()
    {
        var k1 = RandomNumberGenerator.GetBytes(32);
        var k2 = RandomNumberGenerator.GetBytes(32);
        var stored = new CurpHmac(new CurpHmacKeys(new Dictionary<int, byte[]> { [1] = k1 }, 1)).Compute(_curp);
        var rotated = new CurpHmac(new CurpHmacKeys(new Dictionary<int, byte[]> { [1] = k1, [2] = k2 }, 2));
        var store = new LiveCurpStore([stored]);

        var act = () => new CurpDuplicateGuard(rotated, store).EnsureNotDuplicateAsync("sub-2", _curp, CancellationToken.None);

        var error = (await act.Should().ThrowAsync<DomainException>()).Which;
        error.Code.Should().Be("curp_duplicate");
        error.Message.Should().NotContain(_curp);
        store.Asked.Should().HaveCount(2).And.Contain(stored);
    }

    [Fact]
    public async Task Curp_Sin_Duplicado_Pasa_CA_025_07()
    {
        var hmac = new CurpHmac(new CurpHmacKeys(new Dictionary<int, byte[]> { [1] = RandomNumberGenerator.GetBytes(32) }, 1));
        var act = () => new CurpDuplicateGuard(hmac, new LiveCurpStore([])).EnsureNotDuplicateAsync("sub-2", _curp, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
}
