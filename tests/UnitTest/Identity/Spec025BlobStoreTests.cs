using System.Security.Cryptography;
using System.Web;
using AwesomeAssertions;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Knock.Identity.Api.Modules.Trust;
using Knock.Identity.Api.Modules.Trust.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>
/// Spec 025, T-04 (R-08): el cliente real de Blob de verificación detrás de <c>Verificacion:Blob:Habilitado</c> (apagado) y sus SAS de un solo blob. La firma usa una llave de delegación sintética
/// generada en cada ejecución; no hay red ni cuenta real.
/// </summary>
public sealed class Spec025BlobStoreTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan _fiveMinutes = TimeSpan.FromMinutes(5);

    private static VerificationBlobOptions Options() => new() { Habilitado = true, CuentaUrl = "https://cuenta.example.test" };

    private static ServiceProvider Build(Dictionary<string, string?> values)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddBlobVerificationStore();
        return services.BuildServiceProvider();
    }

    private static UserDelegationKey Key() => BlobsModelFactory.UserDelegationKey(
        Guid.NewGuid().ToString(),
        Guid.NewGuid().ToString(),
        _now.AddHours(-1),
        _now.AddHours(23),
        "b",
        "2024-11-04",
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    private static Dictionary<string, string> Query(Uri url)
    {
        var parsed = HttpUtility.ParseQueryString(url.Query);
        return parsed.AllKeys.ToDictionary(key => key!, key => parsed[key]!);
    }

    [Fact]
    public void Switch_Off_Keeps_The_Closed_Failing_Store_CA_025_03()
    {
        using var provider = Build([]);

        provider.GetRequiredService<IVerificationBlobStore>().Should().BeOfType<UnavailableVerificationBlobStore>();
        provider.GetRequiredService<IOptions<VerificationBlobOptions>>().Value.Habilitado.Should().BeFalse();
    }

    [Fact]
    public void Switch_On_With_Good_Settings_Uses_The_Azure_Store_CA_025_03()
    {
        using var provider = Build(new() { ["Verificacion:Blob:Habilitado"] = "true", ["Verificacion:Blob:CuentaUrl"] = "https://cuenta.example.test" });

        provider.GetRequiredService<IVerificationBlobStore>().Should().BeOfType<AzureVerificationBlobStore>();
    }

    [Theory]
    [InlineData("", "verificaciones-cuarentena", "verificaciones")]
    [InlineData("http://cuenta.example.test", "verificaciones-cuarentena", "verificaciones")]
    [InlineData("https://cuenta.example.test", "Mayusculas", "verificaciones")]
    [InlineData("https://cuenta.example.test", "verificaciones-cuarentena", "ab")]
    [InlineData("https://cuenta.example.test", "mismo-nombre", "mismo-nombre")]
    public void Switch_On_With_Bad_Settings_Fails_To_Start_CA_025_03(string account, string quarantine, string final)
    {
        var options = new VerificationBlobOptions { Habilitado = true, CuentaUrl = account, ContenedorCuarentena = quarantine, ContenedorDefinitivo = final };

        new VerificationBlobOptionsValidator().Validate(null, options).Failed.Should().BeTrue();
    }

    [Fact]
    public void Switch_Off_Ignores_Missing_Settings_CA_025_03()
    {
        new VerificationBlobOptionsValidator().Validate(null, new VerificationBlobOptions()).Succeeded.Should().BeTrue();
        new VerificationBlobOptionsValidator().Validate(null, Options()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Upload_Link_Is_Write_Only_One_Blob_Https_Five_Minutes_CA_025_03()
    {
        var blob = new BlobServiceClient(new Uri(Options().CuentaUrl)).GetBlobContainerClient("verificaciones-cuarentena").GetBlobClient("ejemplo/selfie");

        var url = AzureVerificationBlobStore.Sign(blob, Key(), BlobSasPermissions.Create | BlobSasPermissions.Write, _now, _fiveMinutes);

        var query = Query(url);
        query["sp"].Should().Be("cw", "solo crear y escribir: sin lectura ni listado");
        query["sr"].Should().Be("b", "un solo blob");
        query["spr"].Should().Be("https");
        DateTimeOffset.Parse(query["se"], System.Globalization.CultureInfo.InvariantCulture).Should().Be(_now.AddMinutes(5));
        DateTimeOffset.Parse(query["st"], System.Globalization.CultureInfo.InvariantCulture).Should().Be(_now - AzureVerificationBlobStore.ClockSkew);
        url.AbsolutePath.Should().EndWith("/verificaciones-cuarentena/ejemplo/selfie");
    }

    [Fact]
    public void Read_Link_Is_Read_Only_One_Blob_Of_The_Final_Container_CA_025_03()
    {
        var blob = new BlobServiceClient(new Uri(Options().CuentaUrl)).GetBlobContainerClient("verificaciones").GetBlobClient("ejemplo/selfie");

        var query = Query(AzureVerificationBlobStore.Sign(blob, Key(), BlobSasPermissions.Read, _now, _fiveMinutes));

        query["sp"].Should().Be("r");
        query["sr"].Should().Be("b");
    }

    [Theory]
    [InlineData(301)]
    [InlineData(3600)]
    [InlineData(0)]
    [InlineData(-5)]
    public void Link_Longer_Than_Five_Minutes_Or_Already_Expired_Is_Refused_CA_025_03(int seconds)
    {
        var blob = new BlobServiceClient(new Uri(Options().CuentaUrl)).GetBlobContainerClient("verificaciones").GetBlobClient("ejemplo/selfie");

        var act = () => AzureVerificationBlobStore.Sign(blob, Key(), BlobSasPermissions.Read, _now, TimeSpan.FromSeconds(seconds));

        act.Should().Throw<ArgumentOutOfRangeException>("el SAS nunca vive más de 5 minutos ni nace vencido");
    }
}
