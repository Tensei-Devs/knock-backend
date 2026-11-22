using System.Web;
using AwesomeAssertions;
using Knock.Market.Api.Modules.Media.Data;
using Knock.Market.Api.Modules.Media.Domain;
using Knock.Market.Api.Modules.Media.Options;
using Knock.Market.Api.Modules.Media.Services;
using Xunit;

namespace Knock.UnitTest.Media;

/// <summary>Spec 032, R-16 y CA-20: enlaces de lectura privados con motivo cerrado y registro de acceso.</summary>
public sealed class Spec032EnlacesPrivadosTests : IDisposable
{
    private static readonly DateTimeOffset _ahora = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
    private const string _sub = "persona-1";

    private AlmacenBlob? _almacen;

    /// <summary>El almacén vive lo que dura la prueba y se libera en <see cref="Dispose"/>.</summary>
    public void Dispose() => _almacen?.Dispose();

    private (EnlacesPrivados Servicio, AccesosFalso Accesos) Crear()
    {
        var reloj = new Spec032SasTests.RelojFalso(_ahora);
        _almacen = new AlmacenBlob(
            new Spec032SasTests.ServicioFalso("stknockprivada", _ahora), new Spec032SasTests.ServicioFalso("stknockpublica", _ahora), reloj);
        var almacen = _almacen;
        var accesos = new AccesosFalso();
        var llave = Convert.ToBase64String(new byte[32]);
        var huella = new HuellaSubHmac(Microsoft.Extensions.Options.Options.Create(new ImagenesOptions { LlaveBusqueda = llave }));
        return (new EnlacesPrivados(almacen, accesos, huella, reloj), accesos);
    }

    [Fact]
    public async Task Lectura_Firmada_Con_Registro_CA_032_20()
    {
        var (servicio, accesos) = Crear();

        var enlace = await servicio.CrearLecturaAsync(new BlobPrivado("verificaciones", "doc-1"), MotivoAcceso.VerificacionRevision, _sub, CancellationToken.None);

        var consulta = HttpUtility.ParseQueryString(enlace.Url.Query);
        consulta["sp"].Should().Be("r");
        consulta["sr"].Should().Be("b");
        consulta["spr"].Should().Be("https");
        consulta["rscc"].Should().Be("no-store");
        consulta["rscd"].Should().Be("inline");
        enlace.VenceEn.Should().Be(_ahora.AddMinutes(5));
        var registro = accesos.Registros.Should().ContainSingle().Subject;
        registro.Motivo.Should().Be("verificacion_revision");
        registro.SubHmac.Should().HaveLength(64).And.NotContain(_sub);
        registro.BlobId.Should().NotContain("doc-1");
    }

    [Theory]
    [InlineData("imagenes-cuarentena")]
    [InlineData("imagenes-publicas")]
    [InlineData("otro")]
    public async Task Rechaza_Contenedores_No_Privados_CA_032_20(string contenedor)
    {
        var (servicio, accesos) = Crear();

        var accion = () => servicio.CrearLecturaAsync(new BlobPrivado(contenedor, "x"), MotivoAcceso.VerificacionDueno, _sub, CancellationToken.None);

        await accion.Should().ThrowAsync<ArgumentException>();
        accesos.Registros.Should().BeEmpty();
    }

    [Fact]
    public async Task Rechaza_Motivo_Fuera_De_La_Lista_CA_032_20()
    {
        var (servicio, _) = Crear();

        var accion = () => servicio.CrearLecturaAsync(new BlobPrivado("verificaciones", "x"), (MotivoAcceso)99, _sub, CancellationToken.None);

        await accion.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private sealed class AccesosFalso : IAccesosPrivadosStore
    {
        public List<AccesoPrivado> Registros { get; } = [];

        public Task InsertAsync(AccesoPrivado acceso, CancellationToken cancellationToken)
        {
            Registros.Add(acceso);
            return Task.CompletedTask;
        }
    }
}
