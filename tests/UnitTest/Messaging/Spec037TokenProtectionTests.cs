extern alias messaging;
using System.Security.Cryptography;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;
using DeviceKeyConfiguration = messaging::Knock.Messaging.Api.Modules.Avisos.Services.DeviceKeyConfiguration;
using DispositivoId = messaging::Knock.Messaging.Api.Modules.Avisos.Domain.DispositivoId;
using RegistroDispositivo = messaging::Knock.Messaging.Api.Modules.Avisos.Dtos.RegistroDispositivo;
using RegistroDispositivoValidator = messaging::Knock.Messaging.Api.Modules.Avisos.Validators.RegistroDispositivoValidator;
using TokenProtection = messaging::Knock.Messaging.Api.Modules.Avisos.Services.TokenProtection;

namespace Knock.UnitTest.Messaging;

public sealed class Spec037TokenProtectionTests
{
    [Fact]
    public void Cipher_AuthenticatesOwnerAndId_AndUsesFreshNonce_CA_037_13()
    {
        var protection = new TokenProtection("1", RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32));
        const string token = "synthetic-push-token";
        var first = protection.Encrypt(token, "owner", "device");
        var second = protection.Encrypt(token, "owner", "device");

        first.Should().NotContain(token).And.NotBe(second);
        first.Should().StartWith("1.");
        protection.Decrypt(first, "owner", "device").Should().Be(token);
        var wrongOwner = () => protection.Decrypt(first, "other", "device");
        var wrongId = () => protection.Decrypt(first, "owner", "other");
        wrongOwner.Should().Throw<CryptographicException>();
        wrongId.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Fingerprint_IsDeterministicAndKeyed_CA_037_01()
    {
        var encryption = RandomNumberGenerator.GetBytes(32);
        var first = new TokenProtection("1", encryption, RandomNumberGenerator.GetBytes(32));
        var other = new TokenProtection("1", encryption, RandomNumberGenerator.GetBytes(32));

        first.Fingerprint("synthetic-push-token").Should().Be(first.Fingerprint("synthetic-push-token"));
        first.Fingerprint("synthetic-push-token").Should().NotBe(other.Fingerprint("synthetic-push-token"));
    }

    [Fact]
    public void Cipher_ReadsPreviousVersionAfterActiveKeyChanges()
    {
        var oldKey = RandomNumberGenerator.GetBytes(32);
        var newKey = RandomNumberGenerator.GetBytes(32);
        var indexKey = RandomNumberGenerator.GetBytes(32);
        var oldProtection = new TokenProtection("1", oldKey, indexKey);
        var stored = oldProtection.Encrypt("synthetic-push-token", "owner", "device");
        var rotated = new TokenProtection("2", new Dictionary<string, byte[]> { ["1"] = oldKey, ["2"] = newKey }, indexKey);

        rotated.Decrypt(stored, "owner", "device").Should().Be("synthetic-push-token");
        rotated.Encrypt("synthetic-push-token", "owner", "device").Should().StartWith("2.");
    }

    [Theory]
    [InlineData("android", true)]
    [InlineData("ios", true)]
    [InlineData("Android", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Registration_RequiresKnownPlatform(string? platform, bool valid)
    {
        var validator = new RegistroDispositivoValidator();
        validator.Validate(new RegistroDispositivo("synthetic-token", platform)).IsValid.Should().Be(valid);
    }

    [Fact]
    public void PublicIds_Are128BitHexAndDistinct()
    {
        var first = DispositivoId.Create();
        first.Should().HaveLength(32);
        Convert.FromHexString(first).Should().HaveCount(16);
        DispositivoId.Create().Should().NotBe(first);
    }

    [Fact]
    public void MissingOrInvalidKeys_DoNotEnableDeviceWrites()
    {
        var empty = new ConfigurationBuilder().Build();
        var invalid = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Avisos:Devices:ActiveEncryptionVersion"] = "1",
            ["Avisos:Devices:EncryptionKeys:1"] = "invalid",
            ["Avisos:Devices:IndexKey"] = "invalid",
        }).Build();

        DeviceKeyConfiguration.Read(empty).Protection.Should().BeNull();
        DeviceKeyConfiguration.Read(invalid).Protection.Should().BeNull();
        DeviceKeyConfiguration.Read(empty).WritesEnabled.Should().BeFalse();
    }

    [Fact]
    public void ValidLocalKeys_DoNotProveCrossReplicaConsistency()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Avisos:Devices:ActiveEncryptionVersion"] = "1",
            ["Avisos:Devices:EncryptionKeys:1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["Avisos:Devices:IndexKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        }).Build();

        var settings = DeviceKeyConfiguration.Read(config);
        settings.Protection.Should().NotBeNull();
        settings.WritesEnabled.Should().BeFalse();
    }

    [Fact]
    public void ValidKeysAndExplicitActivation_EnableWrites()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Avisos:Devices:WritesEnabled"] = "true",
            ["Avisos:Devices:ActiveEncryptionVersion"] = "1",
            ["Avisos:Devices:EncryptionKeys:1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["Avisos:Devices:IndexKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        }).Build();

        DeviceKeyConfiguration.Read(config).WritesEnabled.Should().BeTrue();
    }
}
