using System.Net;
using AwesomeAssertions;

namespace Knock.SecurityTest.Support;

/// <summary>
/// Una ruta de Mercado y quién puede usarla. <c>Path</c> y <c>Body</c> salen de la escena; <c>Arrange</c> deja los datos en el estado en que la ruta funciona para quien tiene derecho;
/// <c>Allowed</c> son esas personas y <c>Denied</c> lo que debe recibir cada una de las demás.
/// </summary>
internal sealed record AccessCase(
    string Method,
    Func<MarketScene, string> Path,
    Func<MarketScene, object?> Body,
    Func<MarketAccessHost, MarketScene, Task> Arrange,
    Actor[] Allowed,
    HttpStatusCode Success,
    Dictionary<Actor, HttpStatusCode> Denied)
{
    /// <summary>La ruta no lleva cuerpo.</summary>
    public static readonly Func<MarketScene, object?> NoBody = _ => null;

    /// <summary>Los datos sembrados ya sirven tal cual.</summary>
    public static readonly Func<MarketAccessHost, MarketScene, Task> Nothing = (_, _) => Task.CompletedTask;

    private bool IsRead => Method == "GET";

    /// <summary>Los nombres de una tabla de casos como filas de <c>MemberData</c>.</summary>
    public static TheoryData<string> Names(Dictionary<string, AccessCase> cases)
    {
        var data = new TheoryData<string>();
        foreach (var name in cases.Keys)
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>
    /// Sobre los mismos datos sembrados: sin token, con token vencido y con cada persona sin derecho la ruta rechaza sin filtrar la dirección ni el cliente y sin cambiar nada; después, la
    /// misma petición de quien sí tiene derecho funciona (control: el rechazo no se debía a los datos ni al cuerpo).
    /// </summary>
    public async Task VerifyAsync(MarketAccessHost host, MarketScene scene)
    {
        await Arrange(host, scene);
        var before = await host.SnapshotAsync(scene);

        await AssertDeniedAsync(host, scene, null, HttpStatusCode.Unauthorized);
        await AssertDeniedAsync(host, scene, TokenForge.Expired(scene.Sub(Allowed[0])), HttpStatusCode.Unauthorized);
        foreach (var (actor, status) in Denied)
        {
            await AssertDeniedAsync(host, scene, TokenForge.Valid(scene.Sub(actor)), status);
        }

        (await host.SnapshotAsync(scene)).Should().Be(before, "una petición rechazada no cambia nada");

        // Una escritura solo se puede repetir una vez; una lectura se comprueba con todas las personas con derecho.
        foreach (var actor in IsRead ? Allowed : Allowed[..1])
        {
            using var response = await SendAsync(host, scene, TokenForge.Valid(scene.Sub(actor)));
            var content = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(Success, $"{actor} tiene derecho: {content}");
            if (IsRead)
            {
                content.Should().Contain(Marker(scene), "la lectura entrega el dato sembrado");
            }
        }

        if (!IsRead)
        {
            (await host.SnapshotAsync(scene)).Should().NotBe(before, "la misma petición de quien tiene derecho sí cambia los datos");
        }
    }

    /// <summary>El identificador que debe venir en la lectura permitida.</summary>
    private string Marker(MarketScene scene)
    {
        var path = Path(scene);
        if (path.EndsWith("/cotizaciones", StringComparison.Ordinal))
        {
            return scene.QuoteId.ToString();
        }

        if (path.EndsWith("/ajustes-pendientes", StringComparison.Ordinal))
        {
            return scene.AdjustmentId.ToString();
        }

        return path.Contains("/visitas/", StringComparison.Ordinal) ? scene.VisitId.ToString() : scene.RequestId.ToString();
    }

    private Task<HttpResponseMessage> SendAsync(MarketAccessHost host, MarketScene scene, string? bearer) =>
        host.SendAsync(new HttpMethod(Method), Path(scene), bearer, Body(scene));

    private async Task AssertDeniedAsync(MarketAccessHost host, MarketScene scene, string? bearer, HttpStatusCode status)
    {
        using var response = await SendAsync(host, scene, bearer);
        await SecureHttp.AssertProblemAsync(response, status, MarketAccessHost.Address, scene.Sub(Actor.Client));
    }
}
