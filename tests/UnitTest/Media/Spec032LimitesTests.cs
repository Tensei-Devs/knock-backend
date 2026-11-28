using AwesomeAssertions;
using Knock.Market.Api.Modules.Media.Services;
using StackExchange.Redis;
using Xunit;

namespace Knock.UnitTest.Media;

/// <summary>Spec 032, R-14 y CA-18: límites horarios por persona compartidos entre réplicas y máximo de pendientes.</summary>
public sealed class Spec032LimitesTests
{
    private const string _sub = "persona-1";

    [Fact]
    public async Task Limite_Compartido_Entre_Replicas_CA_032_18()
    {
        var reloj = new RelojFijo();
        var compartido = new ContadorVentanaMemoria(reloj);
        var replicaA = new LimitesImagen(compartido, new ContadorVentanaMemoria(reloj));
        var replicaB = new LimitesImagen(compartido, new ContadorVentanaMemoria(reloj));

        for (var i = 1; i <= 20; i++)
        {
            var replica = i % 2 == 0 ? replicaA : replicaB;
            (await replica.ConsumirAsync(LimiteImagen.Subidas, _sub, CancellationToken.None)).Permitido.Should().BeTrue($"el intento {i} está dentro del límite");
        }

        var vigesimaPrimera = await replicaA.ConsumirAsync(LimiteImagen.Subidas, _sub, CancellationToken.None);

        vigesimaPrimera.Permitido.Should().BeFalse();
        vigesimaPrimera.ReintentarEn.Should().BeGreaterThan(TimeSpan.Zero).And.BeLessThanOrEqualTo(TimeSpan.FromHours(1));
    }

    [Fact]
    public async Task Confirmaciones_Permiten_30_Por_Hora_CA_032_18()
    {
        var reloj = new RelojFijo();
        var limites = new LimitesImagen(new ContadorVentanaMemoria(reloj), new ContadorVentanaMemoria(reloj));

        for (var i = 0; i < 30; i++)
        {
            (await limites.ConsumirAsync(LimiteImagen.Confirmaciones, _sub, CancellationToken.None)).Permitido.Should().BeTrue();
        }

        (await limites.ConsumirAsync(LimiteImagen.Confirmaciones, _sub, CancellationToken.None)).Permitido.Should().BeFalse();
    }

    [Fact]
    public async Task Los_Contadores_Son_Independientes_Por_Persona_Y_Por_Tipo_CA_032_18()
    {
        var reloj = new RelojFijo();
        var limites = new LimitesImagen(new ContadorVentanaMemoria(reloj), new ContadorVentanaMemoria(reloj));
        for (var i = 0; i < 21; i++)
        {
            await limites.ConsumirAsync(LimiteImagen.Subidas, _sub, CancellationToken.None);
        }

        (await limites.ConsumirAsync(LimiteImagen.Subidas, "persona-2", CancellationToken.None)).Permitido.Should().BeTrue();
        (await limites.ConsumirAsync(LimiteImagen.Confirmaciones, _sub, CancellationToken.None)).Permitido.Should().BeTrue();
    }

    [Fact]
    public async Task La_Ventana_Se_Reinicia_A_La_Hora_CA_032_18()
    {
        var reloj = new RelojFijo();
        var limites = new LimitesImagen(new ContadorVentanaMemoria(reloj), new ContadorVentanaMemoria(reloj));
        for (var i = 0; i < 21; i++)
        {
            await limites.ConsumirAsync(LimiteImagen.Subidas, _sub, CancellationToken.None);
        }

        reloj.Avanzar(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(1)));

        (await limites.ConsumirAsync(LimiteImagen.Subidas, _sub, CancellationToken.None)).Permitido.Should().BeTrue();
    }

    [Fact]
    public async Task Si_Redis_Falla_Se_Usa_El_Limite_Por_Instancia_CA_032_18()
    {
        var reloj = new RelojFijo();
        var limites = new LimitesImagen(new ContadorCaido(), new ContadorVentanaMemoria(reloj));

        for (var i = 0; i < 20; i++)
        {
            (await limites.ConsumirAsync(LimiteImagen.Subidas, _sub, CancellationToken.None)).Permitido.Should().BeTrue();
        }

        (await limites.ConsumirAsync(LimiteImagen.Subidas, _sub, CancellationToken.None)).Permitido.Should().BeFalse("no se abre el paso sin límite");
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(10, false)]
    [InlineData(11, false)]
    public void Maximo_De_10_Pendientes_Vigentes_CA_032_18(int pendientes, bool permitido) =>
        LimitesImagen.PermiteNuevaSubida(pendientes).Should().Be(permitido);

    [Fact]
    public void Las_Llaves_De_Redis_Son_Las_De_La_Spec_CA_032_18()
    {
        LimitesImagen.Llave(LimiteImagen.Subidas, _sub).Should().Be("img:subidas:persona-1");
        LimitesImagen.Llave(LimiteImagen.Confirmaciones, _sub).Should().Be("img:confirmaciones:persona-1");
    }

    private sealed class ContadorCaido : IContadorVentana
    {
        public Task<(long Cuenta, TimeSpan Restante)> GolpearAsync(string llave, TimeSpan ventana, CancellationToken cancellationToken) =>
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "sin Redis");
    }

    private sealed class RelojFijo : TimeProvider
    {
        private DateTimeOffset _actual = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

        public void Avanzar(TimeSpan tiempo) => _actual += tiempo;

        public override DateTimeOffset GetUtcNow() => _actual;
    }
}
