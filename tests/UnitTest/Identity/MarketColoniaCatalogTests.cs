using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AwesomeAssertions;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.Usuarios;
using Knock.Identity.Api.Modules.Usuarios.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 017, R-06 y R-11: el cliente del catálogo de colonias de Mercado reenvía la sesión y toda falla sale como «Mercado no responde».</summary>
public class MarketColoniaCatalogTests
{
    private const string _bearer = "Bearer token-de-prueba";

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(answer(request));
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpContextAccessor WithSession(string? authorization = _bearer, string? correlationId = null)
    {
        var context = new DefaultHttpContext();
        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }

        if (correlationId is not null)
        {
            context.Items[CorrelationId.ItemKey] = correlationId;
        }

        return new HttpContextAccessor { HttpContext = context };
    }

    /// <summary>Un catálogo sobre un <see cref="HttpClient"/> propio: se libera junto con la prueba.</summary>
    private sealed class Rig : IDisposable
    {
        private readonly HttpClient _http;

        public Rig(Func<HttpRequestMessage, HttpResponseMessage> answer, HttpContextAccessor? accessor = null, bool withBaseAddress = true)
        {
            Handler = new StubHandler(answer);
            _http = new HttpClient(Handler);
            if (withBaseAddress)
            {
                _http.BaseAddress = new Uri("http://knock-market/");
            }

            Catalog = new MarketColoniaCatalog(_http, accessor ?? WithSession());
        }

        public StubHandler Handler { get; }

        public MarketColoniaCatalog Catalog { get; }

        public void Dispose() => _http.Dispose();
    }

    private static ServiceProvider Provider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration);
        services.AddUsuariosModule();
        return services.BuildServiceProvider(validateScopes: false);
    }

    [Theory]
    [InlineData("""{"existe":true}""", true)]
    [InlineData("""{"existe":false}""", false)]
    public async Task Answer_IsTheExisteFlagOfMarket_CA_017_07(string body, bool expected)
    {
        using var rig = new Rig(_ => Json(body));

        (await rig.Catalog.ExistsAsync("Centro", CancellationToken.None)).Should().Be(expected);
    }

    [Fact]
    public async Task Request_GoesToTheExistsRouteWithTheNameEscapedAndTheSessionForwarded_CA_017_07()
    {
        using var rig = new Rig(_ => Json("""{"existe":true}"""), WithSession(_bearer, "corr-123"));

        await rig.Catalog.ExistsAsync("Cañada & Co/ñ", CancellationToken.None);

        var request = rig.Handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.RequestUri!.AbsoluteUri.Should().Be("http://knock-market/api/v1/colonias/existe?nombre=Ca%C3%B1ada%20%26%20Co%2F%C3%B1");
        request.Headers.Authorization.Should().Be(AuthenticationHeaderValue.Parse(_bearer));
        request.Headers.GetValues(CorrelationId.HeaderName).Should().ContainSingle().Which.Should().Be("corr-123");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task AnyErrorStatusOfMarket_IsMarketUnavailable_CA_017_12(HttpStatusCode status)
    {
        using var rig = new Rig(_ => Json("""{"existe":true}""", status));

        var act = () => rig.Catalog.ExistsAsync("Centro", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>("un error de Mercado nunca se toma por «la colonia existe»");
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"existe":null}""")]
    [InlineData("""{"existe":"si"}""")]
    [InlineData("""null""")]
    [InlineData("""no es json""")]
    [InlineData("")]
    public async Task BodyThatIsNotUnderstood_IsMarketUnavailable_CA_017_12(string body)
    {
        using var rig = new Rig(_ => Json(body));

        var act = () => rig.Catalog.ExistsAsync("Centro", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task AnswerThatIsNotJson_IsMarketUnavailable_CA_017_12()
    {
        using var rig = new Rig(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html/>", Encoding.UTF8, "text/html") });

        var act = () => rig.Catalog.ExistsAsync("Centro", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task WithoutASessionToForward_FailsClosedWithoutCallingMarket_CA_017_12()
    {
        using var rig = new Rig(_ => Json("""{"existe":true}"""), WithSession(authorization: null));

        var act = () => rig.Catalog.ExistsAsync("Centro", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        rig.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task WithoutAnHttpContext_FailsClosedWithoutCallingMarket_CA_017_12()
    {
        using var rig = new Rig(_ => Json("""{"existe":true}"""), new HttpContextAccessor());

        var act = () => rig.Catalog.ExistsAsync("Centro", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        rig.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task WithoutTheMarketAddress_FailsClosedWithoutCallingMarket_CA_017_12()
    {
        using var rig = new Rig(_ => Json("""{"existe":true}"""), withBaseAddress: false);

        var act = () => rig.Catalog.ExistsAsync("Centro", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        rig.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ATimeoutOfMarket_SurfacesAsACancellationTheProfileTurnsInto503_CA_017_12()
    {
        using var rig = new Rig(_ => throw new TaskCanceledException("synthetic timeout"));

        var act = () => rig.Catalog.ExistsAsync("Centro", CancellationToken.None);

        await act.Should().ThrowAsync<TaskCanceledException>("ProfileService la trata como «Mercado no responde» mientras la petición no se haya cancelado");
    }

    [Fact]
    public async Task WithoutConfiguration_TheRegisteredCatalogFailsClosed_CA_017_12()
    {
        var provider = Provider([]);
        await using var scope = provider.CreateAsyncScope();

        var act = () => scope.ServiceProvider.GetRequiredService<Knock.Contracts.Colonias.IColoniaCatalog>().ExistsAsync("Centro", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>("sin Services:Market:BaseUrl el perfil con colonia responde 503, como antes del endpoint");
    }

    [Fact]
    public void ConfiguredAddress_IsUsedWithItsTimeout_CA_017_07()
    {
        var provider = Provider(new() { ["Services:Market:BaseUrl"] = "http://knock-market", ["Services:Market:TimeoutSeconds"] = "5" });

        var options = provider.GetRequiredService<IOptions<MarketCatalogOptions>>().Value;

        options.BaseUrl.Should().Be("http://knock-market");
        options.TimeoutSeconds.Should().Be(5);
    }

    [Theory]
    [InlineData("ftp://knock-market")]
    [InlineData("knock-market")]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://knock-market/con-ruta")]
    [InlineData("http://knock-market/?x=1")]
    [InlineData("http://knock-market/#fragmento")]
    public void AddressThatIsNotAPlainHttpHost_IsRejectedWhenTheOptionsAreRead_CA_017_12(string address)
    {
        var provider = Provider(new() { ["Services:Market:BaseUrl"] = address });

        var act = () => provider.GetRequiredService<IOptions<MarketCatalogOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("31")]
    public void TimeoutOutOfRange_IsRejectedWhenTheOptionsAreRead_CA_017_12(string seconds)
    {
        var provider = Provider(new() { ["Services:Market:BaseUrl"] = "http://knock-market", ["Services:Market:TimeoutSeconds"] = seconds });

        var act = () => provider.GetRequiredService<IOptions<MarketCatalogOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }
}
