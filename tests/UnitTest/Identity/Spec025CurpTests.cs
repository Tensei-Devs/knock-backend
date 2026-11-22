using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using Knock.Identity.Api.Modules.Trust.Crypto;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 025, T-03: HMAC de la CURP y cifrado con llave por verificación. CURP y contenidos ficticios; las llaves se generan en cada prueba.</summary>
public sealed class Spec025CurpTests
{
    private const string _curp = "GODE561231HDFRRN09";
    private const string _otherCurp = "GODE561231MDFRRN06";

    private static readonly byte[] _photo = [.. Enumerable.Range(0, 5000).Select(i => (byte)(i % 251))];

    private static CurpHmacKeys NewKeys() => new(new Dictionary<int, byte[]> { [1] = RandomNumberGenerator.GetBytes(32) }, 1);

    [Fact]
    public void Curp_Solo_Como_Hmac_CA_025_06()
    {
        var stored = new CurpHmac(NewKeys()).Compute(_curp);
        stored.Should().StartWith("1.").And.NotContainEquivalentOf(_curp);
        stored[2..].Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Hmac_Es_Estable_Y_Normaliza_Mayusculas_Y_Espacios_CA_025_06()
    {
        var hmac = new CurpHmac(NewKeys());
        hmac.Compute("  gode561231hdfrrn09 ").Should().Be(hmac.Compute(_curp));
    }

    [Fact]
    public void Hmac_Depende_De_La_Llave_Y_De_La_Curp_CA_025_06()
    {
        new CurpHmac(NewKeys()).Compute(_curp).Should().NotBe(new CurpHmac(NewKeys()).Compute(_curp));
        var hmac = new CurpHmac(NewKeys());
        hmac.Compute(_curp).Should().NotBe(hmac.Compute(_otherCurp));
    }

    [Fact]
    public void Hmac_Rechaza_Curp_Invalida_Sin_Incluirla_En_El_Error_CA_025_06()
    {
        var act = () => new CurpHmac(NewKeys()).Compute("ABC-no-es-curp");
        act.Should().Throw<ArgumentException>().Which.Message.Should().NotContain("ABC-no-es-curp");
    }

    [Fact]
    public void Hmac_Con_Llave_Corta_O_Version_Inexistente_Falla_CA_025_06()
    {
        var shortKey = () => new CurpHmacKeys(new Dictionary<int, byte[]> { [1] = new byte[31] }, 1);
        shortKey.Should().Throw<ArgumentException>();
        var noVersion = () => new CurpHmacKeys(new Dictionary<int, byte[]> { [1] = new byte[32] }, 2);
        noVersion.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rotacion_Conserva_La_Busqueda_De_Duplicados_CA_025_06()
    {
        var k1 = RandomNumberGenerator.GetBytes(32);
        var k2 = RandomNumberGenerator.GetBytes(32);
        var old = new CurpHmac(new CurpHmacKeys(new Dictionary<int, byte[]> { [1] = k1 }, 1)).Compute(_curp);
        var rotated = new CurpHmac(new CurpHmacKeys(new Dictionary<int, byte[]> { [1] = k1, [2] = k2 }, 2));
        rotated.Compute(_curp).Should().StartWith("2.");
        rotated.ComputeAll(_curp).Should().Contain(old);
        rotated.Matches(_curp, old).Should().BeTrue();
        rotated.Matches(_otherCurp, old).Should().BeFalse();
        rotated.Matches(_curp, "9.abc").Should().BeFalse();
        rotated.Matches(_curp, "sin-version").Should().BeFalse();
    }

    internal sealed class FakeWrapper : IVerificationKeyWrapper
    {
        private readonly byte[] _kek = RandomNumberGenerator.GetBytes(32);

        public Task<WrappedVerificationKey> WrapAsync(byte[] dataKey, CancellationToken cancellationToken)
        {
            var packed = new byte[12 + 16 + dataKey.Length];
            RandomNumberGenerator.Fill(packed.AsSpan(0, 12));
            using var aes = new AesGcm(_kek, 16);
            aes.Encrypt(packed.AsSpan(0, 12), dataKey, packed.AsSpan(28), packed.AsSpan(12, 16));
            return Task.FromResult(new WrappedVerificationKey(Convert.ToBase64String(packed), "kek-1"));
        }

        public Task<byte[]> UnwrapAsync(WrappedVerificationKey wrapped, CancellationToken cancellationToken)
        {
            var packed = Convert.FromBase64String(wrapped.Value);
            var plain = new byte[packed.Length - 28];
            using var aes = new AesGcm(_kek, 16);
            aes.Decrypt(packed.AsSpan(0, 12), packed.AsSpan(28), packed.AsSpan(12, 16), plain);
            return Task.FromResult(plain);
        }
    }

    [Fact]
    public async Task Cifra_Y_Descifra_Con_La_Llave_Recuperada_De_La_Envuelta_CA_025_06()
    {
        var cifrado = new CifradoDeVerificacion(new FakeWrapper());
        var (key, wrapped) = await cifrado.CreateKeyAsync(CancellationToken.None);
        var envelope = CifradoDeVerificacion.Encrypt(key, _photo, "v1|selfie");
        wrapped.KekVersion.Should().Be("kek-1");
        wrapped.Value.Should().NotBeNullOrEmpty();
        envelope.Should().HaveCount(4 + 12 + 16 + _photo.Length);
        Encoding.ASCII.GetString(envelope, 0, 4).Should().Be("KVF1");
        envelope.AsSpan(32).ToArray().Should().NotEqual(_photo);

        using var again = await cifrado.UnwrapKeyAsync(wrapped, CancellationToken.None);
        CifradoDeVerificacion.Decrypt(again, envelope, "v1|selfie").Should().Equal(_photo);
    }

    [Fact]
    public async Task Dos_Cifrados_Del_Mismo_Contenido_Difieren_Por_El_Nonce_CA_025_06()
    {
        var cifrado = new CifradoDeVerificacion(new FakeWrapper());
        var (key, _) = await cifrado.CreateKeyAsync(CancellationToken.None);
        CifradoDeVerificacion.Encrypt(key, _photo, "c").Should().NotEqual(CifradoDeVerificacion.Encrypt(key, _photo, "c"));
    }

    [Fact]
    public async Task Cada_Verificacion_Tiene_Su_Propia_Llave_Y_No_Descifra_Las_Ajenas_CA_025_06()
    {
        var cifrado = new CifradoDeVerificacion(new FakeWrapper());
        var (a, wa) = await cifrado.CreateKeyAsync(CancellationToken.None);
        var (b, wb) = await cifrado.CreateKeyAsync(CancellationToken.None);
        wa.Value.Should().NotBe(wb.Value);
        var envelope = CifradoDeVerificacion.Encrypt(a, _photo, "c");
        var act = () => CifradoDeVerificacion.Decrypt(b, envelope, "c");
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public async Task Contexto_Distinto_O_Blob_Alterado_No_Se_Descifra_CA_025_06()
    {
        var cifrado = new CifradoDeVerificacion(new FakeWrapper());
        var (key, _) = await cifrado.CreateKeyAsync(CancellationToken.None);
        var envelope = CifradoDeVerificacion.Encrypt(key, _photo, "v1|selfie");
        var wrongContext = () => CifradoDeVerificacion.Decrypt(key, envelope, "v1|ine_frente");
        wrongContext.Should().Throw<CryptographicException>();
        envelope[^1] ^= 0xFF;
        var tampered = () => CifradoDeVerificacion.Decrypt(key, envelope, "v1|selfie");
        tampered.Should().Throw<CryptographicException>();
        var unknown = () => CifradoDeVerificacion.Decrypt(key, new byte[10], "v1|selfie");
        unknown.Should().Throw<CryptographicException>();
    }

    [Fact]
    public async Task Llave_Desechada_Queda_En_Ceros_CA_025_06()
    {
        var cifrado = new CifradoDeVerificacion(new FakeWrapper());
        var (key, _) = await cifrado.CreateKeyAsync(CancellationToken.None);
        key.Dispose();
        key.Bytes.Should().OnlyContain(b => b == 0);
    }
}
