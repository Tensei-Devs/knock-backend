using System.Net;

namespace Knock.UnitTest.Requests;

/// <summary>Lo que una entrega de la bandeja de Mercado envió por HTTP.</summary>
internal sealed record EnvioCapturado(string Path, string Body, string Token);

/// <summary>Destino HTTP de mentira para las entregas de la bandeja: guarda cada envío y responde lo que se le indique. También hace de fábrica de clientes.</summary>
internal sealed class CapturaDeEntregas(HttpStatusCode answer) : HttpMessageHandler, IHttpClientFactory
{
    public List<EnvioCapturado> Sent { get; } = [];

    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Sent.Add(new EnvioCapturado(request.RequestUri!.AbsolutePath, await request.Content!.ReadAsStringAsync(cancellationToken), request.Headers.Authorization!.Parameter!));
        return new HttpResponseMessage(answer);
    }
}
