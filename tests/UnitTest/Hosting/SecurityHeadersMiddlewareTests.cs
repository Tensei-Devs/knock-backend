using AwesomeAssertions;
using Knock.Hosting.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Knock.UnitTest.Hosting;

/// <summary>
/// Spec 055 (T-17), autorrevisión de seguridad: el único <c>SecurityHeadersMiddleware</c> conserva lo que hacían las cuatro copias (BUG-002, BUG-003, spec 009 R-02)
/// y sus opciones no abren nada por defecto.
/// </summary>
public sealed class SecurityHeadersMiddlewareTests
{
    private const string _hsts = "max-age=31536000; includeSubDomains";

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Knock.Test";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    /// <summary>Respuesta que ejecuta las llamadas de <c>OnStarting</c> como lo hace el servidor justo antes de enviar los encabezados.</summary>
    private sealed class FiringResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _callbacks = [];

        public override void OnStarting(Func<object, Task> callback, object state) => _callbacks.Add((callback, state));

        public async Task FireAsync()
        {
            foreach (var (callback, state) in _callbacks)
            {
                await callback(state);
            }
        }
    }

    private static async Task<IHeaderDictionary> SendAsync(string environment, SecurityHeadersOptions options, string path, Action<IHeaderDictionary>? endpoint = null)
    {
        var feature = new FiringResponseFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(feature);
        context.Request.Path = path;
        var middleware = new SecurityHeadersMiddleware(
            ctx =>
            {
                ctx.Response.Headers.Server = "Kestrel";
                ctx.Response.Headers["X-Powered-By"] = "ASP.NET";
                endpoint?.Invoke(ctx.Response.Headers);
                return Task.CompletedTask;
            },
            new FakeEnvironment(environment),
            options);

        await middleware.InvokeAsync(context);
        await feature.FireAsync();
        return context.Response.Headers;
    }

    [Theory]
    [InlineData("Production", "/api/v1/x")]
    [InlineData("Development", "/api/v1/x")]
    [InlineData("Production", "/scalar/index.html")]
    public async Task FixedHeaders_AlwaysSet_AndLeakyHeadersRemoved_TC_055_SEC_04(string environment, string path)
    {
        var headers = await SendAsync(environment, new SecurityHeadersOptions { RelaxContentSecurityPolicyForApiDocsInDevelopment = true }, path);

        headers.XContentTypeOptions.ToString().Should().Be("nosniff");
        headers.XFrameOptions.ToString().Should().Be("DENY");
        headers["Referrer-Policy"].ToString().Should().Be("no-referrer");
        headers.ContentSecurityPolicy.ToString().Should().Be(SecurityHeadersMiddleware.ContentSecurityPolicy);
        headers.CacheControl.ToString().Should().Be("no-store");
        headers.ContainsKey("Server").Should().BeFalse();
        headers.ContainsKey("X-Powered-By").Should().BeFalse();
    }

    [Fact]
    public async Task EndpointCacheControl_IsRespected_TC_055_SEC_05()
    {
        var headers = await SendAsync("Production", new SecurityHeadersOptions(), "/api/v1/x", h => h.CacheControl = "private, max-age=60");

        headers.CacheControl.ToString().Should().Be("private, max-age=60");
    }

    [Fact]
    public async Task Hsts_IsSentOutsideDevelopmentOnly_AndOnlyWhenConfigured_TC_055_SEC_06()
    {
        var production = await SendAsync("Production", new SecurityHeadersOptions { StrictTransportSecurity = _hsts }, "/health");
        var development = await SendAsync("Development", new SecurityHeadersOptions { StrictTransportSecurity = _hsts }, "/health");
        var services = await SendAsync("Production", new SecurityHeadersOptions(), "/health");

        production.StrictTransportSecurity.ToString().Should().Be(_hsts);
        development.ContainsKey("Strict-Transport-Security").Should().BeFalse();
        services.ContainsKey("Strict-Transport-Security").Should().BeFalse();
    }

    [Fact]
    public async Task Hsts_TheDevelopmentOption_ChangesNothingOutsideDevelopment_CA_BUG_002_E02()
    {
        var options = new SecurityHeadersOptions { StrictTransportSecurity = _hsts, SendStrictTransportSecurityInDevelopment = true };

        var production = await SendAsync("Production", options, "/health");

        production.StrictTransportSecurity.ToString().Should().Be(_hsts);
    }

    [Theory]
    [InlineData("Development", true, "/scalar/index.html", false)]
    [InlineData("Development", true, "/openapi/v1.json", false)]
    [InlineData("Development", true, "/api/v1/x", true)]
    [InlineData("Development", false, "/scalar/index.html", true)]
    [InlineData("Production", true, "/scalar/index.html", true)]
    [InlineData("Production", true, "/openapi/v1.json", true)]
    public async Task ContentSecurityPolicy_IsRelaxedOnlyForApiDocsInDevelopmentWhenAsked_TC_055_SEC_07(string environment, bool relax, string path, bool expectsCsp)
    {
        var headers = await SendAsync(environment, new SecurityHeadersOptions { RelaxContentSecurityPolicyForApiDocsInDevelopment = relax }, path);

        headers.ContainsKey("Content-Security-Policy").Should().Be(expectsCsp);
    }
}
