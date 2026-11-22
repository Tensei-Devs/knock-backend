using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using MongoDB.Bson;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>Spec 020, R-04 y CA-18: las fotos de una solicitud son de quien la crea; no se pueden adjuntar fotos de otra persona (MongoDB real, módulo de archivos de mentira con dueños).</summary>
public sealed class Spec020FotosTests
{
    [Fact]
    public async Task Rechaza_Foto_De_Otra_Persona_CA_020_18()
    {
        await using var host = await Spec020Host.StartAsync();
        var otherPerson = Guid.NewGuid().ToString("D");
        var own = Guid.CreateVersion7().ToString("N");
        var foreign = Guid.CreateVersion7().ToString("N");
        host.Photos.Owners = new Dictionary<string, string> { [own] = host.ClientSub, [foreign] = otherPerson };
        var onlyForeign = host.ValidBody();
        onlyForeign["fotos"] = new[] { foreign };
        var mixed = host.ValidBody();
        mixed["fotos"] = new[] { own, foreign };

        using var foreignResponse = await host.CreateAsync(onlyForeign);
        using var mixedResponse = await host.CreateAsync(mixed);

        foreignResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        mixedResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "una sola foto ajena invalida toda la solicitud");
        foreach (var response in new[] { foreignResponse, mixedResponse })
        {
            var raw = await response.Content.ReadAsStringAsync();
            JsonElement.Parse(raw).GetProperty("code").GetString().Should().Be("photo_invalid");
            ProblemBody.Reasons(raw).Should().NotContain(foreign).And.NotContain(otherPerson, "no dice cuál foto ni de quién es");
        }

        host.Photos.LastOwnerId.Should().Be(host.ClientSub, "la propiedad se verifica contra el sub del token");
        (await host.CountRequestsAsync()).Should().Be(0, "no se guarda nada");
    }

    [Fact]
    public async Task Acepta_Solo_Fotos_Propias_CA_020_18()
    {
        await using var host = await Spec020Host.StartAsync();
        var otherPerson = Guid.NewGuid().ToString("D");
        var photo = Guid.CreateVersion7().ToString("N");
        host.Photos.Owners = new Dictionary<string, string> { [photo] = host.ClientSub };
        var body = host.ValidBody();
        body["fotos"] = new[] { photo };

        using var asOwner = await host.CreateAsync(body);
        using var asOtherPerson = await host.CreateAsync(body, sub: otherPerson);

        asOwner.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await Spec020Host.ReadAsync(asOwner);
        json.GetProperty("fotos").GetArrayLength().Should().Be(1);
        json.GetProperty("fotos")[0].GetProperty("id").GetString().Should().Be(photo);
        asOtherPerson.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "la misma foto no sirve a otra persona");
        JsonElement.Parse(await asOtherPerson.Content.ReadAsStringAsync()).GetProperty("code").GetString().Should().Be("photo_invalid");
        host.Photos.LastOwnerId.Should().Be(otherPerson);
        (await host.CountRequestsAsync()).Should().Be(1, "solo se guardó la solicitud de la dueña de la foto");
    }

    [Fact]
    public async Task Guarda_El_Id_De_Archivo_En_Minuscula_R_04()
    {
        await using var host = await Spec020Host.StartAsync();
        var photo = Guid.CreateVersion7().ToString("N");
        host.Photos.Owners = new Dictionary<string, string> { [photo] = host.ClientSub };
        var body = host.ValidBody();
        body["fotos"] = new[] { photo.ToUpperInvariant() };

        using var response = await host.CreateAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await Spec020Host.ReadAsync(response)).GetProperty("fotos")[0].GetProperty("id").GetString().Should().Be(photo, "se guarda y se devuelve en la forma canónica");
    }
}
