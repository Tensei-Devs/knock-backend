using System.Net;
using AwesomeAssertions;
using Knock.SecurityTest.Support;
using Xunit;

namespace Knock.SecurityTest.Messaging;

/// <summary>
/// Specs 030 y 044: una persona con sesión válida no lee ni cambia las conversaciones ni los avisos de otra. Mensajería real en Production con MongoDB y Redis reales y los datos sembrados;
/// cada caso tiene el control de quien sí tiene derecho y comprueba que lo ajeno no cambió. Solo datos ficticios.
/// </summary>
public sealed class AccesoEntrePersonasSecurityTests
{
    private const string _conversations = "/api/v1/conversaciones/";
    private const string _notices = "/api/v1/avisos";

    [Theory]
    [InlineData("GET", "", HttpStatusCode.OK)]
    [InlineData("GET", "/mensajes", HttpStatusCode.OK)]
    [InlineData("POST", "/mensajes", HttpStatusCode.Created)]
    [InlineData("POST", "/leido", HttpStatusCode.NoContent)]
    public async Task OnlyTheTwoParticipants_UseTheConversation_CA_030_03(string method, string suffix, HttpStatusCode success)
    {
        await using var host = await MessagingAccessHost.StartAsync();
        var chat = await host.SeedChatAsync();
        var otherChat = await host.SeedChatAsync();
        var path = _conversations + chat.Id + suffix;
        var before = await host.SnapshotAsync(chat);

        using var anonymous = await SendAsync(host, method, path, null);
        using var expired = await SendAsync(host, method, path, TokenForge.Expired(chat.ClientSub));
        // Participa en otra conversación, como cliente o como técnico: eso no le abre esta.
        using var otherClient = await SendAsync(host, method, path, TokenForge.Valid(otherChat.ClientSub));
        using var otherTechnician = await SendAsync(host, method, path, TokenForge.Valid(otherChat.TechnicianSub));

        await SecureHttp.AssertProblemAsync(anonymous, HttpStatusCode.Unauthorized, MessagingAccessHost.MessageText);
        await SecureHttp.AssertProblemAsync(expired, HttpStatusCode.Unauthorized, MessagingAccessHost.MessageText);
        await SecureHttp.AssertProblemAsync(otherClient, HttpStatusCode.NotFound, MessagingAccessHost.MessageText, chat.ClientSub, chat.TechnicianSub);
        await SecureHttp.AssertProblemAsync(otherTechnician, HttpStatusCode.NotFound, MessagingAccessHost.MessageText, chat.ClientSub, chat.TechnicianSub);
        (await host.SnapshotAsync(chat)).Should().Be(before, "una petición rechazada no cambia nada");

        using var participant = await SendAsync(host, method, path, TokenForge.Valid(chat.ClientSub));

        var content = await participant.Content.ReadAsStringAsync();
        participant.StatusCode.Should().Be(success, "quien participa sí puede: " + content);
        if (method == "GET")
        {
            content.Should().Contain(suffix.Length == 0 ? chat.Id.ToString() : MessagingAccessHost.MessageText);
        }
        else
        {
            (await host.SnapshotAsync(chat)).Should().NotBe(before, "la misma petición de quien participa sí cambia la conversación");
        }
    }

    [Fact]
    public async Task TheConversationList_DoesNotBringOtherPeoplesConversations_CA_030_03()
    {
        await using var host = await MessagingAccessHost.StartAsync();
        var mine = await host.SeedChatAsync();
        var theirs = await host.SeedChatAsync();

        using var response = await SendAsync(host, "GET", "/api/v1/conversaciones", TokenForge.Valid(mine.ClientSub));
        using var owner = await SendAsync(host, "GET", "/api/v1/conversaciones", TokenForge.Valid(theirs.ClientSub));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain(mine.Id.ToString()).And.NotContain(theirs.Id.ToString());
        (await owner.Content.ReadAsStringAsync()).Should().Contain(theirs.Id.ToString(), "la conversación ajena existe y su participante la ve");
    }

    [Fact]
    public async Task AnotherPersonCannotMarkMyNoticeAsRead_CA_044_11_CA_044_18()
    {
        await using var host = await MessagingAccessHost.StartAsync();
        var owner = TokenForge.Sub();
        var notice = await host.SeedNoticeAsync(owner);
        var path = $"{_notices}/{notice}/leido";

        using var foreign = await SendAsync(host, "POST", path, TokenForge.Valid());

        var problem = await SecureHttp.AssertProblemAsync(foreign, HttpStatusCode.NotFound, owner);
        problem.Should().Contain("notice_not_found");
        (await host.IsReadAsync(notice)).Should().BeFalse("el aviso ajeno sigue sin leer");

        using var own = await SendAsync(host, "POST", path, TokenForge.Valid(owner));

        own.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await host.IsReadAsync(notice)).Should().BeTrue("su dueño sí lo marca");
    }

    [Fact]
    public async Task MarkingAllAsRead_OnlyTouchesTheCallersNotices_CA_044_18()
    {
        await using var host = await MessagingAccessHost.StartAsync();
        var owner = TokenForge.Sub();
        var other = TokenForge.Sub();
        var ownersNotice = await host.SeedNoticeAsync(owner);
        var othersNotice = await host.SeedNoticeAsync(other);

        using var response = await SendAsync(host, "POST", _notices + "/leidos", TokenForge.Valid(other));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await host.IsReadAsync(othersNotice)).Should().BeTrue("quien llama marca los suyos");
        (await host.IsReadAsync(ownersNotice)).Should().BeFalse("los de otra persona no se tocan");
    }

    [Fact]
    public async Task TheNoticeList_DoesNotBringOtherPeoplesNotices_CA_044_01()
    {
        await using var host = await MessagingAccessHost.StartAsync();
        var owner = TokenForge.Sub();
        var other = TokenForge.Sub();
        var ownersNotice = await host.SeedNoticeAsync(owner);
        var othersNotice = await host.SeedNoticeAsync(other);

        using var response = await SendAsync(host, "GET", _notices, TokenForge.Valid(other));
        using var own = await SendAsync(host, "GET", _notices, TokenForge.Valid(owner));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain(othersNotice.ToString()).And.NotContain(ownersNotice.ToString());
        (await own.Content.ReadAsStringAsync()).Should().Contain(ownersNotice.ToString());
    }

    /// <summary>Envía la petición; los dos <c>POST</c> de conversación llevan el cuerpo que piden y el resto va sin cuerpo.</summary>
    private static Task<HttpResponseMessage> SendAsync(MessagingAccessHost host, string method, string path, string? bearer)
    {
        if (method == "POST" && path.EndsWith("/mensajes", StringComparison.Ordinal))
        {
            return SecureHttp.PostJsonAsync(host.Client, path, new { texto = "Llego en diez minutos" }, bearer);
        }

        return method == "POST" && path.StartsWith(_conversations, StringComparison.Ordinal)
            ? SecureHttp.PostJsonAsync(host.Client, path, new { hasta = DateTime.UtcNow }, bearer)
            : SecureHttp.SendAsync(host.Client, new HttpMethod(method), path, bearer: bearer);
    }
}
