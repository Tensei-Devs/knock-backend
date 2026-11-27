using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Knock.UnitTest.Requests;

/// <summary>Lo que una entrega de la bandeja de Mercado envió por HTTP.</summary>
internal sealed record EnvioCapturado(string Path, string Body, string Token);

/// <summary>Destino HTTP de mentira para las entregas de la bandeja: guarda cada envío y responde lo que se le indique. También hace de fábrica de clientes.</summary>
internal sealed class CapturaDeEntregas(HttpStatusCode answer) : HttpMessageHandler, IHttpClientFactory
{
    private static readonly string _key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    public List<EnvioCapturado> Sent { get; } = [];

    /// <summary>La configuración de Mercado hacia Mensajería; sin <paramref name="configured"/> faltan la dirección y la clave.</summary>
    public static IConfiguration Messaging(bool configured = true)
    {
        var settings = new Dictionary<string, string?>();
        if (configured)
        {
            settings["Services:Messaging:BaseUrl"] = "http://messaging.test";
            settings["Services:Messaging:InternalKey"] = _key;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    public static List<(string, string)> Claims(string token) => [.. new JsonWebTokenHandler().ReadJsonWebToken(token).Claims.Select(c => (c.Type, c.Value))];

    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Sent.Add(new EnvioCapturado(request.RequestUri!.AbsolutePath, await request.Content!.ReadAsStringAsync(cancellationToken), request.Headers.Authorization!.Parameter!));
        return new HttpResponseMessage(answer);
    }
}
