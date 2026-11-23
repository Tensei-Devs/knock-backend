using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using AwesomeAssertions;
using Knock.Market.Api.Common.Bandeja;
using Knock.Market.Api.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace Knock.UnitTest.Reviews;

/// <summary>
/// Spec 061 (R-03, CA-05, CA-06): la puerta real de permisos del panel en Mercado pregunta a Identidad con el token de servicio, manda solo <c>sub</c>, <c>sid</c>, <c>pv</c> y el permiso, y falla cerrado ante
/// cualquier cosa distinta de un 200 con <c>permitido = true</c>. Sin red real: el manejador HTTP es de prueba.
/// </summary>
public sealed class Spec061PuertaRealTests
{
    private static readonly string _key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static IConfiguration Config(bool withUrl = true, bool withKey = true) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [EntregaOpinionModerada.IdentityBaseUrlConfigurationKey] = withUrl ? "http://identity.test" : null,
            [EntregaOpinionModerada.IdentityKeyConfigurationKey] = withKey ? _key : null,
        }).Build();

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private static Task<PanelDecision> DecideAsync(Handler handler, IConfiguration? configuration = null, string version = "3") =>
        new IdentityPanelPermissionGate(new Factory(handler), configuration ?? Config(), TimeProvider.System)
            .DecideAsync("cuenta-1", "sesion-1", version, "reviews:moderate", CancellationToken.None);

    [Fact]
    public async Task Identidad_Dice_Permitido_Y_Mercado_Manda_Solo_Los_Claims_Y_El_Permiso_Con_Token_De_Servicio_CA_061_01()
    {
        using var handler = new Handler(_ => Json(HttpStatusCode.OK, "{\"permitido\":true}"));

        (await DecideAsync(handler)).Should().Be(PanelDecision.Allowed);

        handler.Request!.RequestUri!.AbsolutePath.Should().Be("/api/v1/interno/permisos-del-panel");
        using var body = JsonDocument.Parse(handler.Body!);
        body.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("sub", "sid", "pv", "permiso");
        body.RootElement.GetProperty("pv").GetInt32().Should().Be(3);
        var token = new JsonWebTokenHandler().ReadJsonWebToken(handler.Request.Headers.Authorization!.Parameter);
        (token.Issuer, token.Audiences.Single()).Should().Be(("knock-market", "knock-identity"));
        token.Claims.Where(c => c.Type == "permisos").Select(c => c.Value).Should().Equal("panel-permiso");
        handler.Request.Headers.Authorization.Scheme.Should().Be("Bearer");
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "{\"permitido\":false}")]
    [InlineData(HttpStatusCode.OK, "{}")]
    [InlineData(HttpStatusCode.OK, "no es json")]
    [InlineData(HttpStatusCode.Unauthorized, "{\"permitido\":true}")]
    [InlineData(HttpStatusCode.Forbidden, "{\"permitido\":true}")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{\"permitido\":true}")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "{\"permitido\":true}")]
    public async Task Cualquier_Respuesta_Que_No_Sea_Permitido_Verdadero_Se_Deniega_CA_061_05_CA_061_06(HttpStatusCode status, string body)
    {
        using var handler = new Handler(_ => Json(status, body));

        (await DecideAsync(handler)).Should().Be(PanelDecision.Denied);
    }

    [Fact]
    public async Task Red_Caida_Sin_Direccion_Sin_Clave_O_Version_Invalida_Se_Deniega_Sin_Llamar_CA_061_05()
    {
        using var down = new Handler(_ => throw new HttpRequestException("sin red"));
        using var silent = new Handler(_ => Json(HttpStatusCode.OK, "{\"permitido\":true}"));

        (await DecideAsync(down)).Should().Be(PanelDecision.Denied);
        (await DecideAsync(silent, Config(withUrl: false))).Should().Be(PanelDecision.Denied);
        (await DecideAsync(silent, Config(withKey: false))).Should().Be(PanelDecision.Denied);
        (await DecideAsync(silent, version: "no-numero")).Should().Be(PanelDecision.Denied);
        silent.Request.Should().BeNull();
    }

    [Fact]
    public void Mercado_Registra_La_Puerta_Real_Y_No_La_Que_Niega_Todo()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton<IConfiguration>(Config());
        services.AddHttpClient();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPanelPermissionGate, IdentityPanelPermissionGate>();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IPanelPermissionGate>().Should().BeOfType<IdentityPanelPermissionGate>();
    }

    [Fact]
    public void El_Token_De_Servicio_No_Lleva_Mas_Permiso_Que_El_De_La_Consulta()
    {
        var token = EntregaServicio.IssueToken(Convert.FromBase64String(_key), "knock-identity", IdentityPanelPermissionGate.Permission, TimeProvider.System);

        var read = new JsonWebTokenHandler().ReadJsonWebToken(token);

        read.Claims.Count(c => c.Type == "permisos").Should().Be(1);
        new AuthenticationHeaderValue("Bearer", token).Scheme.Should().Be("Bearer");
    }
}
