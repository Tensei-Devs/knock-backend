using System.Web;
using AwesomeAssertions;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Knock.Market.Api.Modules.Media.Data;
using Xunit;

namespace Knock.UnitTest.Media;

/// <summary>
/// Spec 032, R-05, R-02 y CA-01/CA-03/CA-04 (parte verificable sin almacenamiento): la SAS de subida es de delegación de usuario,
/// de un solo blob de cuarentena, con permisos <c>cw</c>, solo HTTPS y vigencia de 5 minutos. Que el almacenamiento real rechace
/// el enlace vencido o el uso ajeno se comprueba en las pruebas de seguridad de la tarea T-09 y en el humo de T-10.
/// </summary>
public sealed class Spec032SasTests
{
    private static readonly DateTimeOffset _ahora = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
    private const string _cuentaPrivada = "stknockprivada";
    private const string _cuentaPublica = "stknockpublica";

    [Fact]
    public async Task Emite_Enlace_De_Un_Blob_CA_032_01()
    {
        using var almacen = CrearAlmacen(out var privada, out _);
        var nombre = AlmacenBlob.NuevoNombreCuarentena();

        var enlace = await almacen.CrearEnlaceSubidaAsync(nombre, "image/jpeg", CancellationToken.None);

        var consulta = HttpUtility.ParseQueryString(enlace.Url.Query);
        enlace.Url.Scheme.Should().Be("https");
        enlace.Url.AbsolutePath.Should().Be($"/imagenes-cuarentena/{nombre}");
        enlace.Url.Host.Should().Be($"{_cuentaPrivada}.blob.core.windows.net");
        consulta["sp"].Should().Be("cw");
        consulta["sr"].Should().Be("b");
        consulta["spr"].Should().Be("https");
        enlace.VenceEn.Should().Be(_ahora + TimeSpan.FromMinutes(5));
        enlace.Encabezados["x-ms-blob-type"].Should().Be("BlockBlob");
        enlace.Encabezados["Content-Type"].Should().Be("image/jpeg");
        privada.LlavesPedidas.Should().Be(1);
    }

    [Fact]
    public async Task Enlace_Vencido_No_Escribe_CA_032_03()
    {
        using var almacen = CrearAlmacen(out _, out _);

        var enlace = await almacen.CrearEnlaceSubidaAsync(AlmacenBlob.NuevoNombreCuarentena(), "image/png", CancellationToken.None);

        var consulta = HttpUtility.ParseQueryString(enlace.Url.Query);
        var inicio = DateTimeOffset.Parse(consulta["st"]!, System.Globalization.CultureInfo.InvariantCulture);
        var fin = DateTimeOffset.Parse(consulta["se"]!, System.Globalization.CultureInfo.InvariantCulture);
        inicio.Should().Be(_ahora - TimeSpan.FromMinutes(1), "la tolerancia de reloj es de un minuto");
        fin.Should().Be(_ahora + TimeSpan.FromMinutes(5), "el almacenamiento rechaza la firma pasado este instante");
    }

    [Fact]
    public async Task Enlace_Solo_Escribe_Su_Blob_CA_032_04()
    {
        using var almacen = CrearAlmacen(out _, out _);

        var enlace = await almacen.CrearEnlaceSubidaAsync(AlmacenBlob.NuevoNombreCuarentena(), "image/webp", CancellationToken.None);

        var consulta = HttpUtility.ParseQueryString(enlace.Url.Query);
        consulta["sp"].Should().NotContain("r").And.NotContain("d").And.NotContain("l").And.NotContain("a");
        consulta["sr"].Should().Be("b", "la firma cubre un solo blob y no el contenedor");
        consulta["skoid"].Should().NotBeNullOrEmpty("es una SAS de delegación de usuario");
        consulta["sig"].Should().NotBeNullOrEmpty();
        consulta["sktid"].Should().NotBeNullOrEmpty();
        consulta.AllKeys.Should().NotContain("sv_account", "nunca SAS de cuenta");
    }

    [Fact]
    public void Nombre_De_Cuarentena_Es_Hexadecimal_De_128_Bits_Y_No_Se_Repite_CA_032_01()
    {
        var nombres = Enumerable.Range(0, 200).Select(_ => AlmacenBlob.NuevoNombreCuarentena()).ToList();

        nombres.Should().OnlyContain(n => n.Length == 32 && n.All(c => char.IsAsciiHexDigitLower(c)));
        nombres.Distinct().Should().HaveCount(nombres.Count);
    }

    [Fact]
    public async Task La_Llave_De_Delegacion_Se_Reutiliza_Mientras_Quede_Margen_CA_032_01()
    {
        var reloj = new RelojFalso(_ahora);
        using var almacen = CrearAlmacen(out var privada, out _, reloj);

        await almacen.CrearEnlaceSubidaAsync(AlmacenBlob.NuevoNombreCuarentena(), "image/jpeg", CancellationToken.None);
        reloj.Avanzar(TimeSpan.FromHours(10));
        await almacen.CrearEnlaceSubidaAsync(AlmacenBlob.NuevoNombreCuarentena(), "image/jpeg", CancellationToken.None);
        privada.LlavesPedidas.Should().Be(1);

        reloj.Avanzar(TimeSpan.FromHours(13).Add(TimeSpan.FromMinutes(30)));
        await almacen.CrearEnlaceSubidaAsync(AlmacenBlob.NuevoNombreCuarentena(), "image/jpeg", CancellationToken.None);
        privada.LlavesPedidas.Should().Be(2, "a menos de una hora de vencer se pide una llave nueva");
    }

    [Fact]
    public void Firma_Solo_Los_Permisos_Pedidos_CA_032_04()
    {
        var blob = new BlobClient(new Uri($"https://{_cuentaPrivada}.blob.core.windows.net/imagenes-cuarentena/abc"));
        var llave = new LlaveDelegacion(NuevaLlave(_ahora), _cuentaPrivada);

        var url = AlmacenBlob.FirmarBlob(blob, llave, BlobSasPermissions.Read, _ahora);

        HttpUtility.ParseQueryString(url.Query)["sp"].Should().Be("r");
    }

    private static AlmacenBlob CrearAlmacen(out ServicioFalso privada, out ServicioFalso publica, TimeProvider? reloj = null)
    {
        privada = new ServicioFalso(_cuentaPrivada, _ahora);
        publica = new ServicioFalso(_cuentaPublica, _ahora);
        return new AlmacenBlob(privada, publica, reloj ?? new RelojFalso(_ahora));
    }

    private static UserDelegationKey NuevaLlave(DateTimeOffset desde) => BlobsModelFactory.UserDelegationKey(
        "00000000-0000-0000-0000-000000000001",
        "00000000-0000-0000-0000-000000000002",
        desde,
        desde + TimeSpan.FromHours(24),
        "b",
        "2024-11-04",
        Convert.ToBase64String(new byte[32]));

    /// <summary>Servicio de Blob sin red: solo entrega una llave de delegación fija y cuenta cuántas se pidieron.</summary>
    private sealed class ServicioFalso(string cuenta, DateTimeOffset desde) : BlobServiceClient(new Uri($"https://{cuenta}.blob.core.windows.net"))
    {
        public int LlavesPedidas { get; private set; }

        public override Task<Response<UserDelegationKey>> GetUserDelegationKeyAsync(
            DateTimeOffset? startsOn, DateTimeOffset expiresOn, CancellationToken cancellationToken)
        {
            LlavesPedidas++;
            var inicio = startsOn ?? desde;
            return Task.FromResult(Response.FromValue(NuevaLlave(inicio), null!));
        }
    }

    private sealed class RelojFalso(DateTimeOffset inicial) : TimeProvider
    {
        private DateTimeOffset _actual = inicial;

        public void Avanzar(TimeSpan tiempo) => _actual += tiempo;

        public override DateTimeOffset GetUtcNow() => _actual;
    }
}
