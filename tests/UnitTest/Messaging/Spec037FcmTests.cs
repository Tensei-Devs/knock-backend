extern alias messaging;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using AvisosModuleExtensions = messaging::Knock.Messaging.Api.Modules.Avisos.AvisosModuleExtensions;
using FcmAccessToken = messaging::Knock.Messaging.Api.Modules.Avisos.Push.FcmAccessToken;
using FcmOptions = messaging::Knock.Messaging.Api.Modules.Avisos.Push.FcmOptions;
using FcmPushSender = messaging::Knock.Messaging.Api.Modules.Avisos.Push.FcmPushSender;
using IPushSender = messaging::Knock.Messaging.Api.Modules.Avisos.Push.IPushSender;
using NoOpPushSender = messaging::Knock.Messaging.Api.Modules.Avisos.Push.NoOpPushSender;
using PushResult = messaging::Knock.Messaging.Api.Modules.Avisos.Push.PushResult;

namespace Knock.UnitTest.Messaging;

/// <summary>Cliente FCM detrás del interruptor <c>Notificaciones:PushHabilitado</c> (spec 037 R-05 y R-10). La llave se genera en cada ejecución.</summary>
public sealed class Spec037FcmTests
{
    private static readonly IReadOnlyDictionary<string, string> _data = new Dictionary<string, string> { ["tipo"] = "mensajes", ["v"] = "1" };

    [Fact]
    public void Switch_Off_Keeps_NoOp_Sender_CA_037_10()
    {
        using var provider = Build(new Dictionary<string, string?>());

        provider.GetRequiredService<IPushSender>().Should().BeOfType<NoOpPushSender>();
    }

    [Fact]
    public void Switch_Off_Ignores_Missing_Fcm_Settings_CA_037_10()
    {
        using var provider = Build(new Dictionary<string, string?> { ["Notificaciones:PushHabilitado"] = "false" });

        provider.GetRequiredService<IPushSender>().Should().BeOfType<NoOpPushSender>();
    }

    [Fact]
    public void Switch_On_Without_Project_Or_Account_Fails_Closed_CA_037_10()
    {
        using var provider = Build(new Dictionary<string, string?> { ["Notificaciones:PushHabilitado"] = "true" });

        var act = () => provider.GetRequiredService<IPushSender>();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Switch_On_With_Malformed_Account_Fails_Closed_Without_Echoing_It_CA_037_10()
    {
        var secret = Guid.NewGuid().ToString("N");
        using var provider = Build(new Dictionary<string, string?>
        {
            ["Notificaciones:PushHabilitado"] = "true",
            ["Notificaciones:Fcm:ProjectId"] = "p",
            ["Notificaciones:Fcm:ServiceAccount"] = secret,
        });

        var act = () => provider.GetRequiredService<IPushSender>();

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().NotContain(secret);
    }

    [Fact]
    public void Switch_On_With_Valid_Settings_Uses_Fcm_Sender_CA_037_10()
    {
        using var provider = Build(ValidSettings());

        provider.GetRequiredService<IPushSender>().Should().BeOfType<FcmPushSender>();
    }

    [Fact]
    public async Task Send_Success_Posts_Data_Only_High_Priority_Message_CA_037_10()
    {
        using var rig = new Rig(new Handler(HttpStatusCode.OK));
        var handler = rig.Handler;
        var sender = rig.Sender;

        var result = await sender.SendAsync("device-token", _data, CancellationToken.None);

        result.Should().Be(PushResult.Sent);
        handler.FcmRequests.Should().ContainSingle();
        handler.LastFcmBody.Should().Contain("\"HIGH\"").And.NotContain("\"notification\"");
        handler.LastFcmAuthorization.Should().Be("Bearer access");
    }

    [Fact]
    public async Task Send_NotFound_Reports_Invalid_Token_CA_037_10()
    {
        using var rig = new Rig(new Handler(HttpStatusCode.NotFound));
        var sender = rig.Sender;

        (await sender.SendAsync("t", _data, CancellationToken.None)).Should().Be(PushResult.InvalidToken);
    }

    [Fact]
    public async Task Send_ServerError_Reports_Failed_CA_037_10()
    {
        using var rig = new Rig(new Handler(HttpStatusCode.InternalServerError));
        var sender = rig.Sender;

        (await sender.SendAsync("t", _data, CancellationToken.None)).Should().Be(PushResult.Failed);
    }

    [Fact]
    public async Task Send_Without_Access_Token_Reports_Failed_And_Never_Calls_Fcm_CA_037_10()
    {
        using var rig = new Rig(new Handler(HttpStatusCode.OK, tokenStatus: HttpStatusCode.Forbidden));
        var handler = rig.Handler;
        var sender = rig.Sender;

        (await sender.SendAsync("t", _data, CancellationToken.None)).Should().Be(PushResult.Failed);
        handler.FcmRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task Access_Token_Is_Reused_Until_It_Expires_CA_037_10()
    {
        using var rig = new Rig(new Handler(HttpStatusCode.OK));
        var handler = rig.Handler;
        var sender = rig.Sender;

        await sender.SendAsync("a", _data, CancellationToken.None);
        await sender.SendAsync("b", _data, CancellationToken.None);

        handler.TokenRequests.Should().Be(1);
    }

    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddLogging();
        messaging::Knock.Messaging.Api.Modules.Avisos.AvisosModuleExtensions.AddPushSender(services);
        return services.BuildServiceProvider();
    }

    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["Notificaciones:PushHabilitado"] = "true",
        ["Notificaciones:Fcm:ProjectId"] = "project-test",
        ["Notificaciones:Fcm:ServiceAccount"] = AccountJson(),
    };

    private static string AccountJson()
    {
        using var rsa = RSA.Create(2048);
        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["client_email"] = "svc@project-test.example",
            ["private_key"] = rsa.ExportPkcs8PrivateKeyPem(),
            ["token_uri"] = "https://oauth.example/token",
        });
    }

    private sealed class Rig : IDisposable
    {
        private readonly HttpClient _http;
        private readonly FcmAccessToken _token;

        public Rig(Handler handler)
        {
            Handler = handler;
            var options = FcmOptions.Read(new ConfigurationBuilder().AddInMemoryCollection(ValidSettings()).Build());
            _http = new HttpClient(handler);
            _token = new FcmAccessToken(_http, options, TimeProvider.System);
            Sender = new FcmPushSender(_http, options, _token, NullLogger<FcmPushSender>.Instance);
        }

        public Handler Handler { get; }
        public FcmPushSender Sender { get; }

        public void Dispose()
        {
            _token.Dispose();
            _http.Dispose();
        }
    }

    internal sealed class Handler(HttpStatusCode fcmStatus, HttpStatusCode tokenStatus = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<HttpRequestMessage> FcmRequests { get; } = [];
        public int TokenRequests { get; private set; }
        public string? LastFcmBody { get; private set; }
        public string? LastFcmAuthorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host == "oauth.example")
            {
                TokenRequests++;
                return new HttpResponseMessage(tokenStatus) { Content = new StringContent("{\"access_token\":\"access\",\"expires_in\":3600}") };
            }

            FcmRequests.Add(request);
            LastFcmBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            LastFcmAuthorization = request.Headers.Authorization?.ToString();
            return new HttpResponseMessage(fcmStatus) { Content = new StringContent(fcmStatus == HttpStatusCode.NotFound ? "UNREGISTERED" : "{}") };
        }
    }
}
