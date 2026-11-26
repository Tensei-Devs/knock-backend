extern alias MarketApi;

using System.Security.Cryptography;
using AwesomeAssertions;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Modules.Media.Data;
using MarketApi::Knock.Market.Api.Modules.Media.Options;

namespace Knock.IntegrationTest.Media;

/// <summary>
/// Spec 032, T-04: <see cref="AlmacenBlob"/> contra Azurite real (propiedades, descarga, publicación con encabezados y borrado).
/// Azurite se levanta sin OAuth, así que la llave de delegación de usuario y las SAS que firma no se prueban aquí: sus propiedades
/// se verifican en <c>Spec032SasTests</c> (unitaria) y el rechazo real del almacenamiento queda para T-09 y T-10.
/// </summary>
public class Spec032EntregaTests
{
    private static readonly byte[] _bytes = [1, 2, 3, 4, 5, 6, 7, 8];

    [Fact]
    public async Task Publica_Con_Cache_Inmutable_CA_032_06()
    {
        await using var azurite = await AzuriteFixture.StartAsync();
        var (almacen, servicio) = Crear(azurite);
        var publico = servicio.GetBlobContainerClient(ImagenesOptions.ContenedorPublico);
        await publico.CreateIfNotExistsAsync(PublicAccessType.Blob);
        var ruta = "trabajo/" + Convert.ToHexStringLower(SHA256.HashData(_bytes)) + ".jpg";

        var creado = await almacen.PublicarAsync(ruta, _bytes, CancellationToken.None);

        creado.Should().BeTrue();
        using var http = new HttpClient();
        using var respuesta = await http.GetAsync(publico.GetBlobClient(ruta).Uri);
        respuesta.IsSuccessStatusCode.Should().BeTrue("la lectura anónima por blob está habilitada en el contenedor público");
        respuesta.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
        respuesta.Headers.CacheControl!.ToString().Should().Be("public, max-age=31536000, immutable");
        respuesta.Content.Headers.ContentDisposition!.DispositionType.Should().Be("inline");
        (await respuesta.Content.ReadAsByteArrayAsync()).Should().Equal(_bytes);
    }

    [Fact]
    public async Task Misma_Ruta_No_Se_Reescribe_CA_032_17()
    {
        await using var azurite = await AzuriteFixture.StartAsync();
        var (almacen, servicio) = Crear(azurite);
        await servicio.GetBlobContainerClient(ImagenesOptions.ContenedorPublico).CreateIfNotExistsAsync(PublicAccessType.Blob);
        const string ruta = "perfil/aaaa.jpg";

        var primera = await almacen.PublicarAsync(ruta, _bytes, CancellationToken.None);
        var segunda = await almacen.PublicarAsync(ruta, _bytes, CancellationToken.None);

        primera.Should().BeTrue();
        segunda.Should().BeFalse("si ya existe un blob con ese nombre no se vuelve a escribir");
        (await azurite.ListBlobNamesAsync(ImagenesOptions.ContenedorPublico)).Should().ContainSingle();
    }

    [Fact]
    public async Task Propiedades_Descarga_Y_Borrado_De_Cuarentena_CA_032_11()
    {
        await using var azurite = await AzuriteFixture.StartAsync();
        var (almacen, servicio) = Crear(azurite);
        var cuarentena = servicio.GetBlobContainerClient(ImagenesOptions.ContenedorCuarentena);
        await cuarentena.CreateIfNotExistsAsync();
        var nombre = AlmacenBlob.NuevoNombreCuarentena();
        (await almacen.ObtenerPropiedadesAsync(nombre, CancellationToken.None)).Should().BeNull("no se subió nada");
        await cuarentena.GetBlobClient(nombre).UploadAsync(BinaryData.FromBytes(_bytes));

        var propiedades = (await almacen.ObtenerPropiedadesAsync(nombre, CancellationToken.None))!;
        var descargado = await almacen.DescargarAsync(nombre, propiedades, 5_242_880, CancellationToken.None);

        propiedades.Bytes.Should().Be(_bytes.Length);
        descargado.Should().Equal(_bytes);

        await cuarentena.GetBlobClient(nombre).UploadAsync(BinaryData.FromBytes([9, 9, 9]), overwrite: true);
        (await almacen.DescargarAsync(nombre, propiedades, 5_242_880, CancellationToken.None))
            .Should().BeNull("la versión cambió después de consultarla");

        await almacen.BorrarCuarentenaAsync(nombre, CancellationToken.None);
        await almacen.BorrarCuarentenaAsync(nombre, CancellationToken.None);
        (await azurite.ListBlobNamesAsync(ImagenesOptions.ContenedorCuarentena)).Should().BeEmpty();
    }

    [Fact]
    public async Task Descarga_No_Pasa_Del_Maximo_CA_032_11()
    {
        await using var azurite = await AzuriteFixture.StartAsync();
        var (almacen, servicio) = Crear(azurite);
        var cuarentena = servicio.GetBlobContainerClient(ImagenesOptions.ContenedorCuarentena);
        await cuarentena.CreateIfNotExistsAsync();
        var nombre = AlmacenBlob.NuevoNombreCuarentena();
        await cuarentena.GetBlobClient(nombre).UploadAsync(BinaryData.FromBytes(new byte[100]));
        var propiedades = (await almacen.ObtenerPropiedadesAsync(nombre, CancellationToken.None))!;

        var descargado = await almacen.DescargarAsync(nombre, propiedades, 10, CancellationToken.None);

        descargado.Should().HaveCount(11, "lee como mucho el máximo más un byte para que quien llama vea que se pasó");
    }

    private static (AlmacenBlob Almacen, BlobServiceClient Servicio) Crear(AzuriteFixture azurite)
    {
        var servicio = new BlobServiceClient(azurite.ConnectionString);
        return (new AlmacenBlob(servicio, servicio, TimeProvider.System), servicio);
    }
}
