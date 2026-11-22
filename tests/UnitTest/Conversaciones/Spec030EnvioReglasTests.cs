extern alias messaging;

using System.Net;
using System.Text;
using AwesomeAssertions;
using Knock.Contracts.Conversations;
using Knock.Contracts.Moderation;
using messaging::Knock.Messaging.Api.Modules.Conversaciones.Data;
using messaging::Knock.Messaging.Api.Modules.Conversaciones.Domain;
using messaging::Knock.Messaging.Api.Modules.Conversaciones.Dtos;
using messaging::Knock.Messaging.Api.Modules.Conversaciones.Services;
using messaging::Knock.Messaging.Api.Modules.Conversaciones.Validators;
using Xunit;

namespace Knock.UnitTest.Conversaciones;

/// <summary>Spec 030, T-06 (KNOCK-73): las reglas puras del envío de mensajes, su validador, el cliente de permiso de Mercado y la moderación por omisión. Solo datos ficticios.</summary>
public sealed class Spec030EnvioReglasTests
{
    private static readonly EnviarMensajeRequestValidator _validator = new();

    // Sin conversacionId la caché no se toca: estas pruebas no necesitan Redis.
    private static readonly PermisoCache _noCache = new(null!);

    [Theory]
    [InlineData("a", "a")]
    [InlineData("  hola  ", "hola")]
    [InlineData("línea uno\nlínea dos", "línea uno\nlínea dos")]
    [InlineData("emoji 🔧 incluido", "emoji 🔧 incluido")]
    public void Limpiar_Acepta_Texto_Valido_CA_030_02(string input, string expected) =>
        ReglasDeMensaje.Limpiar(input).Should().Be(expected);

    [Fact]
    public void Limpiar_Acepta_Exactamente_1000_Y_Rechaza_1001_CA_030_02()
    {
        ReglasDeMensaje.Limpiar(new string('x', 1_000)).Should().HaveLength(1_000);
        ReglasDeMensaje.Limpiar(new string('x', 1_001)).Should().BeNull();
        ReglasDeMensaje.Limpiar(" " + new string('x', 1_000) + " ").Should().HaveLength(1_000, "el recorte va antes de contar");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    [InlineData("hola\u0007")]
    [InlineData("hola\u0000mundo")]
    [InlineData("tab\taqui")]
    [InlineData("retorno\rde carro")]
    public void Limpiar_Rechaza_Vacio_O_Con_Caracteres_De_Control_CA_030_02(string? input) =>
        ReglasDeMensaje.Limpiar(input).Should().BeNull();

    [Fact]
    public void Limpiar_No_Deja_Pasar_Texto_Que_Excede_Los_Bytes_Del_Cuerpo_CA_030_02()
    {
        var huge = new string('é', 3_000);
        Encoding.UTF8.GetByteCount(huge).Should().BeGreaterThan(ReglasDeMensaje.MaxBytesDelTexto);
        ReglasDeMensaje.Limpiar(huge).Should().BeNull();
    }

    [Fact]
    public void Resumir_Recorta_A_80_Y_No_Parte_Un_Par_Sustituto_CA_030_01()
    {
        ReglasDeMensaje.Resumir("corto").Should().Be("corto");
        ReglasDeMensaje.Resumir(new string('a', 80)).Should().HaveLength(80);
        ReglasDeMensaje.Resumir(new string('a', 81)).Should().HaveLength(80);
        var split = new string('a', 79) + "🔧🔧";
        var summary = ReglasDeMensaje.Resumir(split);
        summary.Should().Be(new string('a', 79), "el emoji que cae en el corte se descarta completo");
        char.IsHighSurrogate(summary[^1]).Should().BeFalse();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("hola", true)]
    [InlineData("hola\u0007", false)]
    public void El_Validador_Usa_La_Misma_Regla_CA_030_02(string? text, bool valid) =>
        _validator.Validate(new EnviarMensajeRequest(text)).IsValid.Should().Be(valid);

    [Fact]
    public void El_Validador_No_Repite_El_Texto_En_El_Motivo_CA_030_02()
    {
        var text = new string('z', 1_001);

        var result = _validator.Validate(new EnviarMensajeRequest(text));

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Texto");
        result.Errors[0].ErrorMessage.Should().NotContain(text);
    }

    [Fact]
    public async Task La_Moderacion_Por_Omision_Falla_Cerrado_CA_030_07()
    {
        var moderator = new ModeradorNoDisponible();

        (await moderator.ReviewTextAsync("hola", CancellationToken.None)).Outcome.Should().Be(ModerationOutcome.Unavailable);
        (await moderator.ReviewImageAsync(new byte[] { 1 }, CancellationToken.None)).Outcome.Should().Be(ModerationOutcome.Unavailable);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, EstadoPermisoConversacion.NoEncontrado)]
    [InlineData(HttpStatusCode.Forbidden, EstadoPermisoConversacion.NoEncontrado)]
    [InlineData(HttpStatusCode.Unauthorized, EstadoPermisoConversacion.NoEncontrado)]
    [InlineData(HttpStatusCode.BadRequest, EstadoPermisoConversacion.NoEncontrado)]
    [InlineData(HttpStatusCode.InternalServerError, EstadoPermisoConversacion.NoDisponible)]
    [InlineData(HttpStatusCode.BadGateway, EstadoPermisoConversacion.NoDisponible)]
    [InlineData(HttpStatusCode.TooManyRequests, EstadoPermisoConversacion.NoDisponible)]
    public async Task El_Cliente_De_Permiso_Traduce_La_Respuesta_De_Mercado_CA_030_09(HttpStatusCode status, EstadoPermisoConversacion expected)
    {
        using var http = new StubMarket(_ => new HttpResponseMessage(status));

        var result = await new MarketPermisoDeConversacion(http.Client, _noCache).ConsultarAsync("sol", "tec", null, "sub", "token", CancellationToken.None);

        result.Estado.Should().Be(expected);
        result.Permiso.Should().BeNull();
    }

    [Fact]
    public async Task El_Cliente_De_Permiso_Lee_El_Permiso_Y_Reenvia_Sesion_Y_Tecnico_CA_030_09()
    {
        HttpRequestMessage? seen = null;
        using var http = new StubMarket(request =>
        {
            seen = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"solicitudId":"sol","tecnicoId":"tec","clienteId":"c","tecnicoUsuarioId":"t","rol":"cliente","abierta":true,"elegido":false,"servicio":"Plomería","coloniaNombre":"Centro","tecnicoPublico":{"nombre":"Ana"}}""", Encoding.UTF8, "application/json"),
            };
        });

        var result = await new MarketPermisoDeConversacion(http.Client, _noCache).ConsultarAsync("sol 1", "tec/1", null, "sub", "token-de-prueba", CancellationToken.None);

        result.Estado.Should().Be(EstadoPermisoConversacion.Concedido);
        result.Permiso!.Abierta.Should().BeTrue();
        result.Permiso.TecnicoPublico.Nombre.Should().Be("Ana");
        seen!.RequestUri!.ToString().Should().Be("http://market.invalid/api/v1/solicitudes/sol%201/conversacion?tecnicoId=tec%2F1");
        seen.Headers.Authorization!.ToString().Should().Be("Bearer token-de-prueba");
    }

    [Fact]
    public async Task El_Cliente_De_Permiso_Sin_Tecnico_No_Manda_La_Consulta_CA_030_09()
    {
        HttpRequestMessage? seen = null;
        using var http = new StubMarket(request =>
        {
            seen = request;
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        await new MarketPermisoDeConversacion(http.Client, _noCache).ConsultarAsync("sol", null, null, "sub", "token", CancellationToken.None);

        seen!.RequestUri!.Query.Should().BeEmpty();
    }

    [Fact]
    public async Task El_Cliente_De_Permiso_Falla_Cerrado_Sin_Red_O_Con_Cuerpo_Ilegible_CA_030_09()
    {
        using var down = new StubMarket(_ => throw new HttpRequestException("sin red"));
        using var garbage = new StubMarket(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("no es json", Encoding.UTF8, "application/json") });
        using var empty = new StubMarket(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null", Encoding.UTF8, "application/json") });

        (await new MarketPermisoDeConversacion(down.Client, _noCache).ConsultarAsync("sol", "tec", null, "sub", "t", CancellationToken.None)).Estado.Should().Be(EstadoPermisoConversacion.NoDisponible);
        (await new MarketPermisoDeConversacion(garbage.Client, _noCache).ConsultarAsync("sol", "tec", null, "sub", "t", CancellationToken.None)).Estado.Should().Be(EstadoPermisoConversacion.NoDisponible);
        (await new MarketPermisoDeConversacion(empty.Client, _noCache).ConsultarAsync("sol", "tec", null, "sub", "t", CancellationToken.None)).Estado.Should().Be(EstadoPermisoConversacion.NoDisponible);
    }

    [Fact]
    public async Task La_Cancelacion_De_Quien_Pide_Se_Propaga_CA_030_09()
    {
        using var http = new StubMarket(_ => throw new TaskCanceledException());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var act = () => new MarketPermisoDeConversacion(http.Client, _noCache).ConsultarAsync("sol", "tec", null, "sub", "t", cancelled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>Un Mercado de mentira: un <see cref="HttpClient"/> propio sobre un manejador que contesta lo dado; se libera junto con la prueba.</summary>
    private sealed class StubMarket : IDisposable
    {
        private readonly StubHandler _handler;

        public StubMarket(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _handler = new StubHandler(respond);
            Client = new HttpClient(_handler) { BaseAddress = new Uri("http://market.invalid/") };
        }

        public HttpClient Client { get; }

        public void Dispose()
        {
            Client.Dispose();
            _handler.Dispose();
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
