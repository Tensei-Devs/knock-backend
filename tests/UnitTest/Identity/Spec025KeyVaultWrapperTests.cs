using System.Security.Cryptography;
using AwesomeAssertions;
using Azure;
using Azure.Core;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Trust;
using Knock.Identity.Api.Modules.Trust.Crypto;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 025 R-11: llave maestra de Key Vault detrás de <c>Verificacion:KeyVault:Habilitado</c>. El cliente de Key Vault se sustituye por un doble con RSA local generado en cada ejecución.</summary>
public sealed class Spec025KeyVaultWrapperTests
{
    private const string _version = "949eccb8002b446a8d096aca9fe8af76";
    private const string _otherVersion = "0123456789abcdef0123456789abcdef";

    private static KeyVaultVerificationOptions Options() => new() { Habilitado = true, VaultUri = "https://vault.example.test", KeyName = "curp-kek", KekVersion = _version };

    private static ServiceProvider Build(Dictionary<string, string?> values)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        services.AddLogging();
        services.AddKeyVaultVerificationKeyWrapper();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Switch_Off_Keeps_Closed_Failing_Wrapper_TC_025_KV_01()
    {
        using var provider = Build([]);

        provider.GetRequiredService<IVerificationKeyWrapper>().Should().BeOfType<UnavailableVerificationKeyWrapper>();
    }

    [Fact]
    public void Switch_Off_Ignores_Missing_Settings_TC_025_KV_02()
    {
        using var provider = Build(new() { ["Verificacion:KeyVault:Habilitado"] = "false" });

        provider.GetRequiredService<IVerificationKeyWrapper>().Should().BeOfType<UnavailableVerificationKeyWrapper>();
        provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<KeyVaultVerificationOptions>>().Value.Habilitado.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "curp-kek", _version)]
    [InlineData("http://vault.example.test", "curp-kek", _version)]
    [InlineData("https://vault.example.test", "curp kek", _version)]
    [InlineData("https://vault.example.test", "curp-kek", "no-es-hex")]
    [InlineData("https://vault.example.test", "curp-kek", "")]
    public void Switch_On_With_Bad_Settings_Fails_To_Start_TC_025_KV_03(string vault, string key, string version)
    {
        var result = new KeyVaultVerificationOptionsValidator().Validate(null, new KeyVaultVerificationOptions { Habilitado = true, VaultUri = vault, KeyName = key, KekVersion = version });

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Switch_On_With_Good_Settings_Uses_Key_Vault_Wrapper_TC_025_KV_04()
    {
        using var provider = Build(new()
        {
            ["Verificacion:KeyVault:Habilitado"] = "true",
            ["Verificacion:KeyVault:VaultUri"] = "https://vault.example.test",
            ["Verificacion:KeyVault:KekVersion"] = _version,
        });

        provider.GetRequiredService<IVerificationKeyWrapper>().Should().BeOfType<KeyVaultVerificationKeyWrapper>();
    }

    [Fact]
    public async Task Wrap_Then_Unwrap_Round_Trips_With_The_Version_In_The_Key_Id_TC_025_KV_05()
    {
        using var rsa = RSA.Create(2048);
        var ids = new List<Uri>();
        var wrapper = new KeyVaultVerificationKeyWrapper(Options(), id => { ids.Add(id); return new LocalCryptoClient(rsa, id, throws: null); });
        var dataKey = RandomNumberGenerator.GetBytes(32);

        var wrapped = await wrapper.WrapAsync(dataKey, CancellationToken.None);
        var back = await wrapper.UnwrapAsync(wrapped, CancellationToken.None);

        wrapped.KekVersion.Should().Be(_version);
        wrapped.Value.Should().NotContain(Convert.ToBase64String(dataKey));
        back.Should().Equal(dataKey);
        ids.Should().ContainSingle().Which.ToString().Should().Be($"https://vault.example.test/keys/curp-kek/{_version}");
    }

    [Fact]
    public async Task Unwrap_Uses_The_Stored_Version_After_A_Rotation_TC_025_KV_06()
    {
        using var rsa = RSA.Create(2048);
        var ids = new List<Uri>();
        var wrapper = new KeyVaultVerificationKeyWrapper(Options(), id => { ids.Add(id); return new LocalCryptoClient(rsa, id, throws: null); });
        var dataKey = RandomNumberGenerator.GetBytes(32);
        var old = new WrappedVerificationKey(Convert.ToBase64String(rsa.Encrypt(dataKey, RSAEncryptionPadding.OaepSHA256)), _otherVersion);

        var back = await wrapper.UnwrapAsync(old, CancellationToken.None);

        back.Should().Equal(dataKey);
        ids.Should().ContainSingle().Which.AbsolutePath.Should().EndWith(_otherVersion);
    }

    [Theory]
    [InlineData("vault")]
    [InlineData("auth")]
    [InlineData("bad-version")]
    [InlineData("bad-base64")]
    public async Task Vault_Failures_Become_503_Without_Content_TC_025_KV_07(string failure)
    {
        using var rsa = RSA.Create(2048);
        Exception? error = failure switch
        {
            "vault" => new RequestFailedException(403, "forbidden"),
            "auth" => new Azure.Identity.CredentialUnavailableException("no identity"),
            _ => null,
        };
        var wrapper = new KeyVaultVerificationKeyWrapper(Options(), id => new LocalCryptoClient(rsa, id, error));
        var wrapped = failure switch
        {
            "bad-version" => new WrappedVerificationKey("AAAA", "../../x"),
            "bad-base64" => new WrappedVerificationKey("%%%no-base64%%%", _version),
            _ => new WrappedVerificationKey(Convert.ToBase64String(new byte[256]), _version),
        };

        var wrap = () => wrapper.WrapAsync(RandomNumberGenerator.GetBytes(32), CancellationToken.None);
        var unwrap = () => wrapper.UnwrapAsync(wrapped, CancellationToken.None);

        if (error is not null)
        {
            (await wrap.Should().ThrowAsync<DomainException>()).Which.Kind.Should().Be(DomainErrorKind.ServiceUnavailable);
        }

        var thrown = (await unwrap.Should().ThrowAsync<DomainException>()).Which;
        thrown.Kind.Should().Be(DomainErrorKind.ServiceUnavailable);
        thrown.Message.Should().NotContain(wrapped.Value);
    }

    /// <summary>Doble de <see cref="CryptographyClient"/>: envuelve con una RSA local; si trae un error, lo lanza.</summary>
    private sealed class LocalCryptoClient(RSA rsa, Uri keyId, Exception? throws) : CryptographyClient
    {
        public override Task<WrapResult> WrapKeyAsync(KeyWrapAlgorithm algorithm, byte[] key, CancellationToken cancellationToken = default) =>
            throws is not null
                ? Task.FromException<WrapResult>(throws)
                : Task.FromResult(CryptographyModelFactory.WrapResult(keyId.ToString(), rsa.Encrypt(key, RSAEncryptionPadding.OaepSHA256), algorithm));

        public override Task<UnwrapResult> UnwrapKeyAsync(KeyWrapAlgorithm algorithm, byte[] encryptedKey, CancellationToken cancellationToken = default) =>
            throws is not null
                ? Task.FromException<UnwrapResult>(throws)
                : Task.FromResult(CryptographyModelFactory.UnwrapResult(keyId.ToString(), rsa.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256), algorithm));
    }
}
