using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;

namespace Knock.IntegrationTest.Support;

/// <summary>Un cliente de SignalR sobre el servidor de pruebas en memoria de Mensajería, con el token en la consulta (<c>access_token</c>), como lo hace la app.</summary>
internal static class ChatHubClient
{
    public static HubConnection Connect(TestServer server, string token)
    {
        ArgumentNullException.ThrowIfNull(server);
        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "hubs/chat"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                options.Transports = HttpTransportType.ServerSentEvents;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();
    }
}
